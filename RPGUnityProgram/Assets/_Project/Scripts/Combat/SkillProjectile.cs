using System.Collections.Generic;
using UnityEngine;

public sealed class SkillProjectile : MonoBehaviour
{
    private const float Radius = 0.2f;
    private const int InitialHitBufferSize = 16;
    private static readonly List<SkillProjectile> ActiveProjectiles = new List<SkillProjectile>();
    private static Collider[] overlapBuffer = new Collider[InitialHitBufferSize];
    private static RaycastHit[] sphereCastBuffer = new RaycastHit[InitialHitBufferSize];

    private Vector3 direction;
    private CombatFaction ownerFaction;
    private bool canBlock;
    private float damage;
    private float speed;
    private float remainingDistance;
    private LayerMask hitLayers;

    public static void Create(Vector3 position, Vector3 direction, CombatFaction ownerFaction,
        float damage, float speed, float maxDistance, LayerMask hitLayers, bool canBlock)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Skill Projectile";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * (Radius * 2f);
        Collider projectileCollider = projectile.GetComponent<Collider>();
        projectileCollider.enabled = false;
        Destroy(projectileCollider);

        SkillProjectile component = projectile.AddComponent<SkillProjectile>();
        component.direction = direction.normalized;
        component.ownerFaction = ownerFaction;
        component.canBlock = canBlock;
        component.damage = damage;
        component.speed = speed;
        component.remainingDistance = maxDistance;
        component.hitLayers = hitLayers;
    }

    public static void BlockInRange(Vector3 center, float radius, CombatFaction blockingFaction)
    {
        float reach = radius + Radius;
        float reachSquared = reach * reach;
        for (int i = ActiveProjectiles.Count - 1; i >= 0; i--)
        {
            SkillProjectile projectile = ActiveProjectiles[i];
            if (!projectile.canBlock || projectile.ownerFaction == blockingFaction ||
                (projectile.transform.position - center).sqrMagnitude > reachSquared)
            {
                continue;
            }

            projectile.gameObject.SetActive(false);
            Destroy(projectile.gameObject);
        }
    }

    private void OnEnable()
    {
        ActiveProjectiles.Add(this);
    }

    private void OnDisable()
    {
        ActiveProjectiles.Remove(this);
    }

    private void Update()
    {
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

        float distance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
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
        remainingDistance -= distance;
        if (remainingDistance <= 0f)
            Destroy(gameObject);
    }

    private int GetOverlappingColliders()
    {
        while (true)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                Radius,
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
                Radius,
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
