using UnityEngine;
using Unity.Behavior;

[RequireComponent(typeof(BehaviorGraphAgent))]
public class EnemyTargetSetup : MonoBehaviour
{
    private void Start()
    {
        // 获取当前敌人的行为图组件
        BehaviorGraphAgent behaviorAgent =
            GetComponent<BehaviorGraphAgent>();

        // 查找场景中的玩家
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

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
            behaviorAgent.SetVariableValue<GameObject>(
                "Target",
                player
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