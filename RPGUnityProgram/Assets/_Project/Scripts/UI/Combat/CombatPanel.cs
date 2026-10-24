using UnityEngine;

public class CombatPanel : MonoBehaviour
{
    [Header("Player 血条")]
    [SerializeField] private HealthBar healthBar;

    [Header("玩家设置")]
    [SerializeField] private string playerTag = "Player";

    private void Start()
    {
        InitializePlayerHealthBar();
    }

    /// <summary>
    /// 初始化主角血条。
    /// </summary>
    private void InitializePlayerHealthBar()
    {
        // 如果没有在 Inspector 指定，则自动寻找子物体中的 HealthBar
        if (healthBar == null)
            healthBar = GetComponentInChildren<HealthBar>(true);

        if (healthBar == null)
        {
            Debug.LogError(
                "CombatPanel：没有找到 HealthBar，请检查 UI 层级。",
                this
            );
            return;
        }

        GameObject player = CombatTool.GetPlayer();

        // 获取玩家生命值组件
        Health playerHealth = player.GetComponent<Health>();

        if (playerHealth == null)
        {
            Debug.LogError(
                "CombatPanel：Player 没有 Health 组件。",
                player
            );
            return;
        }

        // 将玩家生命值绑定到 UI 血条
        healthBar.Initialize(playerHealth);

        Debug.Log("CombatPanel：主角血条初始化成功。");
    }
}