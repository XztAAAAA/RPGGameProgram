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
        Transform enemyRoot,
        UnityEngine.Object logContext)
    {
        int spawnedCount = 0;
        int matchedRows = 0;

        foreach (cfg.LevelSpawn spawn in tables.TbLevelSpawn.DataList)
        {
            if (spawn.SpawnGroupId != spawnGroupId)
                continue;

            matchedRows++;

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

            cfg.Skill primarySkill = ResolvePrimarySkill(tables, enemyConfig, logContext);
            if (primarySkill == null)
                continue;

            for (int i = 0; i < spawn.Count; i++)
            {
                Vector3 position = GetSpawnPosition(enemyRoot, spawn.SpawnRadius, logContext);
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

                combatant.InitializeFromConfig(enemyConfig, primarySkill);
                health.InitializeFromConfig(spawn.MaxHealth);
                agent.speed = Mathf.Max(0.1f, spawn.MoveSpeed);
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

    private static cfg.Skill ResolvePrimarySkill(
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

        if (enemy.SkillIds.Length > 1)
        {
            Debug.LogWarning(
                $"敌人 {enemy.EnemyName} 配置了多个技能；当前 Attack 组件只支持一个技能，" +
                $"本次使用第一个技能 ID {enemy.SkillIds[0]}。",
                logContext
            );
        }

        cfg.Skill skill = tables.TbSkill.GetOrDefault(enemy.SkillIds[0]);
        if (skill == null)
        {
            Debug.LogError(
                $"敌人 {enemy.EnemyName} 引用的技能 ID {enemy.SkillIds[0]} 不存在于 TbSkill，" +
                "无法初始化攻击属性。",
                logContext
            );
        }

        return skill;
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
        Transform enemyRoot,
        float spawnRadius,
        UnityEngine.Object logContext)
    {
        Vector2 offset =
            UnityEngine.Random.insideUnitCircle *
            Mathf.Max(0f, spawnRadius);

        Vector3 candidate = enemyRoot.position +
            new Vector3(offset.x, 0f, offset.y);

        if (NavMesh.SamplePosition(
                candidate,
                out NavMeshHit hit,
                3f,
                NavMesh.AllAreas))
        {
            return hit.position;
        }

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
            logContext
        );
        return candidate;
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
