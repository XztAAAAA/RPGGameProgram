using Unity.Behavior;
using UnityEngine.AI;
using UnityEngine;

[RequireComponent(typeof(BehaviorGraphAgent))]
[RequireComponent(typeof(CombatFactionMarker))]
public class Combatant : MonoBehaviour
{
    [SerializeField, Min(0)]
    private int enemyId;

    public int EnemyId => enemyId;
    public cfg.Enemy EnemyConfig { get; private set; }

    private BehaviorGraphAgent behaviorAgent;
    private GameObject player;
    private GameObject target;
    private float targetOutOfRangeTime = -1f;
    private float attackRange = 1.7f;

    private void Awake()
    {
        Health health = GetComponent<Health>();
        HealthBar healthBar = GetComponentInChildren<HealthBar>(true);

        if (healthBar != null)
            healthBar.Initialize(health);
    }

    public void InitializeFromConfig(cfg.Enemy enemy, cfg.Skill primarySkill)
    {
        if (enemy == null)
        {
            Debug.LogError("Combatant：敌人配置为空，无法初始化。", this);
            return;
        }

        enemyId = enemy.EnemyId;
        EnemyConfig = enemy;
        gameObject.name = enemy.EnemyName;

        Health health = GetComponent<Health>();
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        Attack attack = GetComponent<Attack>();

        if (health == null || agent == null || attack == null)
        {
            Debug.LogError(
                $"{name}：敌人 Prefab 必须包含 Health、NavMeshAgent 和 Attack 组件。",
                this
            );
            return;
        }

        health.InitializeFromConfig(enemy.MaxHealth);
        agent.speed = Mathf.Max(0.1f, enemy.MoveSpeed);
        agent.angularSpeed = Mathf.Max(0f, enemy.RotationSpeed);

        if (primarySkill != null)
        {
            attack.InitializeFromConfig(primarySkill);
            attackRange = Mathf.Max(0f, primarySkill.TriggerRange);
        }
    }

    private void Start()
    {
        behaviorAgent = GetComponent<BehaviorGraphAgent>();
        player = CombatTool.GetPlayer();

        if (player == null)
        {
            Debug.LogError(
                $"{name}：没有找到 Tag 为 Player 的对象！",
                this
            );
            return;
        }

        behaviorAgent.SetVariableValue("AttackRange", attackRange);

        bool canDetectPlayer = EnemyConfig == null ||
            Vector3.Distance(transform.position, player.transform.position) <=
            EnemyConfig.DetectRange;
        UpdateTarget(canDetectPlayer ? player : null);
    }

    private void Update()
    {
        CombatFactionMarker factionMarker =
            GetComponent<CombatFactionMarker>();
        if (factionMarker == null ||
            factionMarker.Faction != CombatFaction.Enemy ||
            EnemyConfig == null)
        {
            return;
        }

        if (player == null || behaviorAgent == null)
            return;

        Health playerHealth = player.GetComponent<Health>();
        if (!player.activeInHierarchy ||
            (playerHealth != null && playerHealth.IsDead))
        {
            UpdateTarget(null);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.transform.position);

        if (target == null)
        {
            if (distance <= EnemyConfig.DetectRange)
                UpdateTarget(player);

            return;
        }

        if (distance <= EnemyConfig.LoseTargetRange)
        {
            targetOutOfRangeTime = -1f;
            return;
        }

        if (targetOutOfRangeTime < 0f)
            targetOutOfRangeTime = Time.time;

        if (Time.time - targetOutOfRangeTime >= EnemyConfig.LoseTargetDelay)
            UpdateTarget(null);
    }

    private void UpdateTarget(GameObject newTarget)
    {
        target = newTarget;
        targetOutOfRangeTime = -1f;

        if (behaviorAgent != null &&
            !behaviorAgent.SetVariableValue("Target", target))
        {
            Debug.LogError(
                $"{name}：设置行为图 Target 失败，请检查 Blackboard 是否包含 Target 变量。",
                this
            );
        }
    }
}