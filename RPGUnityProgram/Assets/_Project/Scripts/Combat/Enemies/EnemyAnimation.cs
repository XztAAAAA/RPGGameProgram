using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Attack), typeof(Health))]
public sealed class EnemyAnimation : MonoBehaviour
{
    [Tooltip("死亡动画及过渡的播放时间（秒），使用真实时间。")]
    [SerializeField, Min(0.1f)] private float deathAnimationDuration = 2.7f;

    private Animator animator;
    private NavMeshAgent agent;
    private Attack attack;
    private Health health;
    private BehaviorGraphAgent behaviorAgent;

    public float DeathAnimationDuration => deathAnimationDuration;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        attack = GetComponent<Attack>();
        health = GetComponent<Health>();
        behaviorAgent = GetComponent<BehaviorGraphAgent>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("EnemyAnimation：敌人模型缺少 Animator 或动画状态机。", this);
            enabled = false;
            return;
        }

        if (float.IsNaN(deathAnimationDuration) ||
            float.IsInfinity(deathAnimationDuration) || deathAnimationDuration <= 0f)
        {
            Debug.LogError("EnemyAnimation：死亡动画播放时间必须是大于 0 的有限数值。", this);
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        attack.AttackStarted += HandleAttack;
        attack.SkillExecutionStarted += HandleSkillStarted;
        attack.SkillParried += HandleHit;
        health.Damaged += HandleHit;
        health.Died += HandleDeath;

        if (health.IsDead)
            HandleDeath();
    }

    private void OnDisable()
    {
        if (attack != null)
        {
            attack.AttackStarted -= HandleAttack;
            attack.SkillExecutionStarted -= HandleSkillStarted;
            attack.SkillParried -= HandleHit;
        }

        if (health != null)
        {
            health.Damaged -= HandleHit;
            health.Died -= HandleDeath;
        }
    }

    private void LateUpdate()
    {
        float amount = agent.enabled && agent.isOnNavMesh && agent.speed > 0f
            ? Mathf.Clamp01(agent.velocity.magnitude / agent.speed)
            : 0f;
        AnimationTool.SetMovement(animator, amount);
    }

    private void HandleAttack() => AnimationTool.PlayEnemyAttack(animator);

    private void HandleSkillStarted(cfg.Skill skill)
    {
        if (skill.AttackType == 2)
            AnimationTool.PlayEnemySpell(animator);
        else if (skill.AttackType == 1 && skill.SkillKind == 2)
            AnimationTool.PlayHeavyAttack(animator);
        else
            AnimationTool.PlayEnemyAttack(animator);
    }

    private void HandleHit() => AnimationTool.PlayEnemyHit(animator);

    private void HandleDeath()
    {
        if (behaviorAgent != null)
            behaviorAgent.enabled = false;
        if (agent.enabled)
            agent.enabled = false;

        AnimationTool.PlayDeath(animator);
    }
}
