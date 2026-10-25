using Unity.Behavior;
using UnityEngine;

public enum CombatFaction
{
    Player,
    Enemy
}

[RequireComponent(typeof(BehaviorGraphAgent))]
public class Combatant : MonoBehaviour
{
    [SerializeField]
    private CombatFaction faction = CombatFaction.Enemy;

    public CombatFaction Faction => faction;

    void Awake()
    {
        Health health = GetComponent<Health>();
        HealthBar healthBar = GetComponentInChildren<HealthBar>(true);

        if (healthBar != null)
            healthBar.Initialize(health);
    }

    void Start()
    {
        if (Faction == CombatFaction.Player) return;
        // 获取当前敌人的行为图组件
        BehaviorGraphAgent behaviorAgent =
            GetComponent<BehaviorGraphAgent>();

        // 查找场景中的玩家
        GameObject player = CombatTool.GetPlayer();

        if (player == null)
        {
            Debug.LogError(
                $"{name}：没有找到 Tag 为 Player 的对象！",
                this
            );
            return;
        }

        // 将玩家动态赋值给行为树黑板中的 Target
        bool success =
            behaviorAgent.SetVariableValue(
                "Target",
                player
            );

        behaviorAgent.SetVariableValue(
                        "AttackRange",
                        1.7f
                    );

        if (success)
        {
            Debug.Log(
                $"{name}：Target 已设置为 {player.name}",
                this
            );
        }
        else
        {
            Debug.LogError(
                $"{name}：设置 Target 失败，请检查行为图中是否存在名为 Target 的 GameObject 变量。",
                this
            );
        }
    }
}