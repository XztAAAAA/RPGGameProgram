using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "IsTargetInAttackRange", story: "Checks whether [Self] is within [AttackRange] of [Target]", category: "Combat", id: "87e802529d6a7621f59f5fdc264a20de")]
public partial class IsTargetInAttackRangeAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> AttackRange;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    protected override Status OnStart()
{
    GameObject self = Self != null ? Self.Value : null;
    GameObject target = Target != null ? Target.Value : null;

    if (self == null || target == null)
        return Status.Failure;

    if (!self.activeInHierarchy || !target.activeInHierarchy)
        return Status.Failure;

    Health selfHealth = self.GetComponent<Health>();
    Health targetHealth = target.GetComponent<Health>();

    if (selfHealth != null && selfHealth.IsDead)
        return Status.Failure;

    if (targetHealth != null && targetHealth.IsDead)
        return Status.Failure;

    float range = AttackRange != null
        ? Mathf.Max(0f, AttackRange.Value)
        : 0f;

    Vector3 direction = target.transform.position
        - self.transform.position;

    direction.y = 0f;

    return direction.sqrMagnitude <= range * range
        ? Status.Success
        : Status.Failure;
}

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

