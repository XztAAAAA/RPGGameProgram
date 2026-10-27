using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Combatant))]
public class Attack : MonoBehaviour
{
    [Header("攻击设置")]
    [SerializeField, Min(0f)]
    private float damage = 20f;

    [SerializeField, Min(0.1f)]
    private float attackCooldown = 0.45f;

    [Header("攻击范围")]
    [SerializeField, Min(0.1f)]
    private float attackRadius = 0.9f;

    [SerializeField]
    private float attackForwardOffset = 1f;

    [SerializeField]
    private float attackHeight = 0.1f;

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

        nextAttackTime = Time.time + attackCooldown;

        Vector3 attackCenter =
            transform.position
            + Vector3.up * attackHeight
            + transform.forward * attackForwardOffset;

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
            + transform.forward * attackForwardOffset;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackCenter, attackRadius);
    }
}