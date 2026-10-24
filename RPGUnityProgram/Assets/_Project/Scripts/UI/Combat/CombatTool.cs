using UnityEngine;

public class CombatTool
{
    // 通过 Tag 动态查找玩家
    public static GameObject GetPlayer()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError(
                $"CombatPanel：没有找到 Tag 为 {"Player"} 的玩家。"
            );
            return null;
        }
        return player;
    }
}
