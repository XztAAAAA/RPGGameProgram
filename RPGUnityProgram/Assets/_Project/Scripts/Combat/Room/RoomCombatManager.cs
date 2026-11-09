using System;
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Events;

public enum RoomState
{
    Ready,       // 房间已准备，尚未开始
    InProgress,  // 战斗进行中
    Cleared,     // 房间已清空
    Failed       // 玩家死亡，战斗失败
}

public class RoomCombatManager : MonoSingleton<RoomCombatManager>
{
    [Header("玩家角色配置")]
    [SerializeField, Min(1)] private int playerRoleId = 1;

    [Header("房间设置")]
    [SerializeField] private Transform enemyRoot;

    /// <summary>
    /// 默认关闭，由关卡系统生成敌人后调用 StartRoom。
    /// </summary>
    [SerializeField] private bool startAutomatically = false;

    [SerializeField, Min(0f)]
    private float startDelay = 0.5f;

    [Header("房间事件")]
    [SerializeField]
    private UnityEvent onRoomStarted = new UnityEvent();

    [SerializeField]
    public UnityEvent onRoomCleared = new UnityEvent();

    [SerializeField]
    public UnityEvent onRoomFailed = new UnityEvent();

    private readonly HashSet<Health> aliveEnemies =
        new HashSet<Health>();

    private readonly Dictionary<Health, Action> enemyDeathHandlers =
        new Dictionary<Health, Action>();

    private Health playerHealth;
    private Attack playerAttack;
    private Action playerDeathHandler;

    private PlayerMovement playerMovement;
    private PlayerAttackInput playerAttackInput;

    public RoomState State { get; private set; } = RoomState.Ready;
    public int RemainingEnemies => aliveEnemies.Count;
    public Transform EnemyRoot => enemyRoot;

    private void OnEnable()
    {
        GameEventBus.Subscribe<PlayerMobileAttackRequestedEvent>(
            HandleMobileAttackRequested
        );
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<PlayerMobileAttackRequestedEvent>(
            HandleMobileAttackRequested
        );
    }

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

        // 不在这里初始化敌人；由关卡系统准备敌人后调用 StartRoom。
    }

    private void Start()
    {
        if (startAutomatically)
            StartCoroutine(StartRoomAfterDelay());
    }

    private void FindPlayerHealth()
    {
        GameObject player = CombatTool.GetPlayer();

        if (player == null)
        {
            Debug.LogError(
                "RoomCombatManager：没有找到 Player。",
                this
            );
            return;
        }

        playerHealth = player.GetComponent<Health>();
        playerAttack = player.GetComponent<Attack>();
        playerMovement = player.GetComponent<PlayerMovement>();
        playerAttackInput = player.GetComponent<PlayerAttackInput>();

        InitializePlayerFromRole();

        if (playerHealth == null)
        {
            Debug.LogError(
                "RoomCombatManager：Player 没有 Health 组件。",
                player
            );
            return;
        }

        playerDeathHandler = HandlePlayerDied;
        playerHealth.Died -= playerDeathHandler;
        playerHealth.Died += playerDeathHandler;
    }

    private void InitializePlayerFromRole()
    {
        cfg.Role role = TableSystem.Tables.TbRole.GetOrDefault(playerRoleId);
        if (role == null)
        {
            Debug.LogError(
                $"RoomCombatManager：TbRole 中找不到 RoleId = {playerRoleId} 的配置。",
                this
            );
            return;
        }

        cfg.Skill basicAttack =
            TableSystem.Tables.TbSkill.GetOrDefault(role.BasicAttackSkillId);
        if (basicAttack == null)
        {
            Debug.LogError(
                $"RoomCombatManager：RoleId = {role.RoleId} 引用的技能 " +
                $"{role.BasicAttackSkillId} 不存在。",
                this
            );
            return;
        }

        if (playerHealth == null ||
            playerMovement == null ||
            playerAttack == null)
        {
            Debug.LogError(
                "RoomCombatManager：Player 必须包含 Health、PlayerMovement 和 Attack 组件，" +
                "才能应用角色配置。",
                this
            );
            return;
        }

        playerHealth.InitializeFromConfig(role.MaxHealth);
        playerMovement.InitializeFromConfig(role);
        playerAttack.InitializeFromConfig(basicAttack);

        Debug.Log(
            $"玩家角色配置已应用：{role.RoleName}（ID: {role.RoleId}，" +
            $"基础技能: {basicAttack.SkillName}）。",
            this
        );
    }

    /// <summary>
    /// 接收移动端攻击按钮事件。
    /// 仅在战斗进行中调用玩家已有的 Attack 组件。
    /// </summary>
    private void HandleMobileAttackRequested(
        PlayerMobileAttackRequestedEvent eventData)
    {
        if (State != RoomState.InProgress)
            return;

        if (playerHealth == null || playerHealth.IsDead)
            return;

        if (playerAttack == null)
        {
            Debug.LogError(
                "RoomCombatManager：Player 缺少 Attack 组件，无法响应移动端攻击。",
                this
            );
            return;
        }

        playerAttack.TryAttack();
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
            yield return new WaitForSecondsRealtime(startDelay);

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

        CommonTool.SetTimeScale(1f);

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
            ClearRoom();
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
        if (State != RoomState.InProgress)
            return;

        State = RoomState.Cleared;

        Debug.Log("房间已清空！", this);

        StopPlayerControls();
        PauseGameplay();

        onRoomCleared?.Invoke();
    }

    private void FailRoom()
    {
        if (State != RoomState.Ready &&
            State != RoomState.InProgress)
        {
            return;
        }

        State = RoomState.Failed;

        Debug.Log("玩家死亡，房间战斗失败！", this);

        StopPlayerControls();

        if (enemyRoot != null)
            enemyRoot.gameObject.SetActive(false);

        PauseGameplay();

        onRoomFailed?.Invoke();
    }

    /// <summary>
    /// 房间结束后停止玩家移动和攻击输入。
    /// 移动端攻击输入由 PlayerMovement 发布事件；
    /// 禁用 PlayerMovement 后也会停止读取虚拟按钮。
    /// </summary>
    private void StopPlayerControls()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerAttackInput != null)
            playerAttackInput.enabled = false;
    }

    private void PauseGameplay()
    {
        CommonTool.SetTimeScale(0f);
    }

    private void OnDestroy()
    {
        if (playerHealth != null && playerDeathHandler != null)
            playerHealth.Died -= playerDeathHandler;

        foreach (var pair in enemyDeathHandlers)
        {
            if (pair.Key != null && pair.Value != null)
                pair.Key.Died -= pair.Value;
        }

        enemyDeathHandlers.Clear();
        aliveEnemies.Clear();
    }
}
