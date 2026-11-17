using UnityEngine;

public static class AnimationTool
{
    private static readonly int PlayerMoveAmount = Animator.StringToHash("MoveAmount");
    private static readonly int PlayerAttack = Animator.StringToHash("Attack");
    private static readonly int PlayerDodge = Animator.StringToHash("Dodge");
    private static readonly int PlayerHit = Animator.StringToHash("Hit");
    private static readonly int PlayerDead = Animator.StringToHash("Dead");

    public static void SetPlayerMovement(Animator animator, float amount)
    {
        animator.SetFloat(PlayerMoveAmount, amount);
    }

    public static void PlayPlayerAttack(Animator animator)
    {
        animator.SetTrigger(PlayerAttack);
    }

    public static void PlayPlayerDodge(Animator animator)
    {
        animator.SetTrigger(PlayerDodge);
    }

    public static void PlayPlayerHit(Animator animator)
    {
        animator.SetTrigger(PlayerHit);
    }

    public static void PlayPlayerDeath(Animator animator)
    {
        animator.SetBool(PlayerDead, true);
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }
}
