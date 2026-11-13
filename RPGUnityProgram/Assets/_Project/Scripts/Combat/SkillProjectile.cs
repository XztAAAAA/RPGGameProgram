using UnityEngine;

public sealed class SkillProjectile : MonoBehaviour
{
    private const float Radius = 0.2f;
    private const float Lifetime = 15f;

    private Health target;
    private Collider targetCollider;
    private float damage;
    private float speed;
    private float expiresAt;

    public static void Create(Vector3 position, Health target, float damage, float speed)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "Skill Projectile";
        projectile.transform.position = position;
        projectile.transform.localScale = Vector3.one * (Radius * 2f);
        Collider projectileCollider = projectile.GetComponent<Collider>();
        projectileCollider.enabled = false;
        Destroy(projectileCollider);

        SkillProjectile component = projectile.AddComponent<SkillProjectile>();
        component.target = target;
        component.targetCollider = target.GetComponent<Collider>();
        component.damage = damage;
        component.speed = speed;
        component.expiresAt = Time.time + Lifetime;
    }

    private void Update()
    {
        if (target == null || !target.gameObject.activeInHierarchy ||
            target.IsDead || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = targetCollider != null && targetCollider.enabled
            ? targetCollider.bounds.center
            : target.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        Vector3 closestPoint = targetCollider != null && targetCollider.enabled
            ? targetCollider.ClosestPoint(transform.position)
            : destination;
        if ((transform.position - closestPoint).sqrMagnitude <= Radius * Radius)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
