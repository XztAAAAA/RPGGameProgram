using System.Collections.Generic;
using UnityEngine;

[UnityEngine.Scripting.Preserve]
public sealed class SkillProjectile : MonoBehaviour
{
    private const int InitialHitBufferSize = 16;
    private const string ProjectileMaterialPath = "Materials/SkillProjectile";
    private static readonly List<SkillProjectile> ActiveProjectiles = new List<SkillProjectile>();
    private static Collider[] overlapBuffer = new Collider[InitialHitBufferSize];
    private static RaycastHit[] sphereCastBuffer = new RaycastHit[InitialHitBufferSize];
    private static Material projectileMaterial;
    private static bool projectileMaterialLoadFailed;

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

        if (projectileMaterial == null)
        {
            projectileMaterial = Resources.Load<Material>(ProjectileMaterialPath);
            if (projectileMaterial == null)
            {
                if (!projectileMaterialLoadFailed)
                {
                    Debug.LogError(
                        $"无法加载 Resources/{ProjectileMaterialPath} 弹幕材质，未生成弹幕。"
                    );
                }

                projectileMaterialLoadFailed = true;
                return;
            }

            projectileMaterialLoadFailed = false;
        }

        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Skill Projectile";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * (radius * 2f);
        MeshFilter meshFilter = projectile.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = projectile.GetComponent<MeshRenderer>();
        if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
        {
            Debug.LogError("SkillProjectile：无法创建弹幕的网格或渲染组件。");
            Destroy(projectile);
            return;
        }

        meshRenderer.sharedMaterial = projectileMaterial;
        SphereCollider projectileCollider = projectile.GetComponent<SphereCollider>();
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
            Destroy(projectileCollider);
        }
        else
        {
            Debug.LogWarning("SkillProjectile：球体碰撞体缺失，弹幕仍将使用物理查询检测命中。");
        }

        SkillProjectile component = projectile.AddComponent<SkillProjectile>();
        if (component == null)
        {
            Debug.LogError("SkillProjectile：无法添加弹幕移动组件。");
            Destroy(projectile);
            return;
        }

        component.direction = direction.normalized;
        component.ownerFaction = ownerFaction;
        component.canBlock = canBlock;
        component.damage = damage;
        component.radius = radius;
        component.speed = speed;
        component.remainingTime = projectileTime;
        component.hitLayers = hitLayers;
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
        for (int i = 0; i < overlapCount; i++)
        {
            Collider hit = overlapBuffer[i];
            overlapBuffer[i] = null;
            Health health = GetEnemyHealth(hit);
            if (health != null)
            {
                for (int j = i + 1; j < overlapCount; j++)
                    overlapBuffer[j] = null;

                health.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
        }

        float deltaTime = Mathf.Min(Time.deltaTime, remainingTime);
        float distance = speed * deltaTime;
        int hitCount = GetSphereCastHits(distance);
        Health closestHealth = null;
        float closestDistance = float.PositiveInfinity;
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
