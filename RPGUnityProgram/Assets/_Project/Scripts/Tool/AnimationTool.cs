using UnityEngine;

public static class AnimationTool
{
    // Estimated world speeds for 1x playback of the shared locomotion clips.
    private const float WalkReferenceSpeed = 2f;
    private const float JogReferenceSpeed = 4f;

    private static readonly int MoveAmount = Animator.StringToHash("MoveAmount");
    private static readonly int WalkSpeed = Animator.StringToHash("WalkSpeed");
    private static readonly int JogSpeed = Animator.StringToHash("JogSpeed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");
    private static readonly int HeavyAttack = Animator.StringToHash("HeavyAttack");
    private static readonly int Spell = Animator.StringToHash("Spell");
    private static readonly int PlayerBlock = Animator.StringToHash("Block");
    private static readonly int PlayerDodge = Animator.StringToHash("Dodge");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Dead = Animator.StringToHash("Dead");

    public static void SetMovement(Animator animator, float amount, float speed)
    {
        animator.SetFloat(MoveAmount, amount);
        animator.SetFloat(WalkSpeed, speed / WalkReferenceSpeed);
        animator.SetFloat(JogSpeed, speed / JogReferenceSpeed);
    }

    public static AnimationClip FindClip(Animator animator, string name)
    {
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == name)
                return clip;
        }

        Debug.LogError($"AnimationTool：动画状态机缺少 {name} 动画。", animator);
        return null;
    }

    public static bool SetAttackSpeed(Animator animator, AnimationClip clip, float duration)
    {
        if (clip == null)
            return false;

        if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f ||
            float.IsNaN(clip.length) || float.IsInfinity(clip.length) || clip.length <= 0f)
        {
            Debug.LogError($"AnimationTool：无法根据时长 {duration} 播放攻击动画 {clip.name}。", animator);
            return false;
        }

        float speed = clip.length / duration;
        if (float.IsInfinity(speed) || speed <= 0f)
        {
            Debug.LogError($"AnimationTool：攻击动画 {clip.name} 的播放速度无效。", animator);
            return false;
        }

        animator.SetFloat(AttackSpeed, speed);
        return true;
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
