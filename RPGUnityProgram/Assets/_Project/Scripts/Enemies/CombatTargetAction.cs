using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Combat Target", story: "[Self] chases [Target] and attacks when in range", category: "Combat", id: "7302b5edd9e9298d81a1abc497ec0684")]
public partial class CombatTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    private const float AttackRange = 1.7f;
    private const float TurnSpeed = 720f;

    private NavMeshAgent agent;
    private Attack attack;
    private Health ownHealth;
    private Health targetHealth;

    private Vector3 lastDestination;
    private bool hasDestination;

    protected override Status OnStart()
    {
        if (Self == null || Self.Value == null)
            return Status.Failure;

        GameObject self = Self.Value;

        agent = self.GetComponent<NavMeshAgent>();
        attack = self.GetComponent<Attack>();
        ownHealth = self.GetComponent<Health>();

        if (agent == null || attack == null)
        {
            Debug.LogError(
                $"{self.name} 缺少 NavMeshAgent 或 Attack 组件。",
                self
            );
            return Status.Failure;
        }

        // 由本节点控制敌人的朝向
        agent.updateRotation = false;
        agent.isStopped = false;

        hasDestination = false;
        targetHealth = null;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        GameObject self = Self != null ? Self.Value : null;
        GameObject target = Target != null ? Target.Value : null;

        if (self == null || agent == null || attack == null)
            return Status.Failure;

        if (ownHealth != null && ownHealth.IsDead)
        {
            agent.isStopped = true;
            return Status.Success;
        }

        // 目标尚未设置时，保持等待
        if (target == null)
        {
            agent.isStopped = true;
            return Status.Running;
        }

        if (targetHealth == null ||
            targetHealth.gameObject != target)
        {
            targetHealth = target.GetComponent<Health>();
        }

        // 目标死亡后停止攻击，等待后续目标更新
        if (targetHealth != null && targetHealth.IsDead)
        {
            agent.isStopped = true;
            hasDestination = false;
            return Status.Running;
        }

        // 计算水平距离，不考虑高度差
        Vector3 direction =
            target.transform.position - self.transform.position;

        direction.y = 0f;

        float distanceSqr = direction.sqrMagnitude;

        // 转向玩家
        if (distanceSqr > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            self.transform.rotation = Quaternion.RotateTowards(
                self.transform.rotation,
                targetRotation,
                TurnSpeed * Time.deltaTime
            );
        }

        if (!agent.isOnNavMesh)
        {
            agent.isStopped = true;
            return Status.Running;
        }

        // 进入攻击距离
        if (distanceSqr <= AttackRange * AttackRange)
        {
            agent.isStopped = true;
            hasDestination = false;

            // 攻击冷却由 Attack 组件管理
            attack.TryAttack();
        }
        else
        {
            // 距离太远，继续追击
            agent.isStopped = false;

            Vector3 destination = target.transform.position;

            // 目标移动超过 0.5 单位时才重新设置目的地
            if (!hasDestination ||
                !agent.hasPath ||
                (destination - lastDestination).sqrMagnitude > 0.25f)
            {
                if (agent.SetDestination(destination))
                {
                    lastDestination = destination;
                    hasDestination = true;
                }
            }
        }

        // 持续运行，重复检查距离和攻击冷却
        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (agent != null)
            agent.isStopped = true;
    }
}

