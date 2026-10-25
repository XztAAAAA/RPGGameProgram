using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RoomCombatManager : MonoBehaviour
{
    [Header("房间状态")]
    [SerializeField] private UnityEvent onRoomCleared;

    private readonly HashSet<Health> enemies = new();
    private readonly Dictionary<Health, Action> deathHandlers = new();

    public int RemainingEnemies => enemies.Count;
    public bool IsRoomCleared { get; private set; }

    private void Start()
    {
        // 自动查找当前场景中所有启用的 Combatant
        Combatant[] combatants =
            FindObjectsByType<Combatant>();

        foreach (Combatant combatant in combatants)
        {
            if (combatant == null ||
                combatant.Faction != CombatFaction.Enemy)
            {
                continue;
            }

            RegisterEnemy(combatant);
        }

        if (enemies.Count == 0)
        {
            Debug.LogWarning(
                "RoomCombatManager：没有找到有效的敌人，请检查 Combatant 和阵营设置。",
                this
            );
        }
        else
        {
            Debug.Log(
                $"房间战斗开始，敌人数量：{enemies.Count}",
                this
            );
        }
    }

    /// <summary>
    /// 注册一个敌人。
    /// 以后动态生成敌人时也可以调用此方法。
    /// </summary>
    public void RegisterEnemy(Combatant combatant)
    {
        if (combatant == null ||
            combatant.Faction != CombatFaction.Enemy)
        {
            return;
        }

        Health health = combatant.GetComponent<Health>();

        if (health == null || health.IsDead)
        {
            Debug.LogWarning(
                $"{combatant.name} 缺少 Health 或已经死亡。",
                combatant
            );
            return;
        }

        // 防止重复注册
        if (!enemies.Add(health))
            return;

        Action handler = () => HandleEnemyDied(health);

        deathHandlers[health] = handler;
        health.Died += handler;
    }

    private void HandleEnemyDied(Health health)
    {
        if (!enemies.Remove(health))
            return;

        // 解除事件订阅
        if (deathHandlers.TryGetValue(health, out Action handler))
        {
            health.Died -= handler;
            deathHandlers.Remove(health);
        }

        Debug.Log(
            $"敌人被击败，剩余敌人：{enemies.Count}",
            this
        );

        if (enemies.Count == 0 && !IsRoomCleared)
        {
            ClearRoom();
        }
    }

    private void ClearRoom()
    {
        IsRoomCleared = true;

        Debug.Log("房间已清空！", this);

        // 通知其他系统：本房间战斗结束
        onRoomCleared?.Invoke();
    }

    private void OnDestroy()
    {
        foreach (var pair in deathHandlers)
        {
            if (pair.Key != null)
            {
                pair.Key.Died -= pair.Value;
            }
        }

        deathHandlers.Clear();
        enemies.Clear();
    }
}