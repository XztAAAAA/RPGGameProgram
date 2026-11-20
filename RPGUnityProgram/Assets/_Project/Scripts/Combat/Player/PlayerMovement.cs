using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家移动输入。
/// 支持 PC WASD 和移动端 OnScreenStick 虚拟摇杆。
/// 同时读取移动端攻击按钮，并发布本地攻击请求事件。
/// PC 键盘/鼠标攻击仍由现有 PlayerAttackInput 负责。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 720f;

    [Header("参考相机")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Health health;
    private Attack attack;

    // WASD + 虚拟 Gamepad 左摇杆
    private InputAction moveAction;

    // 仅负责移动端虚拟攻击按钮，避免与现有 PC PlayerAttackInput 重复
    private InputAction mobileAttackAction;
    private InputAction dodgeAction;

    private float dodgeDistance;
    private float dodgeCooldown;
    private float dodgeDuration;
    private float nextDodgeTime;
    private float dodgeElapsed;
    private Vector3 dodgeDirection;
    private bool dodgeConfigured;

    public event Action MobileAttackRequested;
    public event Action DodgeStarted;
    public bool IsDodging { get; private set; }
    public float MovementAmount { get; private set; }
    public float DodgeDuration => dodgeDuration;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        attack = GetComponent<Attack>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        CreateInputActions();
    }

    private void CreateInputActions()
    {
        moveAction = new InputAction(
            name: "PlayerMove",
            type: InputActionType.Value,
            expectedControlType: "Vector2"
        );

        // PC：WASD
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        // Mobile：OnScreenStick 写入的虚拟 Gamepad 左摇杆
        moveAction.AddBinding("<Gamepad>/leftStick");

        mobileAttackAction = new InputAction(
            name: "MobileAttack",
            type: InputActionType.Button
        );

        // Mobile：OnScreenButton 写入的虚拟 Gamepad 南键
        mobileAttackAction.AddBinding("<Gamepad>/buttonSouth");

        dodgeAction = new InputAction(
            name: "Dodge",
            type: InputActionType.Button
        );
        dodgeAction.AddBinding("<Keyboard>/leftShift");
        dodgeAction.AddBinding("<Keyboard>/rightShift");
        dodgeAction.AddBinding("<Mouse>/rightButton");
        dodgeAction.AddBinding("<Gamepad>/buttonEast");
    }

    public void Configure(float configuredMoveSpeed, float configuredRotationSpeed)
    {
        moveSpeed = Mathf.Max(0f, configuredMoveSpeed);
        rotationSpeed = Mathf.Max(0f, configuredRotationSpeed);
    }

    public void ApplyUpgrade(
        cfg.upgradeEnum effectType,
        float value,
        int stacks)
    {
        float totalValue = value * stacks;
        switch (effectType)
        {
            case cfg.upgradeEnum.dodge_cooldown:
                dodgeCooldown *= Mathf.Max(0.1f, 1f - totalValue);
                break;

            case cfg.upgradeEnum.move_speed:
                moveSpeed *= 1f + totalValue;
                break;

            default:
                Debug.LogError(
                    $"PlayerMovement：不支持应用强化效果 {effectType}。",
                    this
                );
                break;
        }
    }

    public void ConfigureDodge(float configuredDistance, float configuredCooldown, float configuredDuration)
    {
        dodgeConfigured = false;
        if (float.IsNaN(configuredDistance) || float.IsInfinity(configuredDistance) ||
            float.IsNaN(configuredCooldown) || float.IsInfinity(configuredCooldown) ||
            float.IsNaN(configuredDuration) || float.IsInfinity(configuredDuration) ||
            configuredDistance <= 0f || configuredCooldown < 0f || configuredDuration <= 0f)
        {
            Debug.LogError("PlayerMovement：闪避距离和持续时间必须大于 0，冷却时间不能小于 0，且都必须是有限数值。", this);
            return;
        }

        dodgeDistance = configuredDistance;
        dodgeCooldown = configuredCooldown;
        dodgeDuration = configuredDuration;
        dodgeConfigured = true;
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        mobileAttackAction?.Enable();
        dodgeAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        mobileAttackAction?.Disable();
        dodgeAction?.Disable();
        IsDodging = false;
        MovementAmount = 0f;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
            return;

        // 玩家死亡后不再执行移动或移动端攻击输入
        if (health != null && health.IsDead)
        {
            IsDodging = false;
            MovementAmount = 0f;
            return;
        }

        // 角色控制器不可用时不执行移动
        if (controller == null || !controller.enabled)
        {
            IsDodging = false;
            MovementAmount = 0f;
            return;
        }

        // 移动端攻击按钮按下时发布事件。
        // RoomCombatManager 只会在 InProgress 状态响应该事件。
        if (mobileAttackAction != null &&
            mobileAttackAction.WasPressedThisFrame())
        {
            MobileAttackRequested?.Invoke();
            if (Time.timeScale <= 0f)
                return;
        }

        if (attack != null && attack.isActiveAndEnabled && attack.IsAttacking)
        {
            MovementAmount = 0f;
            return;
        }

        Vector2 input = moveAction != null
            ? moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        input = Vector2.ClampMagnitude(input, 1f);

        // 根据当前相机方向计算移动方向，保留原有相机相对移动逻辑
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            right = cameraTransform.right;
            right.y = 0f;
            right.Normalize();
        }

        Vector3 move = forward * input.y + right * input.x;

        if (!IsDodging && Time.deltaTime > 0f && dodgeConfigured && Time.time >= nextDodgeTime &&
            dodgeAction.WasPressedThisFrame())
        {
            dodgeDirection = move.sqrMagnitude > 0.001f
                ? move.normalized
                : transform.forward;
            dodgeDirection.y = 0f;
            dodgeDirection.Normalize();
            if (dodgeDirection.sqrMagnitude > 0f)
            {
                IsDodging = true;
                dodgeElapsed = 0f;
                nextDodgeTime = Time.time + dodgeCooldown;
                DodgeStarted?.Invoke();
            }
        }

        if (IsDodging)
        {
            MovementAmount = 0f;
            float stepTime = Mathf.Min(Time.deltaTime, dodgeDuration - dodgeElapsed);
            float stepDistance = dodgeDistance * stepTime / dodgeDuration;
            Vector3 startPosition = transform.position;
            CollisionFlags flags = controller.Move(dodgeDirection * stepDistance);
            dodgeElapsed += stepTime;

            float actualDistance = Vector3.Dot(transform.position - startPosition, dodgeDirection);
            if (dodgeElapsed >= dodgeDuration ||
                ((flags & CollisionFlags.Sides) != 0 && actualDistance < stepDistance - 0.001f))
            {
                IsDodging = false;
            }

            return;
        }

        // 让角色朝移动方向转身
        if (move.sqrMagnitude > 0.001f &&
            (attack == null || attack.LastMeleeParryFacingFrame != Time.frameCount))
        {
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // 执行移动
        Vector3 positionBeforeMove = transform.position;
        controller.Move(move * moveSpeed * Time.deltaTime);
        Vector3 displacement = transform.position - positionBeforeMove;
        displacement.y = 0f;
        MovementAmount = moveSpeed > 0f && Time.deltaTime > 0f
            ? Mathf.Clamp01(displacement.magnitude / (moveSpeed * Time.deltaTime))
            : 0f;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        mobileAttackAction?.Dispose();
        dodgeAction?.Dispose();
    }
}
