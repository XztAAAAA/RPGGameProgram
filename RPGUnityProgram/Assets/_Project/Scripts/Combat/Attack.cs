using System;
using System.Collections;
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

    [Header("近战格挡时停")]
    [Tooltip("格挡成功后的时停时长（秒，使用真实时间）；设为 0 可关闭时停。")]
    [SerializeField, Min(0f)]
    private float parryHitStopDuration = 0.1f;

    private const int RangeSegments = 64;
    private const float RangeLineWidth = 0.04f;
    private static readonly HashSet<Attack> ActiveSpecialSkills = new HashSet<Attack>();
    private readonly HashSet<Health> oneShotHitTargets = new HashSet<Health>();
    private readonly HashSet<Health> meleeSkillHitTargets = new HashSet<Health>();
    private Collider[] hitBuffer = new Collider[16];
    private CombatFactionMarker ownerFaction;
    private PlayerMovement playerMovement;
    private float nextAttackTime;
    private float attackMovementLockEndTime;
    private bool isExecutingSkill;
    private cfg.Skill basicAttackSkill;
    private cfg.Skill[] configuredSkills;
    private float[] nextSkillAttackTimes;
    private int nextSkillIndex;
    private int preferredAttackType;
    private bool hasMeleeAndRangedSkills;
    private int selectedSkillIndex = -1;
    private LineRenderer[] rangeLines;
    private Material rangeMaterial;
    private float rangeDisplayEndTime;
    private bool rangeDisplayUsesScaledTime;
    private bool rangeDisplayFailed;
    private cfg.Skill executingSpecialSkill;
    private Transform specialSkillTarget;
    private float specialSkillStartTime;
    private float parryWindowBonus;
    private float staggerEndTime;
    private bool specialSkillParried;
    private cfg.Skill activeMeleeSkill;
    private float meleeSkillActiveEndTime;
    private bool suppressMeleeSkillDamage;
    private Coroutine parryHitStop;
    private float timeScaleBeforeParry;
    private uint parryTimeScaleVersion;
    private GameObject parryWarning;
    private bool parryWarningFailed;

    public float Damage => damage;
    public event Action AttackStarted;
    public event Action MeleeParried;
    public event Action<cfg.Skill> SkillExecutionStarted;
    public event Action SkillParried;
    public int LastMeleeParryFacingFrame { get; private set; } = -1;
    public bool IsStaggered => Time.time < staggerEndTime;
    public bool IsAttacking => isExecutingSkill || Time.time < attackMovementLockEndTime;
    public float CurrentAttackDuration { get; private set; }
    public bool IsCurrentSkillParried => specialSkillParried;
    public bool IsParryWindowOpen => IsParryWindowOpenFor(0f);

    public bool IsParryWindowOpenFor(float additionalWindow)
    {
        if (!isActiveAndEnabled || executingSpecialSkill == null ||
            specialSkillParried || float.IsNaN(additionalWindow) ||
            float.IsInfinity(additionalWindow))
        {
            return false;
        }

        float elapsed = Time.time - specialSkillStartTime;
        float windowStart = Mathf.Max(
            0f,
            executingSpecialSkill.StartupTime -
                executingSpecialSkill.TelegraphTime
        );
        return executingSpecialSkill.TelegraphTime > 0f &&
            elapsed >= windowStart &&
            elapsed < executingSpecialSkill.StartupTime +
                Mathf.Max(0f, additionalWindow);
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

    public void ApplyUpgrade(
        cfg.upgradeEnum effectType,
        float value,
        int stacks)
    {
        float totalValue = value * stacks;
        switch (effectType)
        {
            case cfg.upgradeEnum.sword_damage:
                damage *= 1f + totalValue;
                break;

            case cfg.upgradeEnum.sword_speed:
                cooldown = Mathf.Max(
                    0.1f,
                    cooldown * Mathf.Max(0.1f, 1f - totalValue)
                );
                break;

            case cfg.upgradeEnum.sword_range:
                hitRadius = Mathf.Max(
                    0.1f,
                    hitRadius * (1f + totalValue)
                );
                break;

            case cfg.upgradeEnum.parry_window:
                parryWindowBonus += totalValue;
                break;

            default:
                Debug.LogError(
                    $"Attack：不支持应用强化效果 {effectType}。",
                    this
                );
                break;
        }
    }

    public void InitializeFromConfig(cfg.Skill skill)
    {
        if (skill == null)
        {
            Debug.LogError("Attack：技能配置为空，无法初始化攻击属性。", this);
            return;
        }

        configuredSkills = null;
        hasMeleeAndRangedSkills = false;
        preferredAttackType = 0;
        basicAttackSkill = skill;
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

        basicAttackSkill = null;
        configuredSkills = skills;
        nextSkillAttackTimes = new float[skills.Length];
        nextSkillIndex = 0;
        selectedSkillIndex = -1;
        bool hasMelee = false;
        bool hasRanged = false;
        foreach (cfg.Skill skill in skills)
        {
            if (skill == null)
                continue;
            hasMelee |= skill.AttackType == 1;
            hasRanged |= skill.AttackType == 2;
        }
        hasMeleeAndRangedSkills = hasMelee && hasRanged;
    }

    public bool ConfigureSkillPreference(string preference)
    {
        switch (preference)
        {
            case "none":
                preferredAttackType = 0;
                return true;
            case "melee":
                preferredAttackType = 1;
                return true;
            case "ranged":
                preferredAttackType = 2;
                return true;
            default:
                Debug.LogError($"Attack：无效的攻击偏好 '{preference}'，请使用 none、melee 或 ranged。", this);
                return false;
        }
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

    public bool ShouldChaseForMelee(Transform target)
    {
        if (!hasMeleeAndRangedSkills || target == null)
            return false;

        float distance = GetHorizontalDistance(target);
        foreach (cfg.Skill skill in configuredSkills)
        {
            if (skill != null && skill.AttackType == 1 && distance <= skill.TriggerRange)
                return false;
        }

        return true;
    }

    public bool HasReadySkillInRange(Transform target)
    {
        return isActiveAndEnabled && !IsStaggered && target != null &&
            HasConfiguredSkills && nextSkillAttackTimes != null &&
            FindReadySkill(target) >= 0;
    }

    public bool TryGetRangedSpacing(out float minDistance, out float maxDistance)
    {
        minDistance = 0f;
        maxDistance = 0f;
        if (preferredAttackType != 2 || !HasConfiguredSkills)
            return false;

        float meleeRange = 0f;
        float rangedRange = 0f;
        foreach (cfg.Skill skill in configuredSkills)
        {
            if (skill == null || float.IsNaN(skill.TriggerRange) ||
                float.IsInfinity(skill.TriggerRange) || skill.TriggerRange <= 0f)
                continue;

            if (skill.AttackType == 1)
                meleeRange = Mathf.Max(meleeRange, skill.TriggerRange);
            else if (skill.AttackType == 2)
                rangedRange = Mathf.Max(rangedRange, skill.TriggerRange);
        }

        if (rangedRange <= 0f)
            return false;

        float desiredMin = meleeRange > 0f
            ? meleeRange * 2f
            : Mathf.Min(4f, rangedRange * 0.2f);
        minDistance = Mathf.Min(desiredMin, rangedRange * 0.65f);
        maxDistance = Mathf.Min(minDistance * 1.5f, rangedRange * 0.9f);
        return true;
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
        isExecutingSkill = true;
        CurrentAttackDuration = TryGetMovementLockDuration(skill, out float duration)
            ? duration
            : 0f;
        SkillExecutionStarted?.Invoke(skill);
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
        isExecutingSkill = false;
        EndMeleeSkillHitbox();
        ActiveSpecialSkills.Remove(this);
        executingSpecialSkill = null;
        specialSkillTarget = null;
        specialSkillParried = false;
        HideParryWarning();
    }

    private bool TryParrySpecialSkills(float attackRadius, float attackOffset)
    {
        bool parried = false;
        bool meleeParried = false;
        Vector3 meleeParryDirection = Vector3.zero;
        float nearestMeleeDistanceSquared = float.PositiveInfinity;
        Vector3 attackCenter = GetAttackCenter(attackOffset);
        foreach (Attack enemy in ActiveSpecialSkills)
        {
            if (!enemy.IsParryWindowOpenFor(parryWindowBonus) ||
                enemy.specialSkillTarget != transform)
            {
                continue;
            }

            cfg.Skill skill = enemy.executingSpecialSkill;
            float enemyRadius = Mathf.Max(0.1f, skill.HitRadius);
            Vector3 enemyCenter = enemy.GetAttackCenter(Mathf.Max(0f, skill.HitOffset));
            bool inRange;
            if (skill.AttackType == 1)
            {
                inRange = enemy.IsTargetInAttackRange(transform, enemyCenter, enemyRadius);
            }
            else
            {
                float combinedRadius = attackRadius + enemyRadius;
                inRange = (attackCenter - enemyCenter).sqrMagnitude <= combinedRadius * combinedRadius;
            }

            if (!inRange)
                continue;

            enemy.specialSkillParried = true;
            enemy.SkillParried?.Invoke();
            parried = true;
            meleeParried |= skill.AttackType == 1;
            if (skill.AttackType == 1)
            {
                Vector3 direction = enemy.transform.position - transform.position;
                direction.y = 0f;
                float distanceSquared = direction.sqrMagnitude;
                if (distanceSquared > 0.000001f && distanceSquared < nearestMeleeDistanceSquared)
                {
                    nearestMeleeDistanceSquared = distanceSquared;
                    meleeParryDirection = direction;
                }
            }
            enemy.HideParryWarning();
            enemy.staggerEndTime = Time.time + skill.StaggerTime;
            enemy.selectedSkillIndex = -1;
            UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
        }

        if (meleeParried)
        {
            if (nearestMeleeDistanceSquared < float.PositiveInfinity)
            {
                transform.rotation = Quaternion.LookRotation(meleeParryDirection);
                LastMeleeParryFacingFrame = Time.frameCount;
            }

            MeleeParried?.Invoke();
            StartParryHitStop();
            Camera mainCamera = Camera.main;
            CameraFollow cameraFollow = mainCamera != null
                ? mainCamera.GetComponent<CameraFollow>()
                : null;
            if (cameraFollow != null && cameraFollow.isActiveAndEnabled)
                cameraFollow.ShakeForMeleeParry();
            else
                Debug.LogWarning("Attack：无法找到启用的主相机跟随组件，近战格挡不会触发镜头抖动。", this);
        }

        return parried;
    }

    private bool IsTargetInAttackRange(Transform target, Vector3 center, float radius)
    {
        int hitCount = GetOverlappingColliders(center, radius);
        bool inRange = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            hitBuffer[i] = null;
            Health health = hit.GetComponentInParent<Health>();
            if (health != null && health.transform == target)
                inRange = true;
        }

        return inRange;
    }

    private void StartParryHitStop()
    {
        if (parryHitStop != null || Time.timeScale <= 0f)
            return;

        if (float.IsNaN(parryHitStopDuration) || float.IsInfinity(parryHitStopDuration) ||
            parryHitStopDuration < 0f)
        {
            Debug.LogError("Attack：近战格挡时停时长必须是非负有限数值。", this);
            return;
        }

        if (parryHitStopDuration == 0f)
            return;

        timeScaleBeforeParry = Time.timeScale;
        CommonTool.SetTimeScale(0f);
        parryTimeScaleVersion = CommonTool.TimeScaleVersion;
        parryHitStop = StartCoroutine(ResumeAfterParry(parryHitStopDuration));
    }

    private IEnumerator ResumeAfterParry(float duration)
    {
        double endTime = Time.realtimeSinceStartupAsDouble + duration;
        do
        {
            yield return null;
        }
        while (Time.realtimeSinceStartupAsDouble < endTime &&
            CommonTool.TimeScaleVersion == parryTimeScaleVersion && Time.timeScale == 0f);

        EndParryHitStop();
    }

    private void EndParryHitStop()
    {
        if (parryHitStop == null)
            return;

        parryHitStop = null;
        if (CommonTool.TimeScaleVersion == parryTimeScaleVersion && Time.timeScale == 0f)
            CommonTool.SetTimeScale(timeScaleBeforeParry);
    }

    private void Awake()
    {
        ownerFaction = GetComponent<CombatFactionMarker>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    public bool TryParry()
    {
        if (!isActiveAndEnabled || ownerFaction == null ||
            ownerFaction.Faction != CombatFaction.Player || IsStaggered || Time.timeScale <= 0f)
            return false;

        if (!TryParrySpecialSkills(hitRadius, hitOffset))
            return false;

        EndMeleeSkillHitbox();
        HideAttackRange();
        return true;
    }

    /// <summary>
    /// 尝试执行一次攻击。
    /// 玩家输入和敌人 AI 都调用这个方法。
    /// </summary>
    public bool TryAttack(Transform target = null)
    {
        if (!isActiveAndEnabled || ownerFaction == null || IsStaggered || Time.timeScale <= 0f ||
            (playerMovement != null && playerMovement.IsDodging))
            return false;

        int skillIndex = configuredSkills != null ? FindReadySkill(target) : -1;
        cfg.Skill attackSkill = configuredSkills != null && skillIndex >= 0
            ? configuredSkills[skillIndex]
            : basicAttackSkill;
        if (configuredSkills == null)
        {
            if (Time.time < nextAttackTime)
                return false;

            if (!TryGetMovementLockDuration(attackSkill, out float duration))
                return false;

            nextAttackTime = Time.time + cooldown;
            attackMovementLockEndTime = Mathf.Max(attackMovementLockEndTime, Time.time + duration);
            CurrentAttackDuration = duration;
        }
        else
        {
            if (skillIndex < 0)
                return false;

            cfg.Skill skill = configuredSkills[skillIndex];
            if (skill.AttackType == 2 && target == null)
            {
                Debug.LogError("Attack：远程技能需要指定目标。", this);
                return false;
            }

            if (!TryGetMovementLockDuration(skill, out float duration))
                return false;

            damage = Mathf.Max(0f, skill.Damage);
            cooldown = Mathf.Max(0.1f, skill.Cooldown);
            hitRadius = Mathf.Max(0.1f, skill.HitRadius);
            hitOffset = Mathf.Max(0f, skill.HitOffset);
            nextSkillAttackTimes[skillIndex] = Time.time + cooldown;
            nextSkillIndex = (skillIndex + 1) % configuredSkills.Length;
            attackMovementLockEndTime = Mathf.Max(attackMovementLockEndTime, Time.time + duration);
            CurrentAttackDuration = duration;
            StopMovingAgent();
            AttackStarted?.Invoke();
            ApplySkillDamage(skill, target);
            return true;
        }

        float? displayDuration = basicAttackSkill != null && basicAttackSkill.AttackType == 1
            ? GetMeleeActiveTime(basicAttackSkill)
            : (float?)null;
        StopMovingAgent();
        AttackStarted?.Invoke();
        ApplyAttack(damage, hitRadius, hitOffset, false, displayDuration: displayDuration);
        return true;
    }

    private bool TryGetMovementLockDuration(cfg.Skill skill, out float duration)
    {
        duration = skill != null
            ? skill.StartupTime + (skill.SkillKind == 2 ? skill.TelegraphTime : 0f) +
                skill.ActiveTime + skill.RecoveryTime
            : cooldown;

        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0f ||
            (skill != null && (skill.StartupTime < 0f || skill.ActiveTime < 0f ||
                skill.RecoveryTime < 0f || (skill.SkillKind == 2 && skill.TelegraphTime < 0f))))
        {
            Debug.LogError("Attack：攻击阶段的时长必须是非负有限数值。", this);
            return false;
        }

        return true;
    }

    private void StopMovingAgent()
    {
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    public void ApplySkillDamage(
        cfg.Skill skill,
        Transform target,
        bool suppressDamage = false,
        bool allowMissingTarget = false)
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
            if (target == null && !allowMissingTarget)
            {
                Debug.LogError("Attack：远程技能需要指定目标。", this);
                return;
            }

            Health targetHealth = target != null ? target.GetComponent<Health>() : null;
            CombatFactionMarker targetFaction = target != null
                ? target.GetComponent<CombatFactionMarker>()
                : null;
            if (ownerFaction == null ||
                (target != null && (targetHealth == null || targetFaction == null ||
                    targetFaction.Faction == ownerFaction.Faction)))
            {
                Debug.LogError("Attack：远程技能目标必须是具有生命值的敌对角色。", this);
                return;
            }

            if (float.IsNaN(skill.HitRadius) || float.IsInfinity(skill.HitRadius))
            {
                Debug.LogError($"Attack：远程技能 {skill.SkillId} 的命中半径必须是有限数值。", this);
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

            if (float.IsNaN(skill.ProjectileTime) || float.IsInfinity(skill.ProjectileTime) ||
                skill.ProjectileTime <= 0f)
            {
                Debug.LogError($"Attack：远程技能 {skill.SkillId} 的弹幕持续时间必须是大于 0 的有限数值。", this);
                return;
            }

            if (!suppressDamage)
            {
                float projectileHeight = Mathf.Max(
                    attackHeight,
                    hitRadius + 0.05f
                );
                Vector3 start = transform.position + Vector3.up * projectileHeight;
                SkillProjectile.Create(start, transform.forward, ownerFaction.Faction, damage,
                    hitRadius, skill.ProjectileSpeed, skill.ProjectileTime, hitLayers,
                    skill.CanBlockProjectile);
            }
        }
        else
        {
            Debug.LogError($"Attack：不支持的攻击类型 {skill.AttackType}。", this);
        }
    }

    private void BeginMeleeSkillHitbox(cfg.Skill skill, bool suppressDamage)
    {
        float activeTime = GetMeleeActiveTime(skill);
        EndMeleeSkillHitbox();
        activeMeleeSkill = skill;
        meleeSkillActiveEndTime = Time.time + activeTime;
        meleeSkillHitTargets.Clear();
        suppressMeleeSkillDamage = suppressDamage;
        ApplyMeleeSkillHitbox(true);

        if (activeTime <= 0f)
            EndMeleeSkillHitbox();
    }

    private float GetMeleeActiveTime(cfg.Skill skill)
    {
        float activeTime = skill.ActiveTime;
        if (float.IsNaN(activeTime) || float.IsInfinity(activeTime))
        {
            Debug.LogError($"Attack：近战技能 {skill.SkillId} 的持续时间必须是有限数值。", this);
            return 0f;
        }

        return Mathf.Max(0f, activeTime);
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
            showRange,
            meleeSkillActiveEndTime - Time.time
        );
    }

    private void EndMeleeSkillHitbox()
    {
        if (activeMeleeSkill != null)
            HideAttackRange();

        activeMeleeSkill = null;
        meleeSkillHitTargets.Clear();
        suppressMeleeSkillDamage = false;
    }

    private void ApplyAttack(
        float attackDamage,
        float attackRadius,
        float attackOffset,
        bool suppressDamage,
        HashSet<Health> hitTargets = null,
        bool showRange = true,
        float? displayDuration = null)
    {
        Vector3 attackCenter = GetAttackCenter(attackOffset);

        if (showRange && !suppressDamage)
            ShowAttackRange(attackCenter, attackRadius, displayDuration);

        if (ownerFaction.Faction == CombatFaction.Player)
            SkillProjectile.BlockInRange(attackCenter, attackRadius, ownerFaction.Faction);

        if (suppressDamage)
            return;

        int hitCount = GetOverlappingColliders(
            attackCenter,
            attackRadius
        );

        // 防止同一目标有多个 Collider 时重复受到伤害
        HashSet<Health> damagedTargets = hitTargets ?? oneShotHitTargets;
        if (hitTargets == null)
            damagedTargets.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            hitBuffer[i] = null;
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

    private int GetOverlappingColliders(Vector3 center, float radius)
    {
        while (true)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                hitBuffer,
                hitLayers,
                QueryTriggerInteraction.Ignore
            );
            if (hitCount < hitBuffer.Length)
                return hitCount;

            System.Array.Clear(hitBuffer, 0, hitCount);
            System.Array.Resize(ref hitBuffer, hitBuffer.Length * 2);
        }
    }

    private Vector3 GetAttackCenter(float attackOffset)
    {
        return transform.position + Vector3.up * attackHeight + transform.forward * attackOffset;
    }

    private int FindReadySkill(Transform target)
    {
        float targetDistance = target != null
            ? GetHorizontalDistance(target)
            : 0f;

        if (preferredAttackType == 2 && target != null)
        {
            int meleeIndex = FindReadySkill(target, targetDistance, 1);
            if (meleeIndex >= 0)
                return meleeIndex;
        }

        if (preferredAttackType != 0)
        {
            int preferredIndex = FindReadySkill(target, targetDistance, preferredAttackType);
            if (preferredIndex >= 0)
                return preferredIndex;
        }

        return FindReadySkill(target, targetDistance, 0);
    }

    private int FindReadySkill(Transform target, float targetDistance, int attackType)
    {
        for (int offset = 0; offset < configuredSkills.Length; offset++)
        {
            int index = (nextSkillIndex + offset) % configuredSkills.Length;
            cfg.Skill skill = configuredSkills[index];
            if (skill == null ||
                (attackType != 0 && skill.AttackType != attackType) ||
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

    private void ShowAttackRange(Vector3 center, float radius, float? displayDuration)
    {
        if (displayDuration.HasValue && displayDuration.Value <= 0f)
        {
            HideAttackRange();
            return;
        }

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

        rangeDisplayUsesScaledTime = displayDuration.HasValue;
        rangeDisplayEndTime = rangeDisplayUsesScaledTime
            ? Time.time + displayDuration.Value
            : Time.unscaledTime + Mathf.Max(0.01f, rangeDisplayDuration);
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
        if (Time.timeScale <= 0f)
            return;

        UpdateMeleeSkillHitbox();

        float targetParryWindowBonus = 0f;
        if (specialSkillTarget != null)
        {
            Attack targetAttack = specialSkillTarget.GetComponent<Attack>();
            if (targetAttack != null)
                targetParryWindowBonus = targetAttack.parryWindowBonus;
        }

        if (IsParryWindowOpenFor(targetParryWindowBonus))
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

        float rangeDisplayTime = rangeDisplayUsesScaledTime ? Time.time : Time.unscaledTime;
        if (rangeLines != null && rangeDisplayTime >= rangeDisplayEndTime)
            HideAttackRange();
    }

    private void HideParryWarning()
    {
        if (parryWarning != null && parryWarning.activeSelf)
            parryWarning.SetActive(false);
    }

    private void OnDisable()
    {
        if (parryHitStop != null)
            StopCoroutine(parryHitStop);
        EndParryHitStop();
        EndSkillExecution();
        attackMovementLockEndTime = 0f;
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