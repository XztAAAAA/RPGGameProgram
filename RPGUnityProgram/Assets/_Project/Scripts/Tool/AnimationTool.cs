using UnityEngine;

public static class AnimationTool
{
    private static readonly int MoveAmount = Animator.StringToHash("MoveAmount");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int HeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int Spell = Animator.StringToHash("Spell");
    private static readonly int PlayerBlock = Animator.StringToHash("Block");
    private static readonly int PlayerDodge = Animator.StringToHash("Dodge");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Dead = Animator.StringToHash("Dead");

    public static void SetMovement(Animator animator, float amount)
    {
        animator.SetFloat(MoveAmount, amount);
    }

    public static void PlayAttack(Animator animator)
    {
        animator.SetTrigger(Attack);
    }

    public static void PlayHeavyAttack(Animator animator)
    {
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(Spell);
        animator.SetTrigger(HeavyAttack);
    }

    public static void PlayEnemyAttack(Animator animator)
    {
        animator.ResetTrigger(HeavyAttack);
        animator.ResetTrigger(Spell);
        PlayAttack(animator);
    }

    public static void PlayEnemySpell(Animator animator)
    {
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(HeavyAttack);
        animator.SetTrigger(Spell);
    }

    public static void PlayEnemyHit(Animator animator)
    {
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(HeavyAttack);
        animator.ResetTrigger(Spell);
        PlayHit(animator);
    }

    public static void PlayPlayerBlock(Animator animator)
    {
        animator.ResetTrigger(Attack);
        animator.SetTrigger(PlayerBlock);
    }

    public static void PlayPlayerDodge(Animator animator)
    {
        animator.ResetTrigger(Hit);
        animator.SetTrigger(PlayerDodge);
    }

    public static void PlayHit(Animator animator)
    {
        animator.SetTrigger(Hit);
    }

    public static void PlayDeath(Animator animator)
    {
        animator.SetBool(Dead, true);
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }
}
