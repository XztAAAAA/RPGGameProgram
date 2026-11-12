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
    private LineRenderer[] rangeLines;
    private Material rangeMaterial;
    private float rangeDisplayEndTime;
    private bool rangeDisplayFailed;

    public float Damage => damage;

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
            damage = Mathf.Max(0f, skill.Damage);
            cooldown = Mathf.Max(0.1f, skill.Cooldown);
            hitRadius = Mathf.Max(0.1f, skill.HitRadius);
            hitOffset = Mathf.Max(0f, skill.HitOffset);
            nextSkillAttackTimes[skillIndex] = Time.time + cooldown;
            nextSkillIndex = (skillIndex + 1) % configuredSkills.Length;
        }

        Vector3 attackCenter =
            transform.position
            + Vector3.up * attackHeight
            + transform.forward * hitOffset;

        ShowAttackRange(attackCenter, hitRadius);

        Collider[] hits = Physics.OverlapSphere(
            attackCenter,
            hitRadius,
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
                targetHealth.TakeDamage(damage);
            }
        }

        return true;
    }

    private int FindReadySkill(Transform target)
    {
        Vector3 targetOffset = target != null
            ? target.position - transform.position
            : Vector3.zero;
        targetOffset.y = 0f;
        float targetDistance = targetOffset.magnitude;

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