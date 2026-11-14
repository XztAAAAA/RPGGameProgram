using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CombatFactionMarker))]
public class Attack : MonoBehaviour
{
    [Header("攻击设置")]
    [SerializeField, Min(0f)]
    private float damage;

    [SerializeField, Min(0.1f)]
    private float cooldown;

    [Header("攻击范围")]
    [SerializeField, Min(0.1f)]
    private float hitRadius;

    [SerializeField]
    private float hitOffset;

    [SerializeField]
    private float attackHeight;

    [SerializeField]
    private LayerMask hitLayers = ~0;

    [Header("攻击范围显示")]
    [SerializeField, Min(0.01f)]
    private float rangeDisplayDuration = 0.3f;

    private const int RangeSegments = 64;
    private const float RangeLineWidth = 0.04f;
    private static readonly HashSet<Attack> ActiveSpecialSkills = new HashSet<Attack>();
    private CombatFactionMarker ownerFaction;
    private float nextAttackTime;
    private cfg.Skill[] configuredSkills;
    private float[] nextSkillAttackTimes;
    private int nextSkillIndex;
    private int selectedSkillIndex = -1;
    private LineRenderer[] rangeLines;
    private Material rangeMaterial;
    private float rangeDisplayEndTime;
    private bool rangeDisplayFailed;
    private cfg.Skill executingSpecialSkill;
    private Transform specialSkillTarget;
    private float specialSkillStartTime;
    private float staggerEndTime;
    private bool specialSkillParried;
    private cfg.Skill activeMeleeSkill;
    private float meleeSkillActiveEndTime;
    private HashSet<Health> meleeSkillHitTargets;
    private bool suppressMeleeSkillDamage;
    private GameObject parryWarning;
    private bool parryWarningFailed;

    public float Damage => damage;
    public bool IsStaggered => Time.time < staggerEndTime;
    public bool IsCurrentSkillParried => specialSkillParried;
    public bool IsParryWindowOpen
    {
        get
        {
            if (!isActiveAndEnabled || executingSpecialSkill == null || specialSkillParried)
                return false;

            float elapsed = Time.time - specialSkillStartTime;
            float windowStart = Mathf.Max(0f, executingSpecialSkill.StartupTime - executingSpecialSkill.TelegraphTime);
            return executingSpecialSkill.TelegraphTime > 0f &&
                elapsed >= windowStart && elapsed < executingSpecialSkill.StartupTime;
        }
    }
    public bool HasConfiguredSkills =>
        configuredSkills != null && configuredSkills.Length > 0;

    public bool CanAttack
    {
        get
        {
            if (!isActiveAndEnabled || IsStaggered)
                return false;

            if (configuredSkills == null)
                return Time.time >= nextAttackTime;

            for (int i = 0; i < configuredSkills.Length; i++)
            {
                if (Time.time >= nextSkillAttackTimes[i])
                    return true;
            }

            return false;
        }
    }

    public void InitializeDamage(float configuredDamage)
    {
        damage = Mathf.Max(0f, configuredDamage);
    }

    public void InitializeFromConfig(cfg.Skill skill)
    {
        if (skill == null)
        {
            Debug.LogError("Attack：技能配置为空，无法初始化攻击属性。", this);
            return;
        }

        configuredSkills = null;
        nextSkillAttackTimes = null;
        selectedSkillIndex = -1;
        damage = Mathf.Max(0f, skill.Damage);
        cooldown = Mathf.Max(0.1f, skill.Cooldown);
        hitRadius = Mathf.Max(0.1f, skill.HitRadius);
        hitOffset = Mathf.Max(0f, skill.HitOffset);
    }

    public void InitializeFromConfig(cfg.Skill[] skills)
    {
        if (skills == null || skills.Length == 0)
        {
            Debug.LogError("Attack：技能配置为空，无法初始化攻击属性。", this);
            return;
        }

        configuredSkills = skills;
        nextSkillAttackTimes = new float[skills.Length];
        nextSkillIndex = 0;
        selectedSkillIndex = -1;
    }

    public bool SelectAvailableSkill(Transform target)
    {
        if (!isActiveAndEnabled || IsStaggered ||
            !HasConfiguredSkills || nextSkillAttackTimes == null || target == null)
        {
            selectedSkillIndex = -1;
            return false;
        }

        selectedSkillIndex = FindReadySkill(target);
        return selectedSkillIndex >= 0;
    }

    public bool HasSkillInRange(Transform target)
    {
        if (!HasConfiguredSkills || target == null)
            return false;

        float targetDistance = GetHorizontalDistance(target);
        foreach (cfg.Skill skill in configuredSkills)
        {
            if (skill != null && targetDistance <= skill.TriggerRange)
                return true;
        }

        return false;
    }

    public bool IsSkillInRange(cfg.Skill skill, Transform target)
    {
        return skill != null &&
            target != null &&
            GetHorizontalDistance(target) <= skill.TriggerRange;
    }

    public bool TryBeginSelectedSkill(Transform target, out cfg.Skill skill)
    {
        skill = null;
        if (!isActiveAndEnabled || IsStaggered || !HasConfiguredSkills ||
            selectedSkillIndex < 0 ||
            selectedSkillIndex >= configuredSkills.Length ||
            target == null)
        {
            return false;
        }

        int skillIndex = selectedSkillIndex;
        cfg.Skill candidate = configuredSkills[skillIndex];
        if (candidate == null ||
            Time.time < nextSkillAttackTimes[skillIndex] ||
            GetHorizontalDistance(target) > candidate.TriggerRange)
        {
            selectedSkillIndex = -1;
            return false;
        }

        skill = candidate;
        nextSkillAttackTimes[skillIndex] =
            Time.time + Mathf.Max(0.1f, candidate.Cooldown);
        nextSkillIndex = (skillIndex + 1) % configuredSkills.Length;
        selectedSkillIndex = -1;
        return true;
    }

    public void BeginSkillExecution(cfg.Skill skill, Transform target, float startTime)
    {
        EndSkillExecution();
        if (skill.SkillKind != 2 || ownerFaction == null ||
            ownerFaction.Faction != CombatFaction.Enemy)
        {
            return;
        }

        if (float.IsNaN(skill.StartupTime) || float.IsInfinity(skill.StartupTime) ||
            float.IsNaN(skill.TelegraphTime) || float.IsInfinity(skill.TelegraphTime) ||
            float.IsNaN(skill.StaggerTime) || float.IsInfinity(skill.StaggerTime) ||
            skill.StartupTime < 0f || skill.TelegraphTime < 0f || skill.StaggerTime < 0f)
        {
            Debug.LogError($"Attack：特殊技能 {skill.SkillId} 的前摇、格挡窗口和硬直时间必须是非负有限数值。", this);
            return;
        }

        executingSpecialSkill = skill;
        specialSkillTarget = target;
        specialSkillStartTime = startTime;
        ActiveSpecialSkills.Add(this);
    }

    public void EndSkillExecution()
    {
        EndMeleeSkillHitbox();
        ActiveSpecialSkills.Remove(this);
        executingSpecialSkill = null;
        specialSkillTarget = null;
        specialSkillParried = false;
        HideParryWarning();
    }

    private static bool TryParrySpecialSkills(Transform player)
    {
        bool parried = false;
        foreach (Attack enemy in ActiveSpecialSkills)
        {
            if (!enemy.IsParryWindowOpen ||
                enemy.specialSkillTarget != player)
            {
                continue;
            }

            cfg.Skill skill = enemy.executingSpecialSkill;
            enemy.specialSkillParried = true;
            parried = true;
            enemy.HideParryWarning();
            enemy.staggerEndTime = Time.time + skill.StaggerTime;
            enemy.selectedSkillIndex = -1;
            UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
        }

        return parried;
    }

    private void Awake()
    {
        ownerFaction = GetComponent<CombatFactionMarker>();
    }

    /// <summary>
    /// 尝试执行一次攻击。
    /// 玩家输入和敌人 AI 都调用这个方法。
    /// </summary>
    public bool TryAttack(Transform target = null)
    {
        if (!isActiveAndEnabled || ownerFaction == null || IsStaggered)
            return false;

        bool parried = ownerFaction.Faction == CombatFaction.Player &&
            TryParrySpecialSkills(transform);

        if (configuredSkills == null)
        {
            if (Time.time < nextAttackTime)
                return false;

            nextAttackTime = Time.time + cooldown;
        }
        else
        {
            int skillIndex = FindReadySkill(target);
            if (skillIndex < 0)
                return false;

            cfg.Skill skill = configuredSkills[skillIndex];
            if (skill.AttackType == 2 && target == null)
            {
                Debug.LogError("Attack：远程技能需要指定目标。", this);
                return false;
            }

            damage = Mathf.Max(0f, skill.Damage);
            cooldown = Mathf.Max(0.1f, skill.Cooldown);
            hitRadius = Mathf.Max(0.1f, skill.HitRadius);
            hitOffset = Mathf.Max(0f, skill.HitOffset);
            nextSkillAttackTimes[skillIndex] = Time.time + cooldown;
            nextSkillIndex = (skillIndex + 1) % configuredSkills.Length;
            ApplySkillDamage(skill, target, parried);
            return true;
        }

        ApplyAttack(damage, hitRadius, hitOffset, parried);
        return true;
    }

    public void ApplySkillDamage(cfg.Skill skill, Transform target, bool suppressDamage = false)
    {
        if (skill == null)
        {
            Debug.LogError("Attack：技能配置为空，无法造成伤害。", this);
            return;
        }

        damage = Mathf.Max(0f, skill.Damage);
        hitRadius = Mathf.Max(0.1f, skill.HitRadius);
        hitOffset = Mathf.Max(0f, skill.HitOffset);
        if (skill.AttackType == 1)
        {
            BeginMeleeSkillHitbox(skill, suppressDamage);
        }
        else if (skill.AttackType == 2)
        {
            if (target == null)
            {
                Debug.LogError("Attack：远程技能需要指定目标。", this);
                return;
            }

            Health targetHealth = target.GetComponent<Health>();
            CombatFactionMarker targetFaction = target.GetComponent<CombatFactionMarker>();
            if (targetHealth == null || targetFaction == null || ownerFaction == null ||
                targetFaction.Faction == ownerFaction.Faction)
            {
                Debug.LogError("Attack：远程技能目标必须是具有生命值的敌对角色。", this);
                return;
            }

            if (float.IsNaN(skill.TriggerRange) || float.IsInfinity(skill.TriggerRange) ||
                skill.TriggerRange <= 0f)
            {
                Debug.LogError("Attack：远程技能的飞行距离必须是大于 0 的有限数值。", this);
                return;
            }

            if (float.IsNaN(skill.ProjectileSpeed) || float.IsInfinity(skill.ProjectileSpeed) ||
                skill.ProjectileSpeed <= 0f)
            {
                Debug.LogError($"Attack：远程技能 {skill.SkillId} 的弹幕速度必须是大于 0 的有限数值。", this);
                return;
            }

            if (!suppressDamage)
            {
                Vector3 start = transform.position + Vector3.up * attackHeight;
                SkillProjectile.Create(start, transform.forward, ownerFaction.Faction, damage,
                    skill.ProjectileSpeed, skill.TriggerRange, hitLayers, skill.CanBlockProjectile);
            }
        }
        else
        {
            Debug.LogError($"Attack：不支持的攻击类型 {skill.AttackType}。", this);
        }
    }

    private void BeginMeleeSkillHitbox(cfg.Skill skill, bool suppressDamage)
    {
        float activeTime = skill.ActiveTime;
        if (float.IsNaN(activeTime) || float.IsInfinity(activeTime))
        {
            Debug.LogError($"Attack：近战技能 {skill.SkillId} 的持续时间必须是有限数值。", this);
            activeTime = 0f;
        }

        EndMeleeSkillHitbox();
        activeMeleeSkill = skill;
        meleeSkillActiveEndTime = Time.time + Mathf.Max(0f, activeTime);
        meleeSkillHitTargets = new HashSet<Health>();
        suppressMeleeSkillDamage = suppressDamage;
        ApplyMeleeSkillHitbox(true);

        if (activeTime <= 0f)
            EndMeleeSkillHitbox();
    }

    private void UpdateMeleeSkillHitbox()
    {
        if (activeMeleeSkill == null)
            return;

        if (Time.time >= meleeSkillActiveEndTime)
        {
            EndMeleeSkillHitbox();
            return;
        }

        ApplyMeleeSkillHitbox(false);
    }

    private void ApplyMeleeSkillHitbox(bool showRange)
    {
        ApplyAttack(
            Mathf.Max(0f, activeMeleeSkill.Damage),
            Mathf.Max(0.1f, activeMeleeSkill.HitRadius),
            Mathf.Max(0f, activeMeleeSkill.HitOffset),
            suppressMeleeSkillDamage,
            meleeSkillHitTargets,
            showRange
        );
    }

    private void EndMeleeSkillHitbox()
    {
        activeMeleeSkill = null;
        meleeSkillHitTargets = null;
        suppressMeleeSkillDamage = false;
    }

    private void ApplyAttack(
        float attackDamage,
        float attackRadius,
        float attackOffset,
        bool suppressDamage,
        HashSet<Health> hitTargets = null,
        bool showRange = true)
    {
        Vector3 attackCenter =
            transform.position
            + Vector3.up * attackHeight
            + transform.forward * attackOffset;

        if (showRange)
            ShowAttackRange(attackCenter, attackRadius);

        if (ownerFaction.Faction == CombatFaction.Player)
            SkillProjectile.BlockInRange(attackCenter, attackRadius, ownerFaction.Faction);

        if (suppressDamage)
            return;

        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            attackRadius,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        // 防止同一目标有多个 Collider 时重复受到伤害
        HashSet<Health> damagedTargets = hitTargets ?? new HashSet<Health>();

        foreach (Collider hit in hits)
        {
            Health targetHealth =
                hit.GetComponentInParent<Health>();

            if (targetHealth == null || targetHealth.IsDead)
                continue;

            CombatFactionMarker targetFaction =
                targetHealth.GetComponent<CombatFactionMarker>();

            if (targetFaction == null)
            {
                targetFaction =
                    targetHealth.GetComponentInParent<CombatFactionMarker>();
            }

            if (targetFaction == null)
                continue;

            if (targetFaction == ownerFaction ||
                targetFaction.Faction == ownerFaction.Faction)
            {
                continue;
            }

            if (damagedTargets.Add(targetHealth))
            {
                targetHealth.TakeDamage(attackDamage);
            }
        }
    }

    private int FindReadySkill(Transform target)
    {
        float targetDistance = target != null
            ? GetHorizontalDistance(target)
            : 0f;

        for (int offset = 0; offset < configuredSkills.Length; offset++)
        {
            int index = (nextSkillIndex + offset) % configuredSkills.Length;
            cfg.Skill skill = configuredSkills[index];
            if (skill == null ||
                Time.time < nextSkillAttackTimes[index] ||
                (target != null && targetDistance > skill.TriggerRange))
            {
                continue;
            }

            return index;
        }

        return -1;
    }

    private float GetHorizontalDistance(Transform target)
    {
        Vector3 targetOffset = target.position - transform.position;
        targetOffset.y = 0f;
        return targetOffset.magnitude;
    }

    private void ShowAttackRange(Vector3 center, float radius)
    {
        if (rangeDisplayFailed)
            return;

        if (rangeLines == null && !CreateRangeLines())
            return;

        for (int plane = 0; plane < rangeLines.Length; plane++)
        {
            LineRenderer line = rangeLines[plane];
            for (int i = 0; i < RangeSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / RangeSegments;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                Vector3 offset = plane == 0 ? new Vector3(x, 0f, y) :
                    plane == 1 ? new Vector3(x, y, 0f) : new Vector3(0f, x, y);
                line.SetPosition(i, center + offset);
            }

            line.enabled = true;
        }

        rangeDisplayEndTime = Time.unscaledTime + Mathf.Max(0.01f, rangeDisplayDuration);
    }

    private bool CreateRangeLines()
    {
        Shader shader = Resources.Load<Shader>("Shaders/AttackRange");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("Attack：无法加载攻击范围显示着色器。", this);
            rangeDisplayFailed = true;
            return false;
        }

        rangeMaterial = new Material(shader);
        rangeLines = new LineRenderer[3];
        for (int i = 0; i < rangeLines.Length; i++)
        {
            GameObject lineObject = new GameObject("Attack Range");
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = RangeSegments;
            line.widthMultiplier = RangeLineWidth;
            line.sharedMaterial = rangeMaterial;
            line.startColor = line.endColor = new Color(1f, 0.15f, 0.1f, 0.85f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            rangeLines[i] = line;
        }

        return true;
    }

    private void Update()
    {
        UpdateMeleeSkillHitbox();

        if (IsParryWindowOpen)
        {
            if (parryWarning == null && !parryWarningFailed)
            {
                parryWarning = ParryWarning.Create(transform);
                parryWarningFailed = parryWarning == null;
            }

            if (parryWarning != null && !parryWarning.activeSelf)
                parryWarning.SetActive(true);
        }
        else
        {
            HideParryWarning();
        }

        if (rangeLines != null && Time.unscaledTime >= rangeDisplayEndTime)
            HideAttackRange();
    }

    private void HideParryWarning()
    {
        if (parryWarning != null && parryWarning.activeSelf)
            parryWarning.SetActive(false);
    }

    private void OnDisable()
    {
        EndSkillExecution();
        HideAttackRange();
    }

    private void HideAttackRange()
    {
        if (rangeLines == null)
            return;

        foreach (LineRenderer line in rangeLines)
            line.enabled = false;
    }

    private void OnDestroy()
    {
        if (rangeLines != null)
        {
            foreach (LineRenderer line in rangeLines)
            {
                if (line != null)
                    Destroy(line.gameObject);
            }
        }

        if (rangeMaterial != null)
            Destroy(rangeMaterial);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 attackCenter =
            transform.position
            + Vector3.up * attackHeight
            + transform.forward * hitOffset;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackCenter, hitRadius);
    }
}