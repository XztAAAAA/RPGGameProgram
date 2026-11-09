using UnityEngine;

public sealed class PlayerConfigInitializer : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int roleId = 1;

    public cfg.Role Role { get; private set; }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        cfg.Role role = TableSystem.Tables.TbRole.GetOrDefault(roleId);
        if (role == null)
        {
            Debug.LogError(
                $"PlayerConfigInitializer：TbRole 中找不到 RoleId = {roleId} 的配置。",
                this
            );
            return;
        }

        cfg.Skill basicAttack =
            TableSystem.Tables.TbSkill.GetOrDefault(role.BasicAttackSkillId);
        if (basicAttack == null)
        {
            Debug.LogError(
                $"PlayerConfigInitializer：RoleId = {role.RoleId} 引用的技能 " +
                $"{role.BasicAttackSkillId} 不存在。",
                this
            );
            return;
        }

        Health health = GetComponent<Health>();
        PlayerMovement movement = GetComponent<PlayerMovement>();
        Attack attack = GetComponent<Attack>();
        if (health == null || movement == null || attack == null)
        {
            Debug.LogError(
                "PlayerConfigInitializer：Player 必须包含 Health、PlayerMovement 和 Attack 组件。",
                this
            );
            return;
        }

        Role = role;
        gameObject.name = role.RoleName;
        health.InitializeFromConfig(role.MaxHealth);
        movement.Configure(role.MoveSpeed, role.RotationSpeed);
        attack.InitializeFromConfig(basicAttack);
    }
}
