using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(
    name: "Update Enemy Target",
    story: "[Self] updates [Target] using player detection and target-loss rules",
    category: "Combat",
    id: "c79169e9e9c7484191e56a379b0c9d01")]
public partial class UpdateEnemyTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    private float targetOutOfRangeTime = -1f;

    protected override Status OnStart()
    {
        return UpdateTarget();
    }

    protected override Status OnUpdate()
    {
        return UpdateTarget();
    }

    private Status UpdateTarget()
    {
        if (Time.timeScale <= 0f)
            return Status.Running;

        GameObject self = Self != null ? Self.Value : null;
        if (self == null || Target == null)
            return Status.Failure;

        Combatant combatant = self.GetComponent<Combatant>();
        if (combatant == null || combatant.EnemyConfig == null)
            return Status.Failure;

        GameObject player = combatant.Player;
        if (player == null || !player.activeInHierarchy)
        {
            SetTarget(null);
            return Status.Running;
        }

        Health playerHealth = player.GetComponent<Health>();
        if (playerHealth != null && playerHealth.IsDead)
        {
            SetTarget(null);
            return Status.Running;
        }

        float distance = Vector3.Distance(
            self.transform.position,
            player.transform.position
        );
        GameObject currentTarget = Target.Value;

        if (currentTarget == null)
        {
            if (distance <= combatant.EnemyConfig.DetectRange)
                SetTarget(player);

            return Status.Running;
        }

        if (distance <= combatant.EnemyConfig.LoseTargetRange)
        {
            targetOutOfRangeTime = -1f;
            return Status.Running;
        }

        if (targetOutOfRangeTime < 0f)
            targetOutOfRangeTime = Time.time;

        if (Time.time - targetOutOfRangeTime >=
            combatant.EnemyConfig.LoseTargetDelay)
        {
            SetTarget(null);
        }

        return Status.Running;
    }

    private void SetTarget(GameObject target)
    {
        Target.Value = target;
        targetOutOfRangeTime = -1f;
    }
}
