using Unity.Behavior;
using UnityEngine.AI;
using UnityEngine;

[RequireComponent(typeof(BehaviorGraphAgent))]
[RequireComponent(typeof(CombatFactionMarker))]
public class Combatant : MonoBehaviour
{
    private int enemyId;

    public int EnemyId => enemyId;
    public cfg.Enemy EnemyConfig { get; private set; }
    public GameObject Player => player;

    private BehaviorGraphAgent behaviorAgent;
    private GameObject player;
    private float attackRange = 1.7f;

    private void Awake()
    {
        Health health = GetComponent<Health>();
        HealthBar healthBar = GetComponentInChildren<HealthBar>(true);

        if (healthBar != null)
            healthBar.Initialize(health);
    }

    public bool InitializeFromConfig(cfg.Enemy enemy, cfg.Skill[] skills)
    {
        if (enemy == null)
        {
            Debug.LogError("Combatant：敌人配置为空，无法初始化。", this);
            return false;
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
            return false;
        }

        if (!attack.ConfigureSkillPreference(enemy.AttackPreference))
            return false;

        health.InitializeFromConfig(enemy.MaxHealth);
        agent.speed = Mathf.Max(0.1f, enemy.MoveSpeed);
        agent.angularSpeed = Mathf.Max(0f, enemy.RotationSpeed);

        if (skills != null && skills.Length > 0)
        {
            attack.InitializeFromConfig(skills);
            attackRange = 0f;
            foreach (cfg.Skill skill in skills)
            {
                if (skill != null)
                    attackRange = Mathf.Max(attackRange, skill.TriggerRange);
            }
        }

        return true;
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
    }
}