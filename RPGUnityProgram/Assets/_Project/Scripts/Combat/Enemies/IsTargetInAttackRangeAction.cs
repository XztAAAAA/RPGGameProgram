using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Select Available Skill", story: "[Self] selects an available skill for [Target] within [AttackRange]", category: "Combat", id: "87e802529d6a7621f59f5fdc264a20de")]
public partial class SelectAvailableSkillAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> AttackRange;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    protected override Status OnStart()
    {
        return SelectSkill();
    }

    protected override Status OnUpdate()
    {
        return SelectSkill();
    }

    private Status SelectSkill()
    {
        if (Time.timeScale <= 0f)
            return Status.Running;

        GameObject self = Self != null ? Self.Value : null;
        GameObject target = Target != null ? Target.Value : null;

        if (self == null || target == null ||
            !self.activeInHierarchy || !target.activeInHierarchy)
        {
            return Status.Failure;
        }

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
        Vector3 direction = target.transform.position - self.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > range * range)
            return Status.Failure;

        Attack attack = self.GetComponent<Attack>();
        return attack != null && attack.SelectAvailableSkill(target.transform)
            ? Status.Success
            : Status.Failure;
    }

    protected override void OnEnd()
    {
    }
}
