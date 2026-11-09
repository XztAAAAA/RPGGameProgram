using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Combatant))]
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

    private Combatant owner;
    private float nextAttackTime;

    public float Damage => damage;

    public bool CanAttack =>
        isActiveAndEnabled &&
        Time.time >= nextAttackTime;

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

        damage = Mathf.Max(0f, skill.Damage);
        cooldown = Mathf.Max(0.1f, skill.Cooldown);
        hitRadius = Mathf.Max(0.1f, skill.HitRadius);
        hitOffset = Mathf.Max(0f, skill.HitOffset);
    }

    private void Awake()
    {
        owner = GetComponent<Combatant>();
    }

    /// <summary>
    /// 尝试执行一次攻击。
    /// 玩家输入和敌人 AI 都调用这个方法。
    /// </summary>
    public bool TryAttack()
    {
        if (!CanAttack || owner == null)
            return false;

        nextAttackTime = Time.time + cooldown;

        Vector3 attackCenter =
            transform.position
            + Vector3.up * attackHeight
            + transform.forward * hitOffset;

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

            Combatant targetCombatant =
                targetHealth.GetComponent<Combatant>();

            if (targetCombatant == null)
            {
                targetCombatant =
                    targetHealth.GetComponentInParent<Combatant>();
            }

            // 没有阵营标识的对象不会被这套攻击系统命中
            if (targetCombatant == null)
                continue;

            // 不攻击自己或同阵营对象
            if (targetCombatant == owner ||
                targetCombatant.Faction == owner.Faction)
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