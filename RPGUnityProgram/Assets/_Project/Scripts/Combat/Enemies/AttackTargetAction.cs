using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Attack Target", story: "[Self] faces [Target] and attacks while Target is within [AttackRange]", category: "Combat", id: "851ba962adb20d17548f3a27f720f1b3")]
public partial class AttackTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> AttackRange;
    private UnityEngine.AI.NavMeshAgent agent;
    private Attack attack;

    protected override Status OnStart()
    {
        GameObject self = Self != null ? Self.Value : null;
        GameObject target = Target != null ? Target.Value : null;

        if (self == null || target == null)
            return Status.Failure;

        agent = self.GetComponent<UnityEngine.AI.NavMeshAgent>();
        attack = self.GetComponent<Attack>();

        if (agent == null || attack == null)
            return Status.Failure;

        agent.updateRotation = false;

        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        GameObject self = Self != null ? Self.Value : null;
        GameObject target = Target != null ? Target.Value : null;

        if (self == null || target == null || agent == null || attack == null)
            return Status.Failure;

        Health selfHealth = self.GetComponent<Health>();
        Health targetHealth = target.GetComponent<Health>();

        if ((selfHealth != null && selfHealth.IsDead) ||
            (targetHealth != null && targetHealth.IsDead))
        {
            return Status.Failure;
        }

        float range = AttackRange != null
            ? Mathf.Max(0f, AttackRange.Value)
            : 0f;

        Vector3 direction = target.transform.position
            - self.transform.position;

        direction.y = 0f;

        // 一旦超出攻击距离，退出攻击分支，交给追击分支
        if (direction.sqrMagnitude > range * range)
            return Status.Failure;

        if (!agent.enabled ||
            !agent.gameObject.activeInHierarchy ||
            !agent.isOnNavMesh)
        {
            return Status.Failure;
        }

        agent.isStopped = true;

        // 面向玩家
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction);

            self.transform.rotation = Quaternion.RotateTowards(
                self.transform.rotation,
                targetRotation,
                agent.angularSpeed * Time.deltaTime
            );
        }

        // 攻击冷却仍由原来的 Attack 组件管理
        attack.TryAttack(target.transform);

        return Status.Running;
    }

    protected override void OnEnd()
    {
        // 不在这里强制停止或重启 Agent。
        // 切换到追击分支时，由 MoveToTarget 负责恢复移动。
    }
}
