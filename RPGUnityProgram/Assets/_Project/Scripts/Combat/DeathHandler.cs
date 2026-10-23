using UnityEngine;

[RequireComponent(typeof(Health))]
public class DeathHandler : MonoBehaviour
{
    [Header("死亡设置")]
    [SerializeField]
    private bool destroyOnDeath = true;

    [SerializeField, Min(0f)]
    private float destroyDelay = 0.25f;

    [Header("死亡时需要停止的其他行为")]
    [SerializeField]
    private Behaviour[] behavioursToDisable;

    private Health health;
    private bool deathHandled;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (health != null)
            health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (deathHandled)
            return;

        deathHandled = true;

        // 死亡后不能继续攻击
        Attack attack = GetComponent<Attack>();

        if (attack != null)
            attack.enabled = false;

        // 停止移动、输入或敌人 AI 等行为
        foreach (Behaviour behaviour in behavioursToDisable)
        {
            if (behaviour != null && behaviour != this)
                behaviour.enabled = false;
        }

        // 关闭自身及子物体上的碰撞体
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Debug.Log($"{gameObject.name} 已死亡");

        // 敌人通常销毁；玩家通常保留并进入失败流程
        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
    }
}