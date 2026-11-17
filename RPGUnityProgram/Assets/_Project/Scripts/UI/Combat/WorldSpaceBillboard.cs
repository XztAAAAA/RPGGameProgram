using UnityEngine;

[DefaultExecutionOrder(100)]
public class WorldSpaceBillboard : MonoBehaviour
{
    private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        transform.rotation = targetCamera.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
    }
}