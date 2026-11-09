
using System;
using System.Collections;
using Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class LevelManager : PersistentMonoSingleton<LevelManager>
{
    private const string EnemyPrefabResourcesPath = "Prefabs/Role";

    [Header("关卡设置")]
    [SerializeField] private int firstLevelId = 1;

    private GameObject[] loadedEnemyPrefabs;

    public int CurrentLevelId { get; private set; }

    private bool isLoadingScene;
    private Coroutine prepareCoroutine;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (CurrentLevelId <= 0)
            CurrentLevelId = firstLevelId;

        // 支持直接打开战斗场景进行调试。
        // 正常游戏流程仍应从 MainMenu 进入。
        Scene activeScene = SceneManager.GetActiveScene();

        if (TryGetLevelData(
                CurrentLevelId,
                out _,
                out _,
                out _,
                out string sceneName))
        {
            if (string.Equals(
                    activeScene.name,
                    sceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                prepareCoroutine =
                    StartCoroutine(PrepareLevelAfterLoad(activeScene));
            }
        }
    }

    /// <summary>
    /// 从主菜单开始新的一轮游戏。
    /// </summary>
    public void StartNewRun()
    {
        StartNewRun(firstLevelId);
    }

    public void StartNewRun(int levelId)
    {
        LoadLevelById(levelId);
    }

    /// <summary>
    /// 当前关卡通关后进入配置指定的下一关。
    /// </summary>
    public void GoToNextLevel()
    {
        if (!TryGetLevelData(
                CurrentLevelId,
                out string currentName,
                out int nextLevelId,
                out _,
                out _))
        {
            return;
        }

        if (nextLevelId <= 0)
        {
            Debug.Log($"全部关卡完成！最后一关：{currentName}", this);
            return;
        }

        Debug.Log($"从 {CurrentLevelId} 前往下一关 {nextLevelId}", this);
        LoadLevelById(nextLevelId);
    }

    /// <summary>
    /// 重新挑战当前关卡，不重置整个闯关进度。
    /// </summary>
    public void RetryCurrentLevel()
    {
        if (CurrentLevelId <= 0)
            CurrentLevelId = firstLevelId;

        LoadLevelById(CurrentLevelId);
    }

    /// <summary>
    /// 返回主菜单。
    /// </summary>
    public void ReturnToMainMenu(string sceneName = "MainMenu")
    {
        CommonTool.SetTimeScale(1f);

        if (isLoadingScene)
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"无法加载主菜单场景：{sceneName}，请检查 Build Profiles 的 Scene List。",
                this
            );
            return;
        }

        isLoadingScene = true;

        try
        {
            AsyncOperation operation =
                SceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Single
                );

            if (operation == null)
                isLoadingScene = false;
        }
        catch (Exception exception)
        {
            isLoadingScene = false;
            Debug.LogException(exception, this);
        }
    }

    private void LoadLevelById(int levelId)
    {
        if (isLoadingScene)
        {
            Debug.LogWarning("场景正在加载，请勿重复请求。", this);
            return;
        }

        if (!TryGetLevelData(
                levelId,
                out string levelName,
                out _,
                out _,
                out string sceneName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(
                $"关卡 {levelId} 没有配置 SceneName。",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"场景 {sceneName} 不能加载。请检查场景名，以及 Build Profiles 的 Scene List。",
                this
            );
            return;
        }

        int previousLevelId = CurrentLevelId;

        CurrentLevelId = levelId;
        CommonTool.SetTimeScale(1f);
        isLoadingScene = true;

        try
        {
            AsyncOperation operation =
                SceneManager.LoadSceneAsync( 
                    sceneName,
                    LoadSceneMode.Single
                );

            if (operation == null)
            {
                CurrentLevelId = previousLevelId;
                isLoadingScene = false;
                return;
            }

            Debug.Log($"正在加载第 {levelId} 关：{levelName} / {sceneName}", this);
        }
        catch (Exception exception)
        {
            CurrentLevelId = previousLevelId;
            isLoadingScene = false;
            Debug.LogException(exception, this);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        isLoadingScene = false;

        // 从胜利/失败状态切换场景时恢复游戏时间。
        CommonTool.SetTimeScale(1f);

        if (CurrentLevelId <= 0)
            return;

        if (!TryGetLevelData(
                CurrentLevelId,
                out _,
                out _,
                out int spawnGroupId,
                out string expectedSceneName))
        {
            return;
        }

        // 主菜单等非关卡场景不需要生成敌人。
        if (!string.Equals(
                scene.name,
                expectedSceneName,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (prepareCoroutine != null)
            StopCoroutine(prepareCoroutine);

        prepareCoroutine = StartCoroutine(PrepareLevelAfterLoad(scene));
    }

    private IEnumerator PrepareLevelAfterLoad(Scene scene)
    {
        // sceneLoaded 在场景对象的 OnEnable 之后、Start 之前触发。
        // 等待一帧，让场景中的基础初始化完成。
        yield return null;

        if (!scene.isLoaded ||
            SceneManager.GetActiveScene() != scene)
        {
            yield break;
        }

        PrepareLoadedLevel(scene);
    }

    private void PrepareLoadedLevel(Scene scene)
    {
        try
        {
            if (!TryGetLevelData(
                    CurrentLevelId,
                    out string levelName,
                    out _,
                    out int spawnGroupId,
                    out string expectedSceneName))
            {
                return;
            }

            if (!string.Equals(
                    scene.name,
                    expectedSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RoomCombatManager roomManager =
                FindFirstObjectByType<RoomCombatManager>();

            if (roomManager == null)
            {
                Debug.LogError(
                    $"场景 {scene.name} 没有 RoomCombatManager。",
                    this
                );
                return;
            }

            Transform enemyRoot = roomManager.EnemyRoot;

            if (enemyRoot == null)
            {
                Debug.LogError(
                    "RoomCombatManager 没有设置 EnemyRoot。",
                    this
                );
                return;
            }

            // 本方案要求 EnemyRoot 初始为空且非激活。
            // 不自动销毁手动摆放的敌人，以免误删场景内容。
            if (enemyRoot.childCount > 0)
            {
                Debug.LogError(
                    "EnemyRoot 下仍有手动摆放的对象。请先将敌人做成 Prefab，" +
                    "然后清空 EnemyRoot，再由 Luban 配置生成敌人。",
                    enemyRoot
                );
                return;
            }

            enemyRoot.gameObject.SetActive(false);

            int spawnedCount =
                SpawnEnemiesForGroup(spawnGroupId, enemyRoot);

            if (spawnedCount <= 0)
            {
                Debug.LogError(
                    $"关卡 {CurrentLevelId}（{levelName}）未生成任何敌人，取消开始战斗。",
                    this
                );
                return;
            }

            // 由房间管理器激活 EnemyRoot、登记敌人并开始战斗。
            roomManager.StartRoom();

            Debug.Log(
                $"第 {CurrentLevelId} 关初始化完成，生成敌人 {spawnedCount} 个。",
                this
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"关卡初始化失败：{exception}",
                this
            );
        }
    }

    private int SpawnEnemiesForGroup(
        int spawnGroupId,
        Transform enemyRoot)
    {
        var tables = TableSystem.Tables;

        int spawnedCount = 0;
        int matchedRows = 0;

        foreach (var spawn in tables.TbLevelSpawn.DataList)
        {
            if (spawn.SpawnGroupId != spawnGroupId)
                continue;

            matchedRows++;

            GameObject prefab = FindEnemyPrefab(spawn.EnemyKey);

            if (prefab == null)
            {
                Debug.LogError(
                    $"Assets/Resources/{EnemyPrefabResourcesPath} 中找不到名为 {spawn.EnemyKey} 的敌人 Prefab。",
                    this
                );
                continue;
            }

            if (spawn.Count <= 0)
            {
                Debug.LogWarning(
                    $"生成组 {spawnGroupId} 的 {spawn.EnemyKey} 数量小于等于 0。",
                    this
                );
                continue;
            }

            Combatant prefabCombatant = prefab.GetComponent<Combatant>();
            if (prefabCombatant == null)
            {
                Debug.LogError(
                    $"敌人 Prefab {prefab.name} 缺少 Combatant 组件，无法读取敌人配置 ID。",
                    prefab
                );
                continue;
            }

            cfg.Enemy enemyConfig = ResolveEnemyConfig(
                tables,
                prefabCombatant,
                prefab,
                spawn.EnemyKey
            );
            if (enemyConfig == null)
                continue;

            cfg.Skill primarySkill = null;
            if (enemyConfig.SkillIds.Length == 0)
            {
                Debug.LogError(
                    $"敌人 {enemyConfig.EnemyName}（ID: {enemyConfig.EnemyId}）没有配置技能，无法初始化攻击属性。",
                    this
                );
                continue;
            }
            else
            {
                if (enemyConfig.SkillIds.Length > 1)
                {
                    Debug.LogWarning(
                        $"敌人 {enemyConfig.EnemyName} 配置了多个技能；当前 Attack 组件只支持一个技能，" +
                        $"本次使用第一个技能 ID {enemyConfig.SkillIds[0]}。",
                        this
                    );
                }

                primarySkill = tables.TbSkill.GetOrDefault(enemyConfig.SkillIds[0]);
                if (primarySkill == null)
                {
                    Debug.LogError(
                        $"敌人 {enemyConfig.EnemyName} 引用的技能 ID " +
                        $"{enemyConfig.SkillIds[0]} 不存在于 TbSkill，无法初始化攻击属性。",
                        this
                    );
                    continue;
                }
            }

            for (int i = 0; i < spawn.Count; i++)
            {
                Vector3 position =
                    GetSpawnPosition(enemyRoot, spawn.SpawnRadius);

                GameObject enemy = Instantiate(
                    prefab,
                    position,
                    prefab.transform.rotation,
                    enemyRoot
                );

                Health health = enemy.GetComponent<Health>();
                Attack attack = enemy.GetComponent<Attack>();
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                Combatant combatant = enemy.GetComponent<Combatant>();

                if (health == null || attack == null || agent == null || combatant == null)
                {
                    Debug.LogError(
                        $"敌人 Prefab {prefab.name} 必须包含 Health、Attack、NavMeshAgent 和 Combatant。",
                        prefab
                    );

                    Destroy(enemy);
                    continue;
                }

                // EnemyRoot 当前为非激活状态，因此可以先设置属性，再激活敌人。
                combatant.InitializeFromConfig(enemyConfig, primarySkill);
                // 保留关卡表对生命和移速的逐关覆盖。
                health.InitializeFromConfig(spawn.MaxHealth);
                agent.speed = Mathf.Max(0.1f, spawn.MoveSpeed);

                spawnedCount++;
            }
        }

        if (matchedRows == 0)
        {
            Debug.LogError(
                $"TbLevelSpawn 中找不到 SpawnGroupId = {spawnGroupId} 的记录。",
                this
            );
        }

        return spawnedCount;
    }

    private cfg.Enemy ResolveEnemyConfig(
        cfg.Tables tables,
        Combatant prefabCombatant,
        GameObject prefab,
        string spawnKey)
    {
        cfg.Enemy enemy = prefabCombatant.EnemyId > 0
            ? tables.TbEnemy.GetOrDefault(prefabCombatant.EnemyId)
            : null;

        if (enemy == null && prefabCombatant.EnemyId <= 0)
        {
            foreach (cfg.Enemy candidate in tables.TbEnemy.DataList)
            {
                if (!string.Equals(
                        candidate.PrefabKey,
                        prefab.name,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        candidate.PrefabKey,
                        spawnKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (enemy != null)
                {
                    Debug.LogError(
                        $"敌人 Prefab {prefab.name} 对应多个 TbEnemy 配置，请在 Combatant 上指定 EnemyId。",
                        prefab
                    );
                    return null;
                }

                enemy = candidate;
            }
        }

        if (enemy == null)
        {
            Debug.LogError(
                $"无法为敌人 Prefab {prefab.name} 找到 TbEnemy 配置。" +
                (prefabCombatant.EnemyId > 0
                    ? $" Combatant 配置的 EnemyId={prefabCombatant.EnemyId} 不存在。"
                    : " 请在 Combatant 上设置 EnemyId，或确保 PrefabKey 与 Prefab 名称一致。"),
                prefab
            );
            return null;
        }

        if (!string.Equals(
                enemy.PrefabKey,
                prefab.name,
                StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning(
                $"敌人表 PrefabKey={enemy.PrefabKey} 与实际 Prefab={prefab.name} 不一致；" +
                $"继续使用 Combatant EnemyId={enemy.EnemyId} 配置（关卡生成键：{spawnKey}）。",
                prefab
            );
        }

        return enemy;
    }

    private Vector3 GetSpawnPosition(
        Transform enemyRoot,
        float spawnRadius)
    {
        Vector2 offset =
            UnityEngine.Random.insideUnitCircle *
            Mathf.Max(0f, spawnRadius);

        Vector3 candidate = enemyRoot.position +
            new Vector3(offset.x, 0f, offset.y);

        // 将生成位置吸附到当前 NavMesh。
        if (NavMesh.SamplePosition(
                candidate,
                out NavMeshHit hit,
                3f,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

        // 如果随机位置不在 NavMesh 附近，尝试使用中心位置。
        if (NavMesh.SamplePosition(
                enemyRoot.position,
                out hit,
                5f,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

        Debug.LogWarning(
            "未找到合适的 NavMesh 生成位置，请检查地图烘焙。",
            this
        );

        return candidate;
    }

    private GameObject FindEnemyPrefab(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        loadedEnemyPrefabs ??= Resources.LoadAll<GameObject>(
            EnemyPrefabResourcesPath);

        foreach (GameObject prefab in loadedEnemyPrefabs)
        {
            if (prefab != null &&
                string.Equals(
                    prefab.name,
                    key.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private bool TryGetLevelData(
        int levelId,
        out string levelName,
        out int nextLevelId,
        out int spawnGroupId,
        out string sceneName)
    {
        levelName = string.Empty;
        nextLevelId = 0;
        spawnGroupId = 0;
        sceneName = string.Empty;

        try
        {
            var level = TableSystem.Tables.TbLevel.Get(levelId);

            if (level == null)
            {
                Debug.LogError($"Luban 中不存在关卡 ID：{levelId}", this);
                return false;
            }

            levelName = level.Name;
            nextLevelId = level.NextLevelId;
            spawnGroupId = level.SpawnGroupId;
            sceneName = level.SceneName;

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"读取关卡 ID {levelId} 失败，请检查 Luban 表和 JSON 是否一致。\n{exception}",
                this
            );

            return false;
        }
    }

    public bool IsLastLevel()
    {
        if (CurrentLevelId <= 0)
            return false;

        return TryGetLevelData(
                CurrentLevelId,
                out _,
                out int nextLevelId,
                out _,
                out _) &&
            nextLevelId <= 0;
    }
}
