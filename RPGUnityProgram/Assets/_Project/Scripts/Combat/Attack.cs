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

    public float Damage => damage;
    public bool HasConfiguredSkills =>
        configuredSkills != null && configuredSkills.Length > 0;

    public bool CanAttack
    {
        get
        {
            if (!isActiveAndEnabled)
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
        if (!HasConfiguredSkills || nextSkillAttackTimes == null || target == null)
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
        if (!HasConfiguredSkills ||
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
        if (!isActiveAndEnabled || ownerFaction == null)
            return false;

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
            ApplySkillDamage(skill, target);
            return true;
        }

        ApplyAttack(damage, hitRadius, hitOffset);
        return true;
    }

    public void ApplySkillDamage(cfg.Skill skill, Transform target)
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
            ApplyAttack(damage, hitRadius, hitOffset);
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

            Vector3 start = transform.position + Vector3.up * attackHeight;
            SkillProjectile.Create(start, transform.forward, ownerFaction.Faction, damage,
                skill.ProjectileSpeed, skill.TriggerRange, hitLayers, skill.CanBlockProjectile);
        }
        else
        {
            Debug.LogError($"Attack：不支持的攻击类型 {skill.AttackType}。", this);
        }
    }

    private void ApplyAttack(float attackDamage, float attackRadius, float attackOffset)
    {
        Vector3 attackCenter =
            transform.position
            + Vector3.up * attackHeight
            + transform.forward * attackOffset;

        ShowAttackRange(attackCenter, attackRadius);

        if (ownerFaction.Faction == CombatFaction.Player)
            SkillProjectile.BlockInRange(attackCenter, attackRadius, ownerFaction.Faction);

        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            attackRadius,
            hitLayers,
            QueryTriggerInteraction.Ignore
        );

        // 防止同一目标有多个 Collider 时重复受到伤害
        HashSet<Health> damagedTargets = new HashSet<Health>();

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
        if (rangeLines != null && Time.unscaledTime >= rangeDisplayEndTime)
            HideAttackRange();
    }

    private void OnDisable()
    {
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