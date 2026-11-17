using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Attack), typeof(Health))]
public sealed class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement movement;
    private Attack attack;
    private Health health;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        movement = GetComponent<PlayerMovement>();
        attack = GetComponent<Attack>();
        health = GetComponent<Health>();

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("PlayerAnimation：玩家模型缺少 Animator 或动画状态机。", this);
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
        movement.DodgeStarted += HandleDodge;
        health.Damaged += HandleHit;
        health.Died += HandleDeath;

        if (health.IsDead)
            HandleDeath();
    }

    private void OnDisable()
    {
        if (attack != null)
            attack.AttackStarted -= HandleAttack;
        if (movement != null)
            movement.DodgeStarted -= HandleDodge;
        if (health != null)
        {
            health.Damaged -= HandleHit;
            health.Died -= HandleDeath;
        }
    }

    private void LateUpdate()
    {
        AnimationTool.SetPlayerMovement(
            animator, movement.isActiveAndEnabled ? movement.MovementAmount : 0f);
    }

    private void HandleAttack() => AnimationTool.PlayPlayerAttack(animator);
    private void HandleDodge() => AnimationTool.PlayPlayerDodge(animator);
    private void HandleHit() => AnimationTool.PlayPlayerHit(animator);

    private void HandleDeath()
    {
        AnimationTool.PlayPlayerDeath(animator);
    }
}
