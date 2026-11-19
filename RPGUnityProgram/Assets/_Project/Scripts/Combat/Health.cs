using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [Header("生命值设置")]
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;

    private float damageReduction;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }

    public bool IsDead { get; private set; }

    // 生命值变化事件：当前生命值、最大生命值
    public event Action<float, float> HealthChanged;

    // 死亡事件
    public event Action Died;
    public event Action Damaged;

    private bool initializedFromConfig;
    private PlayerMovement playerMovement;

    public void InitializeFromConfig(float configuredMaxHealth)
    {
        maxHealth = Mathf.Max(1f, configuredMaxHealth);

        CurrentHealth = maxHealth;
        IsDead = false;

        initializedFromConfig = true;

        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void ApplyMaxHealthMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) ||
            multiplier < 1f)
        {
            Debug.LogError(
                $"Health：最大生命值倍率必须是大于等于 1 的有限数值：{multiplier}。",
                this
            );
            return;
        }

        float previousMaxHealth = maxHealth;
        maxHealth *= multiplier;
        CurrentHealth = Mathf.Min(
            maxHealth,
            CurrentHealth + maxHealth - previousMaxHealth
        );
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void ApplyDamageReduction(float reduction)
    {
        if (float.IsNaN(reduction) || float.IsInfinity(reduction) ||
            reduction < 0f || reduction >= 1f)
        {
            Debug.LogError(
                $"Health：伤害减免必须是 [0, 1) 范围内的有限数值：{reduction}。",
                this
            );
            return;
        }

        damageReduction = reduction;
    }

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();

        if (initializedFromConfig)
            return;

        CurrentHealth = maxHealth;
        IsDead = false;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f ||
            (playerMovement != null && playerMovement.IsDodging))
            return;

        amount *= 1f - damageReduction;
        if (amount <= 0f)
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
        else
        {
            Damaged?.Invoke();
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