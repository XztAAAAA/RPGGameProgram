using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Attack))]
public class PlayerAttackInput : MonoBehaviour
{
    private Attack attack;

    private void Awake()
    {
        attack = GetComponent<Attack>();
    }

    private void Update()
    {
        bool attackPressed = false;

        if (Keyboard.current != null)
        {
            attackPressed |=
                Keyboard.current.jKey.wasPressedThisFrame;
        }

        if (Mouse.current != null)
        {
            attackPressed |=
                Mouse.current.leftButton.wasPressedThisFrame;
        }

        if (attackPressed)
            attack.TryAttack();
    }
}