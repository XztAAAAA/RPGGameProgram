using System.Collections.Generic;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public sealed class SkillProjectile : MonoBehaviour
{
    private const int InitialHitBufferSize = 16;
    private const string ProjectilePrefabPath = "Prefabs/Bullet/Bullet";
    private static readonly List<SkillProjectile> ActiveProjectiles = new List<SkillProjectile>();
    private static Collider[] overlapBuffer = new Collider[InitialHitBufferSize];
    private static RaycastHit[] sphereCastBuffer = new RaycastHit[InitialHitBufferSize];
    private static GameObject projectilePrefab;
    private static bool projectilePrefabLoadFailed;

    private Vector3 direction;
    private CombatFaction ownerFaction;
    private bool canBlock;
    private float damage;
    private float radius;
    private float speed;
    private float remainingTime;
    private LayerMask hitLayers;

    public static void Create(Vector3 position, Vector3 direction, CombatFaction ownerFaction,
        float damage, float radius, float speed, float projectileTime, LayerMask hitLayers,
        bool canBlock)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
        {
            Debug.LogError("SkillProjectile：弹幕半径必须是大于 0 的有限数值。");
            return;
        }

        if (float.IsNaN(projectileTime) || float.IsInfinity(projectileTime) ||
            projectileTime <= 0f)
        {
            Debug.LogError("SkillProjectile：弹幕持续时间必须是大于 0 的有限数值。");
            return;
        }

        if (projectilePrefab == null)
        {
            projectilePrefab = Resources.Load<GameObject>(ProjectilePrefabPath);
            if (projectilePrefab == null)
            {
                if (!projectilePrefabLoadFailed)
                {
                    Debug.LogError(
                        $"无法加载 Resources/{ProjectilePrefabPath}.prefab，未生成弹幕。"
                    );
                }

                projectilePrefabLoadFailed = true;
                return;
            }
        }

        SkillProjectile prefabComponent =
            projectilePrefab.GetComponent<SkillProjectile>();
        MeshFilter prefabMesh = projectilePrefab.GetComponent<MeshFilter>();
        MeshRenderer prefabRenderer = projectilePrefab.GetComponent<MeshRenderer>();
        if (prefabComponent == null ||
            prefabMesh == null ||
            prefabMesh.sharedMesh == null ||
            prefabRenderer == null ||
            prefabRenderer.sharedMaterial == null)
        {
            Debug.LogError(
                $"Resources/{ProjectilePrefabPath}.prefab 必须包含 SkillProjectile、" +
                "MeshFilter、有效网格和带材质的 MeshRenderer。"
            );
            return;
        }

        GameObject projectile = Instantiate(
            projectilePrefab,
            position,
            Quaternion.identity
        );
        if (projectile == null)
        {
            Debug.LogError($"实例化 Resources/{ProjectilePrefabPath}.prefab 失败。");
            return;
        }

        projectile.name = "Skill Projectile";
        projectile.transform.localScale = Vector3.one * (radius * 2f);
        SkillProjectile component = projectile.GetComponent<SkillProjectile>();
        if (component == null)
        {
            Debug.LogError(
                $"Resources/{ProjectilePrefabPath}.prefab 实例缺少 SkillProjectile 组件。"
            );
            Destroy(projectile);
            return;
        }

        component.Initialize(
            direction,
            ownerFaction,
            canBlock,
            damage,
            radius,
            speed,
            projectileTime,
            hitLayers
        );
    }

    private void Initialize(
        Vector3 projectileDirection,
        CombatFaction projectileOwnerFaction,
        bool projectileCanBlock,
        float projectileDamage,
        float projectileRadius,
        float projectileSpeed,
        float projectileTime,
        LayerMask projectileHitLayers)
    {
        direction = projectileDirection.normalized;
        ownerFaction = projectileOwnerFaction;
        canBlock = projectileCanBlock;
        damage = projectileDamage;
        radius = projectileRadius;
        speed = projectileSpeed;
        remainingTime = projectileTime;
        hitLayers = projectileHitLayers;
    }

    public static void BlockInRange(Vector3 center, float radius, CombatFaction blockingFaction)
    {
        for (int i = ActiveProjectiles.Count - 1; i >= 0; i--)
        {
            SkillProjectile projectile = ActiveProjectiles[i];
            float reach = radius + projectile.radius;
            float reachSquared = reach * reach;
            if (!projectile.canBlock || projectile.ownerFaction == blockingFaction ||
                (projectile.transform.position - center).sqrMagnitude > reachSquared)
            {
                continue;
            }

            projectile.gameObject.SetActive(false);
            Destroy(projectile.gameObject);
        }
    }

    [UnityEngine.Scripting.Preserve]
    private void OnEnable()
    {
        ActiveProjectiles.Add(this);
    }

    [UnityEngine.Scripting.Preserve]
    private void OnDisable()
    {
        ActiveProjectiles.Remove(this);
    }

    [UnityEngine.Scripting.Preserve]
    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        int overlapCount = GetOverlappingColliders();
        Health overlappingEnemy = null;
        bool overlappingObstacle = false;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider hit = overlapBuffer[i];
            overlapBuffer[i] = null;
            if (hit == null)
                continue;

            Health health = GetEnemyHealth(hit);
            if (health != null)
            {
                if (overlappingEnemy == null)
                    overlappingEnemy = health;
            }
            else if (IsObstacle(hit))
            {
                overlappingObstacle = true;
            }
        }

        if (overlappingObstacle)
        {
            Destroy(gameObject);
            return;
        }

        if (overlappingEnemy != null)
        {
            overlappingEnemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        float deltaTime = Mathf.Min(Time.deltaTime, remainingTime);
        float distance = speed * deltaTime;
        int hitCount = GetSphereCastHits(distance);
        Health closestHealth = null;
        float closestDistance = float.PositiveInfinity;
        float closestObstacleDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = sphereCastBuffer[i];
            sphereCastBuffer[i] = default;
            Health health = GetEnemyHealth(hit.collider);
            if (health != null && hit.distance < closestDistance)
            {
                closestHealth = health;
                closestDistance = hit.distance;
            }
            else if (health == null && IsObstacle(hit.collider) &&
                hit.distance < closestObstacleDistance)
            {
                closestObstacleDistance = hit.distance;
            }
        }

        if (closestObstacleDistance <= closestDistance)
        {
            Destroy(gameObject);
            return;
        }

        if (closestHealth != null)
        {
            closestHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        transform.position += direction * distance;
        remainingTime -= Time.deltaTime;
        if (remainingTime <= 0f)
            Destroy(gameObject);
    }

    private bool IsObstacle(Collider hit)
    {
        return hit.GetComponentInParent<CombatFactionMarker>() == null;
    }

    private int GetOverlappingColliders()
    {
        while (true)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                overlapBuffer,
                hitLayers,
                QueryTriggerInteraction.Ignore
            );
            if (hitCount < overlapBuffer.Length)
                return hitCount;

            System.Array.Clear(overlapBuffer, 0, hitCount);
            System.Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
        }
    }

    private int GetSphereCastHits(float distance)
    {
        while (true)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                transform.position,
                radius,
                direction,
                sphereCastBuffer,
                distance,
                hitLayers,
                QueryTriggerInteraction.Ignore
            );
            if (hitCount < sphereCastBuffer.Length)
                return hitCount;

            System.Array.Clear(sphereCastBuffer, 0, hitCount);
            System.Array.Resize(ref sphereCastBuffer, sphereCastBuffer.Length * 2);
        }
    }

    private Health GetEnemyHealth(Collider hit)
    {
        Health health = hit.GetComponentInParent<Health>();
        if (health == null || health.IsDead)
            return null;

        CombatFactionMarker faction = health.GetComponentInParent<CombatFactionMarker>();
        return faction != null && faction.Faction != ownerFaction ? health : null;
    }
}
