using System;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
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
    private NavMeshPath spacingPath;
    private int orbitDirection;
    private float spacingDistanceFactor;
    private float nextSpacingVariation;
    private bool spacingPathWarningLogged;
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
        if (spacingPath == null)
            spacingPath = new NavMeshPath();
        nextSpacingVariation = 0f;
        spacingPathWarningLogged = false;

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

        if (attack != null && (attack.IsStaggered || attack.IsAttacking))
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

        if (attack != null && attack.TryGetRangedSpacing(out float minDistance, out float maxDistance))
        {
            if (attack.HasReadySkillInRange(target.transform))
            {
                agent.isStopped = true;
                return Status.Failure;
            }

            MoveAtRangedDistance(self.transform, target.transform, minDistance, maxDistance);
            return Status.Running;
        }

        bool targetWithinAttackRange = direction.sqrMagnitude <= range * range;
        if (attack != null && attack.HasConfiguredSkills)
        {
            targetWithinAttackRange = attack.ShouldChaseForMelee(target.transform)
                ? attack.HasReadySkillInRange(target.transform)
                : attack.HasSkillInRange(target.transform);
        }

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

    private void MoveAtRangedDistance(Transform self, Transform target, float minDistance, float maxDistance)
    {
        if (Time.time < nextPathUpdate)
            return;

        nextPathUpdate = Time.time + 0.4f;
        if (Time.time >= nextSpacingVariation)
        {
            orbitDirection = UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
            spacingDistanceFactor = UnityEngine.Random.Range(0.2f, 0.8f);
            nextSpacingVariation = Time.time + UnityEngine.Random.Range(2f, 4f);
        }

        Vector3 away = self.position - target.position;
        away.y = 0f;
        float distance = away.magnitude;
        if (distance > maxDistance)
        {
            agent.updateRotation = true;
            if (!NavMesh.SamplePosition(target.position, out NavMeshHit targetHit, 2f, agent.areaMask) ||
                !agent.CalculatePath(targetHit.position, spacingPath) ||
                spacingPath.status != NavMeshPathStatus.PathComplete ||
                !agent.SetPath(spacingPath))
            {
                agent.isStopped = true;
                LogSpacingPathFailure();
            }
            else
            {
                agent.isStopped = false;
                spacingPathWarningLogged = false;
            }
            return;
        }

        if (distance > 0.001f)
            away /= distance;
        else
        {
            away = self.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : Vector3.right;
        }

        Vector3 tangent = Vector3.Cross(Vector3.up, away) * orbitDirection;
        bool retreat = distance < minDistance;
        float step = Mathf.Max(2f, agent.stoppingDistance + 1f);
        for (int i = 0; i < (retreat ? 3 : 2); i++)
        {
            Vector3 desiredPosition;
            if (retreat)
            {
                Vector3 moveDirection = i == 0
                    ? away
                    : (away + tangent * (i == 1 ? 1f : -1f)).normalized;
                desiredPosition = self.position + moveDirection * step;
            }
            else
            {
                Vector3 orbitOffset = (away + tangent * (i == 0 ? 1f : -1f) *
                    (step / Mathf.Max(distance, 0.001f))).normalized;
                float desiredDistance = Mathf.Lerp(minDistance, maxDistance, spacingDistanceFactor);
                desiredPosition = target.position + orbitOffset * desiredDistance;
                desiredPosition.y = self.position.y;
            }

            if (!NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 1f, agent.areaMask))
                continue;

            Vector3 displacement = hit.position - self.position;
            displacement.y = 0f;
            if (displacement.sqrMagnitude <= (agent.stoppingDistance + 0.05f) *
                (agent.stoppingDistance + 0.05f))
                continue;

            Vector3 candidateOffset = hit.position - target.position;
            candidateOffset.y = 0f;
            float candidateDistance = candidateOffset.magnitude;
            if ((retreat && candidateDistance <= distance + 0.1f) ||
                (!retreat && (candidateDistance < minDistance || candidateDistance > maxDistance)))
                continue;

            if (!agent.CalculatePath(hit.position, spacingPath) ||
                spacingPath.status != NavMeshPathStatus.PathComplete ||
                !agent.SetPath(spacingPath))
                continue;

            if (!retreat && i == 1)
                orbitDirection = -orbitDirection;
            agent.updateRotation = true;
            agent.isStopped = false;
            spacingPathWarningLogged = false;
            return;
        }

        agent.isStopped = true;
        LogSpacingPathFailure();
    }

    private void LogSpacingPathFailure()
    {
        if (spacingPathWarningLogged)
            return;

        Debug.LogWarning("MoveToTargetAction：无法找到可达的远程站位，等待重新寻路。", agent);
        spacingPathWarningLogged = true;
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
