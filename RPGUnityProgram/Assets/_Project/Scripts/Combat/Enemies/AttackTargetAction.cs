using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Execute Selected Skill", story: "[Self] executes its selected skill against [Target]", category: "Combat", id: "851ba962adb20d17548f3a27f720f1b3")]
public partial class ExecuteSelectedSkillAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> AttackRange;

    private UnityEngine.AI.NavMeshAgent agent;
    private Attack attack;
    private cfg.Skill skill;
    private float skillStartTime;
    private bool damageApplied;

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

        float range = AttackRange != null
            ? Mathf.Max(0f, AttackRange.Value)
            : 0f;
        if (HorizontalDistance(self.transform, target.transform) > range ||
            !attack.TryBeginSelectedSkill(target.transform, out skill))
        {
            return Status.Failure;
        }

        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;
        agent.updateRotation = false;
        skillStartTime = Time.time;
        damageApplied = false;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        GameObject self = Self != null ? Self.Value : null;
        GameObject target = Target != null ? Target.Value : null;
        if (self == null || target == null || attack == null || agent == null)
            return Status.Failure;

        Health selfHealth = self.GetComponent<Health>();
        Health targetHealth = target.GetComponent<Health>();
        if (!self.activeInHierarchy || !target.activeInHierarchy ||
            (selfHealth != null && selfHealth.IsDead) ||
            (targetHealth != null && targetHealth.IsDead) ||
            !attack.IsSkillInRange(skill, target.transform))
        {
            return Status.Failure;
        }

        Vector3 direction = target.transform.position - self.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            self.transform.rotation = Quaternion.RotateTowards(
                self.transform.rotation,
                targetRotation,
                agent.angularSpeed * Time.deltaTime
            );
        }

        float elapsed = Time.time - skillStartTime;
        float startupEnd = Mathf.Max(0f, skill.StartupTime);
        float telegraphEnd = startupEnd + Mathf.Max(0f, skill.TelegraphTime);
        float activeEnd = telegraphEnd + Mathf.Max(0f, skill.ActiveTime);
        float recoveryEnd = activeEnd + Mathf.Max(0f, skill.RecoveryTime);

        if (elapsed < telegraphEnd)
            return Status.Running;

        if (!damageApplied)
        {
            attack.ApplySkillDamage(skill);
            damageApplied = true;
        }

        return elapsed < recoveryEnd
            ? Status.Running
            : Status.Success;
    }

    protected override void OnEnd()
    {
        if (agent != null)
            agent.updateRotation = true;
    }

    private static float HorizontalDistance(Transform first, Transform second)
    {
        Vector3 direction = second.position - first.position;
        direction.y = 0f;
        return direction.magnitude;
    }
}
