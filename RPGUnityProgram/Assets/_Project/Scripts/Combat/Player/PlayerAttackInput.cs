using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;

[RequireComponent(typeof(Attack))]
[DefaultExecutionOrder(-50)]
public class PlayerAttackInput : MonoBehaviour
{
    private const string ParryGamepadControl = "<Gamepad>/buttonNorth";

    private Attack attack;
    private InputAction parryAction;

    public bool IsParryPressedThisFrame =>
        isActiveAndEnabled && parryAction != null && parryAction.WasPressedThisFrame();

    private void Awake()
    {
        attack = GetComponent<Attack>();
        parryAction = new InputAction("PlayerParry", InputActionType.Button);
        parryAction.AddBinding("<Keyboard>/k");
        parryAction.AddBinding(ParryGamepadControl);
    }

    private void OnEnable()
    {
        parryAction?.Enable();
    }

    private void OnDisable()
    {
        parryAction?.Disable();
    }

    private void Update()
    {
        bool mousePressed = Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame;
        bool parryButtonClicked = mousePressed && IsMouseOverParryButton();
        bool attackPressed = !parryButtonClicked && (
            (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame) ||
            mousePressed);
        ProcessInput(IsParryPressedThisFrame, attackPressed);
    }

    private void ProcessInput(bool parryPressed, bool attackPressed)
    {
        if (parryPressed)
            attack.TryParry();
        else if (attackPressed)
            attack.TryAttack();
    }

    private static bool IsMouseOverParryButton()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointer = new PointerEventData(eventSystem)
        {
            position = Mouse.current.position.ReadValue()
        };
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, results);

        foreach (RaycastResult result in results)
        {
            OnScreenButton button = result.gameObject.GetComponentInParent<OnScreenButton>();
            if (button != null && button.isActiveAndEnabled && button.controlPath == ParryGamepadControl)
                return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        parryAction?.Dispose();
    }
}