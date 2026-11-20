using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Attack), typeof(Health))]
public sealed class PlayerAnimation : MonoBehaviour
{
    private static readonly int DodgeSpeed = Animator.StringToHash("DodgeSpeed");
    private static readonly int Dodging = Animator.StringToHash("Dodging");
    private static readonly int Dodge = Animator.StringToHash("Dodge");

    private Animator animator;
    private PlayerMovement movement;
    private Attack attack;
    private Health health;
    private AnimationClip attackClip;
    private float dodgeClipLength;
    private int meleeParryFrame = -1;

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

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == "Armature|Roll")
            {
                dodgeClipLength = clip.length;
                break;
            }
        }

        if (dodgeClipLength <= 0f)
        {
            Debug.LogError("PlayerAnimation：动画状态机缺少有效的 Armature|Roll 动画。", this);
            enabled = false;
            return;
        }

        attackClip = AnimationTool.FindClip(animator, "Armature|Sword_Attack");
        animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        attack.AttackStarted += HandleAttack;
        attack.MeleeParried += HandleMeleeParry;
        movement.DodgeStarted += HandleDodge;
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
            attack.MeleeParried -= HandleMeleeParry;
        }
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
        AnimationTool.SetMovement(
            animator,
            movement.isActiveAndEnabled ? movement.MovementAmount : 0f,
            movement.isActiveAndEnabled ? movement.MovementSpeed : 0f);
        bool isDodging = movement.isActiveAndEnabled && movement.IsDodging;
        animator.SetBool(Dodging, isDodging);
        if (!isDodging)
            animator.ResetTrigger(Dodge);
    }

    private void HandleAttack()
    {
        if (meleeParryFrame != Time.frameCount &&
            AnimationTool.SetAttackSpeed(animator, attackClip, attack.CurrentAttackDuration))
            AnimationTool.PlayAttack(animator);
    }

    private void HandleMeleeParry()
    {
        meleeParryFrame = Time.frameCount;
        AnimationTool.PlayPlayerBlock(animator);
    }
    private void HandleDodge()
    {
        animator.SetFloat(DodgeSpeed, dodgeClipLength / movement.DodgeDuration);
        animator.SetBool(Dodging, true);
        AnimationTool.PlayPlayerDodge(animator);
    }
    private void HandleHit()
    {
        if (!movement.IsDodging)
            AnimationTool.PlayHit(animator);
    }

    private void HandleDeath()
    {
        AnimationTool.PlayDeath(animator);
    }
}
