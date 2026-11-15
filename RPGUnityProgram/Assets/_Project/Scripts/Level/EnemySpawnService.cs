using System;
using UnityEngine;
using UnityEngine.AI;

public sealed class EnemySpawnService
{
    private const string EnemyPrefabResourcesPath = "Prefabs/Role";

    private GameObject[] loadedEnemyPrefabs;

    public int SpawnGroup(
        cfg.Tables tables,
        int spawnGroupId,
        Transform playerRoot,
        Transform enemyRoot,
        UnityEngine.Object logContext)
    {
        if (playerRoot == null)
        {
            Debug.LogError(
                "没有找到玩家，无法以玩家为中心生成敌人。",
                logContext
            );
            return 0;
        }

        int spawnedCount = 0;
        int matchedRows = 0;

        foreach (cfg.LevelSpawn spawn in tables.TbLevelSpawn.DataList)
        {
            if (spawn.SpawnGroupId != spawnGroupId)
                continue;

            matchedRows++;

            if (spawn.MinRadius < 0f || spawn.SpawnRadius < spawn.MinRadius)
            {
                Debug.LogError(
                    $"敌人生成配置 Id={spawn.Id} 的半径范围无效：" +
                    $"MinRadius={spawn.MinRadius}, SpawnRadius={spawn.SpawnRadius}。",
                    logContext
                );
                continue;
            }

            GameObject prefab = FindEnemyPrefab(spawn.EnemyKey);
            if (prefab == null)
            {
                Debug.LogError(
                    $"Assets/Resources/{EnemyPrefabResourcesPath} 中找不到名为 {spawn.EnemyKey} 的敌人 Prefab。",
                    logContext
                );
                continue;
            }

            if (spawn.Count <= 0)
            {
                Debug.LogWarning(
                    $"生成组 {spawnGroupId} 的 {spawn.EnemyKey} 数量小于等于 0。",
                    logContext
                );
                continue;
            }

            Combatant prefabCombatant = prefab.GetComponent<Combatant>();
            CombatFactionMarker prefabFaction =
                prefab.GetComponent<CombatFactionMarker>();
            if (prefabCombatant == null ||
                prefabFaction == null ||
                prefabFaction.Faction != CombatFaction.Enemy)
            {
                Debug.LogError(
                    $"敌人 Prefab {prefab.name} 必须包含 Combatant 和 Enemy 阵营标记。",
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

            cfg.Skill[] skills = ResolveSkills(tables, enemyConfig, logContext);
            if (skills == null)
                continue;

            for (int i = 0; i < spawn.Count; i++)
            {
                Vector3 position = GetSpawnPosition(
                    playerRoot,
                    spawn.MinRadius,
                    spawn.SpawnRadius,
                    logContext
                );
                GameObject enemy = UnityEngine.Object.Instantiate(
                    prefab,
                    position,
                    prefab.transform.rotation,
                    enemyRoot
                );

                Health health = enemy.GetComponent<Health>();
                Attack attack = enemy.GetComponent<Attack>();
                NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
                Combatant combatant = enemy.GetComponent<Combatant>();
                CombatFactionMarker factionMarker =
                    enemy.GetComponent<CombatFactionMarker>();

                if (health == null || attack == null || agent == null ||
                    combatant == null || factionMarker == null ||
                    factionMarker.Faction != CombatFaction.Enemy)
                {
                    Debug.LogError(
                        $"敌人 Prefab {prefab.name} 必须包含 Health、Attack、NavMeshAgent、Combatant 和 Enemy 阵营标记。",
                        prefab
                    );
                    UnityEngine.Object.Destroy(enemy);
                    continue;
                }

                combatant.InitializeFromConfig(enemyConfig, skills);
                health.InitializeFromConfig(enemyConfig.MaxHealth);
                agent.speed = Mathf.Max(0.1f, enemyConfig.MoveSpeed);
                spawnedCount++;
            }
        }

        if (matchedRows == 0)
        {
            Debug.LogError(
                $"TbLevelSpawn 中找不到 SpawnGroupId = {spawnGroupId} 的记录。",
                logContext
            );
        }

        return spawnedCount;
    }

    private static cfg.Skill[] ResolveSkills(
        cfg.Tables tables,
        cfg.Enemy enemy,
        UnityEngine.Object logContext)
    {
        if (enemy.SkillIds.Length == 0)
        {
            Debug.LogError(
                $"敌人 {enemy.EnemyName}（ID: {enemy.EnemyId}）没有配置技能，无法初始化攻击属性。",
                logContext
            );
            return null;
        }

        cfg.Skill[] skills = new cfg.Skill[enemy.SkillIds.Length];
        for (int i = 0; i < enemy.SkillIds.Length; i++)
        {
            int skillId = enemy.SkillIds[i];
            skills[i] = tables.TbSkill.GetOrDefault(skillId);
            if (skills[i] == null)
            {
                Debug.LogError(
                    $"敌人 {enemy.EnemyName} 引用的技能 ID {skillId} 不存在于 TbSkill，" +
                    "无法初始化攻击属性。",
                    logContext
                );
                return null;
            }
        }

        return skills;
    }

    private static cfg.Enemy ResolveEnemyConfig(
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

    private static Vector3 GetSpawnPosition(
        Transform playerRoot,
        float minRadius,
        float maxRadius,
        UnityEngine.Object logContext)
    {
        const int maxAttempts = 20;
        Vector3 origin = playerRoot.position;
        Vector3 candidate = origin;
        float minRadiusSquared = minRadius * minRadius;
        float maxRadiusSquared = maxRadius * maxRadius;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float radius = Mathf.Sqrt(
                UnityEngine.Random.Range(minRadiusSquared, maxRadiusSquared)
            );
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );
            candidate = origin + offset;

            if (NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    3f,
                    NavMesh.AllAreas) &&
                IsWithinSpawnRadii(
                    hit.position,
                    origin,
                    minRadiusSquared,
                    maxRadiusSquared))
            {
                return hit.position;
            }
        }

        Debug.LogWarning(
            "未找到符合最小/最大生成半径的 NavMesh 位置，" +
            "将使用半径范围内的候选位置，请检查地图烘焙。",
            logContext
        );
        return candidate;
    }

    private static bool IsWithinSpawnRadii(
        Vector3 position,
        Vector3 origin,
        float minRadiusSquared,
        float maxRadiusSquared)
    {
        Vector2 offset = new Vector2(
            position.x - origin.x,
            position.z - origin.z
        );
        float distanceSquared = offset.sqrMagnitude;
        return distanceSquared >= minRadiusSquared &&
            distanceSquared <= maxRadiusSquared;
    }

    private GameObject FindEnemyPrefab(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        loadedEnemyPrefabs ??= Resources.LoadAll<GameObject>(
            EnemyPrefabResourcesPath
        );

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
}
