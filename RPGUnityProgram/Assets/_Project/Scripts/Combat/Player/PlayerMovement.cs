using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家移动输入。
/// 支持 PC WASD 和移动端 OnScreenStick 虚拟摇杆。
/// 同时读取移动端攻击按钮，并通过 GameEventBus 请求攻击。
/// PC 键盘/鼠标攻击仍由现有 PlayerAttackInput 负责。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移动设置")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("参考相机")]
    [SerializeField] private Transform cameraTransform;

    private CharacterController controller;
    private Health health;

    // WASD + 虚拟 Gamepad 左摇杆
    private InputAction moveAction;

    // 仅负责移动端虚拟攻击按钮，避免与现有 PC PlayerAttackInput 重复
    private InputAction mobileAttackAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();

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
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        mobileAttackAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        mobileAttackAction?.Disable();
    }

    private void Update()
    {
        // 玩家死亡后不再执行移动或移动端攻击输入
        if (health != null && health.IsDead)
            return;

        // 角色控制器不可用时不执行移动
        if (controller == null || !controller.enabled)
            return;

        // 移动端攻击按钮按下时发布事件。
        // RoomCombatManager 只会在 InProgress 状态响应该事件。
        if (mobileAttackAction != null &&
            mobileAttackAction.WasPressedThisFrame())
        {
            GameEventBus.Publish(new PlayerMobileAttackRequestedEvent());
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

        // 让角色朝移动方向转身
        if (move.sqrMagnitude > 0.001f)
            transform.forward = move;

        // 执行移动
        controller.Move(move * moveSpeed * Time.deltaTime);
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        mobileAttackAction?.Dispose();
    }
}
