using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    private const float ParryShakeDuration = 0.18f;
    private const float ParryShakeStrength = 0.14f;
    private const float ParryShakeFrequency = 25f;
    private const float HitShakeDuration = 0.12f;
    private const float HitShakeStrength = 0.045f;

    [Header("跟随目标")]
    [SerializeField] private Transform target;

    [Header("跟随平滑度")]
    [SerializeField] private float smoothTime = 0.2f;

    [Header("当前地图的镜头边界")]
    [SerializeField] private BoxCollider cameraBounds;

    [Tooltip("边界内额外保留的距离")]
    [SerializeField] private float boundsPadding = 0f;

    // 相机与玩家之间的初始位置偏移
    private Vector3 offset;

    // SmoothDamp 内部使用的速度
    private Vector3 currentVelocity = Vector3.zero;

    private float shakeRemaining;
    private float shakeDuration;
    private float shakeStrength;
    private Vector3 appliedShakeOffset;

    public void ShakeForMeleeParry()
    {
        StartShake(ParryShakeDuration, ParryShakeStrength);
    }

    public void ShakeForMeleeHit()
    {
        StartShake(HitShakeDuration, HitShakeStrength);
    }

    private void StartShake(float duration, float strength)
    {
        if (shakeRemaining > 0f && shakeStrength > strength)
            return;

        shakeRemaining = duration;
        shakeDuration = duration;
        shakeStrength = strength;
    }

    private void Awake()
    {
        GameObject player = CombatTool.GetPlayer();
        target = player != null ? player.transform : null;
        cameraBounds = GameObject.Find("CameraBounds").GetComponent<BoxCollider>();
    }

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("CameraFollow：请指定 Player 作为跟随目标！");
            enabled = false;
            return;
        }

        // 自动记录当前相机与玩家的相对位置
        offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        transform.position -= appliedShakeOffset;
        appliedShakeOffset = Vector3.zero;

        if (target == null)
            return;

        // 默认跟随玩家当前位置
        Vector3 followPosition = target.position;

        // 如果配置了有效的边界，则限制跟随目标的位置
        if (cameraBounds != null &&
            cameraBounds.enabled &&
            cameraBounds.gameObject.activeInHierarchy)
        {
            Bounds bounds = cameraBounds.bounds;

            float minX = bounds.min.x + boundsPadding;
            float maxX = bounds.max.x - boundsPadding;

            float minZ = bounds.min.z + boundsPadding;
            float maxZ = bounds.max.z - boundsPadding;

            // 防止边界过小导致最小值大于最大值
            if (minX > maxX)
                minX = maxX = bounds.center.x;

            if (minZ > maxZ)
                minZ = maxZ = bounds.center.z;

            // 仅限制 X、Z 轴，保留玩家的高度
            followPosition.x = Mathf.Clamp(
                followPosition.x, minX, maxX
            );

            followPosition.z = Mathf.Clamp(
                followPosition.z, minZ, maxZ
            );
        }

        // 保持原来的相机距离和构图
        Vector3 targetPosition = followPosition + offset;

        // 平滑跟随玩家
        if (Time.deltaTime > 0f)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref currentVelocity,
                smoothTime
            );
        }

        if (shakeRemaining > 0f)
        {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);
            float elapsed = shakeDuration - shakeRemaining;
            float strength = shakeStrength * (shakeRemaining / shakeDuration);
            float phase = elapsed * ParryShakeFrequency * 2f * Mathf.PI;
            appliedShakeOffset = (transform.right * Mathf.Sin(phase) +
                transform.up * Mathf.Cos(phase)) * strength;
            transform.position += appliedShakeOffset;
        }
    }

    private void OnDisable()
    {
        transform.position -= appliedShakeOffset;
        appliedShakeOffset = Vector3.zero;
        shakeRemaining = 0f;
    }
}