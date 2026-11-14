using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Move To Target", story: "[Self] moves towards [Target] using NavMeshAgent until within [AttackRange]", category: "Combat", id: "65c2f7a908cf915e0bb275fc20999aaf")]
public partial class MoveToTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> AttackRange;
    private UnityEngine.AI.NavMeshAgent agent;
    private Attack attack;
    private Vector3 lastDestination;
    private float nextPathUpdate;
    private bool hasDestination;
    protected override Status OnStart()
    {
        GameObject self = Self != null ? Self.Value : null;

        if (self == null)
            return Status.Failure;

        agent = self.GetComponent<UnityEngine.AI.NavMeshAgent>();
        attack = self.GetComponent<Attack>();

        if (agent == null)
            return Status.Failure;

        hasDestination = false;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Time.timeScale <= 0f)
            return Status.Running;

        GameObject self = Self != null ? Self.Value : null;
        GameObject target = Target != null ? Target.Value : null;

        if (self == null || agent == null)
            return Status.Failure;

        if (!agent.enabled ||
            !agent.gameObject.activeInHierarchy ||
            !agent.isOnNavMesh)
        {
            return Status.Running;
        }

        if (attack != null && attack.IsStaggered)
        {
            agent.isStopped = true;
            hasDestination = false;
            return Status.Running;
        }

        // 没有目标时停止，并等待目标重新出现
        if (target == null || !target.activeInHierarchy)
        {
            agent.isStopped = true;
            hasDestination = false;
            return Status.Running;
        }

        Health targetHealth = target.GetComponent<Health>();

        if (targetHealth != null && targetHealth.IsDead)
        {
            agent.isStopped = true;
            hasDestination = false;
            return Status.Running;
        }

        float range = AttackRange != null
            ? Mathf.Max(0f, AttackRange.Value)
            : 1.7f;

        Vector3 direction = target.transform.position
            - self.transform.position;

        direction.y = 0f;

        // 进入攻击距离后，让行为树重新选择攻击分支
        bool targetWithinAttackRange = attack != null &&
            attack.HasConfiguredSkills
                ? attack.HasSkillInRange(target.transform)
                : direction.sqrMagnitude <= range * range;

        if (targetWithinAttackRange)
        {
            agent.isStopped = true;
            hasDestination = false;

            return Status.Failure;
        }

        agent.updateRotation = true;
        agent.isStopped = false;

        bool targetMoved = !hasDestination ||
            (target.transform.position - lastDestination).sqrMagnitude > 0.25f;

        // 不需要每帧重复设置寻路目标
        if (Time.time >= nextPathUpdate &&
            (targetMoved || !agent.hasPath))
        {
            if (agent.SetDestination(target.transform.position))
            {
                lastDestination = target.transform.position;
                hasDestination = true;
            }

            nextPathUpdate = Time.time + 0.2f;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        // 安全停止：避免 Agent 离开 NavMesh 时产生报错
        if (agent != null &&
            agent.enabled &&
            agent.gameObject.activeInHierarchy &&
            agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }
}
