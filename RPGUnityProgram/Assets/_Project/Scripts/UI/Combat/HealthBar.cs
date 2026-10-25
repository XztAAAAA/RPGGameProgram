using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("UI 组件")]
    [SerializeField] private Slider slider;

    private Health targetHealth;
    private bool isSubscribed;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        if (targetHealth != null)
        {
            Refresh(
                targetHealth.CurrentHealth,
                targetHealth.MaxHealth
            );
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// 初始化血条，并绑定需要显示生命值的对象。
    /// </summary>
    public void Initialize(Health health)
    {
        Unsubscribe();

        targetHealth = health;

        if (slider == null)
            slider = GetComponent<Slider>();

        if (targetHealth == null)
        {
            Debug.LogError($"{name}：没有指定 Health。", this);
            return;
        }

        if (slider == null)
        {
            Debug.LogError($"{name}：没有找到 Slider 组件。", this);
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;

        // 立即刷新，避免开场血条显示错误
        Refresh(
            targetHealth.CurrentHealth,
            targetHealth.MaxHealth
        );

        Subscribe();
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled ||
            targetHealth == null ||
            isSubscribed)
            return;

        targetHealth.HealthChanged += OnHealthChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (targetHealth == null || !isSubscribed)
            return;

        targetHealth.HealthChanged -= OnHealthChanged;
        isSubscribed = false;
    }

    private void OnHealthChanged(float current, float max)
    {
        Refresh(current, max);
    }

    private void Refresh(float current, float max)
    {
        if (slider == null)
            return;

        slider.value = max > 0f
            ? Mathf.Clamp01(current / max)
            : 0f;
    }
}