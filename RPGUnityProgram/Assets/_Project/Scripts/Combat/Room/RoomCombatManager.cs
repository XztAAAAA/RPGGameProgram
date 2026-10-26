using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum RoomState
{
    Ready,       // 房间已准备，尚未开始
    InProgress,  // 战斗进行中
    Cleared,     // 房间已清空
    Failed       // 玩家死亡，战斗失败
}

public class RoomCombatManager : MonoBehaviour
{
    [Header("房间设置")]
    [SerializeField] private Transform enemyRoot;

    [SerializeField] private bool startAutomatically = true;

    [SerializeField, Min(0f)]
    private float startDelay = 0.5f;

    [Header("房间事件")]
    [SerializeField]
    private UnityEvent onRoomStarted =
        new UnityEvent();

    [SerializeField]
    public UnityEvent onRoomCleared =
        new UnityEvent();

    [SerializeField]
    public UnityEvent onRoomFailed =
        new UnityEvent();

    private readonly HashSet<Health> aliveEnemies =
        new HashSet<Health>();

    private readonly Dictionary<Health, Action> enemyDeathHandlers =
        new Dictionary<Health, Action>();

    private Health playerHealth;
    private Action playerDeathHandler;

    private PlayerMovement playerMovement;
    private PlayerAttackInput playerAttackInput;

    public RoomState State { get; private set; } = RoomState.Ready;

    public int RemainingEnemies => aliveEnemies.Count;

    private void Awake()
    {
        if (enemyRoot == null)
        {
            Debug.LogError(
                "RoomCombatManager：请指定 Enemy Root。",
                this
            );

            enabled = false;
            return;
        }

        // 玩家保持激活，因此可以在这里初始化玩家引用
        FindPlayerHealth();

        // 不要在这里调用 CollectEnemies()
        // 也不要在这里通过 SetActive(false) 关闭 EnemyRoot
    }

    private void Start()
    {
        if (startAutomatically)
        {
            StartCoroutine(StartRoomAfterDelay());
        }
    }

    private void FindPlayerHealth()
    {
        GameObject player = CombatTool.GetPlayer();

        if (player == null)
        {
            Debug.LogError(
                $"RoomCombatManager：没有找到 Tag 为 {player} 的玩家。",
                this
            );
            return;
        }

        playerHealth = player.GetComponent<Health>();
        playerMovement = player.GetComponent<PlayerMovement>();
        playerAttackInput = player.GetComponent<PlayerAttackInput>();

        if (playerHealth == null)
        {
            Debug.LogError(
                "RoomCombatManager：玩家没有 Health 组件。",
                player
            );
            return;
        }

        playerDeathHandler = HandlePlayerDied;
        playerHealth.Died += playerDeathHandler;
    }

    private void CollectEnemies()
    {
        aliveEnemies.Clear();

        Combatant[] combatants =
            enemyRoot.GetComponentsInChildren<Combatant>(true);

        foreach (Combatant combatant in combatants)
        {
            if (combatant == null ||
                combatant.Faction != CombatFaction.Enemy)
            {
                continue;
            }

            RegisterEnemy(combatant);
        }

        Debug.Log(
            $"房间初始化完成，登记敌人数量：{aliveEnemies.Count}",
            this
        );
    }

    private IEnumerator StartRoomAfterDelay()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        StartRoom();
    }

    public void StartRoom()
    {
        if (State != RoomState.Ready)
            return;

        if (playerHealth == null)
            FindPlayerHealth();

        if (playerHealth == null)
            return;

        if (playerHealth.IsDead)
        {
            FailRoom();
            return;
        }
        Time.timeScale = 1f;

        // 先激活敌人，让其 Awake 和 OnEnable 执行
        enemyRoot.gameObject.SetActive(true);

        // 再初始化敌人登记和死亡事件
        CollectEnemies();

        if (RemainingEnemies == 0)
        {
            Debug.LogError(
                "房间没有找到有效敌人，请检查 EnemyRoot 和 Combatant 阵营。",
                this
            );

            enemyRoot.gameObject.SetActive(false);
            return;
        }

        // 只有准备完成后，才正式开始战斗
        State = RoomState.InProgress;

        Debug.Log(
            $"房间战斗开始，敌人数量：{RemainingEnemies}",
            this
        );

        onRoomStarted?.Invoke();
    }

    /// <summary>
    /// 注册敌人。未来动态生成敌人时可以复用。
    /// </summary>
    public void RegisterEnemy(Combatant combatant)
    {
        if (State == RoomState.Cleared ||
            State == RoomState.Failed)
        {
            return;
        }

        if (combatant == null ||
            combatant.Faction != CombatFaction.Enemy)
        {
            return;
        }

        Health health = combatant.GetComponent<Health>();

        if (health == null)
        {
            Debug.LogError(
                $"{combatant.name} 缺少 Health 组件。",
                combatant
            );
            return;
        }

        if (health.IsDead || !aliveEnemies.Add(health))
            return;

        Action handler = () => HandleEnemyDied(health);

        enemyDeathHandlers.Add(health, handler);
        health.Died += handler;
    }

    private void HandleEnemyDied(Health health)
    {
        // 只有正在战斗且登记过的敌人才能参与结算
        if (State != RoomState.InProgress)
            return;

        if (!aliveEnemies.Remove(health))
            return;

        if (enemyDeathHandlers.TryGetValue(
            health, out Action handler))
        {
            health.Died -= handler;
            enemyDeathHandlers.Remove(health);
        }

        Debug.Log(
            $"敌人被击败，剩余数量：{RemainingEnemies}",
            this
        );

        if (RemainingEnemies == 0)
        {
            ClearRoom();
        }
    }

    private void HandlePlayerDied()
    {
        if (State != RoomState.Ready &&
            State != RoomState.InProgress)
        {
            return;
        }

        FailRoom();
    }

    private void ClearRoom()
    {
        // 防止重复结算
        if (State != RoomState.InProgress)
            return;

        State = RoomState.Cleared;

        Debug.Log("房间已清空！", this);

        // 停止玩家移动和攻击
        StopPlayerControls();

        // 暂停游戏，但保留 UI 的运行与交互
        PauseGameplay();

        // 通知 CombatPanel 等监听者
        onRoomCleared?.Invoke();
    }

    private void FailRoom()
    {
        // 只允许从 Ready 或 InProgress 进入失败状态
        if (State != RoomState.Ready &&
            State != RoomState.InProgress)
        {
            return;
        }

        State = RoomState.Failed;

        Debug.Log("玩家死亡，房间战斗失败！", this);

        // 停止玩家移动和攻击
        StopPlayerControls();

        // 停止敌人 AI 和移动
        if (enemyRoot != null)
            enemyRoot.gameObject.SetActive(false);

        // 暂停游戏
        PauseGameplay();

        // 显示失败面板
        onRoomFailed?.Invoke();
    }

    /// <summary>
    /// 房间结束后停止玩家移动和攻击输入。
    /// </summary>
    private void StopPlayerControls()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerAttackInput != null)
            playerAttackInput.enabled = false;
    }

    /// <summary>
    /// 暂停游戏时间，保留 UI 交互。
    /// </summary>
    private void PauseGameplay()
    {
        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (playerHealth != null &&
            playerDeathHandler != null)
        {
            playerHealth.Died -= playerDeathHandler;
        }

        foreach (var pair in enemyDeathHandlers)
        {
            if (pair.Key != null && pair.Value != null)
            {
                pair.Key.Died -= pair.Value;
            }
        }

        enemyDeathHandlers.Clear();
        aliveEnemies.Clear();
    }
}