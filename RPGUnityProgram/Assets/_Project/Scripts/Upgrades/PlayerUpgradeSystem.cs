using System;
using System.Collections.Generic;
using UnityEngine;

public static class PlayerUpgradeSystem
{
    private const int OfferCount = 3;
    private const float MaxDamageReduction = 0.8f;

    private static readonly Dictionary<int, int> upgradeLevels =
        new Dictionary<int, int>();

    public static int GetUpgradeLevel(int upgradeId)
    {
        return upgradeLevels.TryGetValue(upgradeId, out int level)
            ? level
            : 0;
    }

    public static bool TryRollOffers(
        int currentLevel,
        out List<cfg.Upgrade> offers,
        UnityEngine.Object logContext)
    {
        offers = new List<cfg.Upgrade>(OfferCount);
        if (currentLevel <= 0)
        {
            Debug.LogError(
                $"强化技能：当前关卡 ID 无效：{currentLevel}。",
                logContext
            );
            return false;
        }

        cfg.Tables tables;
        try
        {
            tables = TableSystem.Tables;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"强化技能：无法加载 Luban 配置。\n{exception}",
                logContext
            );
            return false;
        }

        List<cfg.Upgrade> pool = new List<cfg.Upgrade>();
        foreach (cfg.Upgrade upgrade in tables.TbUpgrade.DataList)
        {
            if (upgrade == null ||
                upgrade.UnlockLevel > currentLevel ||
                GetUpgradeLevel(upgrade.Id) >= upgrade.MaxStack)
            {
                continue;
            }

            if (upgrade.Weight <= 0)
            {
                Debug.LogWarning(
                    $"强化技能 ID={upgrade.Id} 的抽取权重必须大于 0，已从卡池排除。",
                    logContext
                );
                continue;
            }

            pool.Add(upgrade);
        }

        while (offers.Count < OfferCount && pool.Count > 0)
        {
            long totalWeight = 0;
            foreach (cfg.Upgrade upgrade in pool)
                totalWeight += upgrade.Weight;

            double selection = UnityEngine.Random.value * totalWeight;
            int selectedIndex = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                selection -= pool[i].Weight;
                if (selection < 0d)
                {
                    selectedIndex = i;
                    break;
                }
            }

            offers.Add(pool[selectedIndex]);
            pool.RemoveAt(selectedIndex);
        }

        return true;
    }

    public static bool TryAcquire(
        cfg.Upgrade upgrade,
        GameObject player,
        UnityEngine.Object logContext)
    {
        if (upgrade == null)
        {
            Debug.LogError("强化技能：不能获取空配置。", logContext);
            return false;
        }

        int currentLevel = GetUpgradeLevel(upgrade.Id);
        if (currentLevel >= upgrade.MaxStack)
        {
            Debug.LogError(
                $"强化技能 {upgrade.Name} 已达到等级上限 {upgrade.MaxStack}。",
                logContext
            );
            return false;
        }

        if (!ApplyEffect(player, upgrade, 1, logContext))
            return false;

        upgradeLevels[upgrade.Id] = currentLevel + 1;
        return true;
    }

    public static void ApplyToPlayer(
        GameObject player,
        UnityEngine.Object logContext)
    {
        if (upgradeLevels.Count == 0)
            return;

        cfg.Tables tables;
        try
        {
            tables = TableSystem.Tables;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"强化技能：无法加载 Luban 配置。\n{exception}",
                logContext
            );
            return;
        }

        foreach (KeyValuePair<int, int> entry in upgradeLevels)
        {
            cfg.Upgrade upgrade = tables.TbUpgrade.GetOrDefault(entry.Key);
            if (upgrade == null)
            {
                Debug.LogError(
                    $"强化技能：TbUpgrade 中找不到已获取的 ID={entry.Key}。",
                    logContext
                );
                continue;
            }

            ApplyEffect(player, upgrade, entry.Value, logContext);
        }
    }

    public static void ResetRun()
    {
        upgradeLevels.Clear();
    }

    private static bool ApplyEffect(
        GameObject player,
        cfg.Upgrade upgrade,
        int stacks,
        UnityEngine.Object logContext)
    {
        if (player == null)
        {
            Debug.LogError("强化技能：找不到玩家，无法应用强化效果。", logContext);
            return false;
        }

        float value = upgrade.EffectValue;
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            Debug.LogError(
                $"强化技能 ID={upgrade.Id} 的效果值无效：{value}。",
                logContext
            );
            return false;
        }

        switch (upgrade.EffectType)
        {
            case cfg.upgradeEnum.sword_damage:
            case cfg.upgradeEnum.sword_speed:
            case cfg.upgradeEnum.sword_range:
            case cfg.upgradeEnum.parry_window:
            {
                Attack attack = player.GetComponent<Attack>();
                if (attack == null)
                {
                    Debug.LogError(
                        "强化技能：玩家缺少 Attack 组件，无法应用攻击强化。",
                        player
                    );
                    return false;
                }

                attack.ApplyUpgrade(upgrade.EffectType, value, stacks);
                return true;
            }

            case cfg.upgradeEnum.max_health:
            case cfg.upgradeEnum.damage_reduction:
            {
                Health health = player.GetComponent<Health>();
                if (health == null)
                {
                    Debug.LogError(
                        "强化技能：玩家缺少 Health 组件，无法应用生存强化。",
                        player
                    );
                    return false;
                }

                if (upgrade.EffectType == cfg.upgradeEnum.max_health)
                {
                    health.ApplyMaxHealthMultiplier(1f + value * stacks);
                }
                else
                {
                    health.ApplyDamageReduction(
                        Mathf.Min(MaxDamageReduction, value * stacks)
                    );
                }

                return true;
            }

            case cfg.upgradeEnum.dodge_cooldown:
            case cfg.upgradeEnum.move_speed:
            {
                PlayerMovement movement =
                    player.GetComponent<PlayerMovement>();
                if (movement == null)
                {
                    Debug.LogError(
                        "强化技能：玩家缺少 PlayerMovement 组件，无法应用移动强化。",
                        player
                    );
                    return false;
                }

                movement.ApplyUpgrade(upgrade.EffectType, value, stacks);
                return true;
            }

            default:
                Debug.LogError(
                    $"强化技能 ID={upgrade.Id} 使用未支持的效果类型 {upgrade.EffectType}。",
                    logContext
                );
                return false;
        }
    }
}
