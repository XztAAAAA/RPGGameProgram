using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("生命值设置")]
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }

    public bool IsDead { get; private set; }

    // 生命值变化事件：当前生命值、最大生命值
    public event Action<float, float> HealthChanged;

    // 死亡事件
    public event Action Died;

    private bool initializedFromConfig;

    public void InitializeFromConfig(float configuredMaxHealth)
    {
        maxHealth = Mathf.Max(1f, configuredMaxHealth);

        CurrentHealth = maxHealth;
        IsDead = false;

        initializedFromConfig = true;

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    private void Awake()
    {
        if (initializedFromConfig)
            return;

        CurrentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Max(
            0f,
            CurrentHealth - amount
        );

        HealthChanged?.Invoke(
            CurrentHealth,
            MaxHealth
        );

        Debug.Log(
            $"{gameObject.name} 受到 {amount} 点伤害，" +
            $"剩余生命：{CurrentHealth}/{MaxHealth}"
        );

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            Died?.Invoke();

            Debug.Log($"{gameObject.name} 已死亡");
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Min(
            MaxHealth,
            CurrentHealth + amount
        );

        HealthChanged?.Invoke(
            CurrentHealth,
            MaxHealth
        );
    }
}