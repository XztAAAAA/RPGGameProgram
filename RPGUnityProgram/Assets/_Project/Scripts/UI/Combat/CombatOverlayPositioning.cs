using UnityEngine;

public sealed class CombatOverlayPositioning
{
    private const float FallbackHeight = 1.5f;

    private readonly Transform owner;
    private readonly SkinnedMeshRenderer[] skinnedRenderers;
    private readonly MeshRenderer[] meshRenderers;
    private readonly Collider[] colliders;
    private bool missingBoundsWarningLogged;

    public CombatOverlayPositioning(Transform owner)
    {
        this.owner = owner;
        skinnedRenderers = owner.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        meshRenderers = owner.GetComponentsInChildren<MeshRenderer>(true);
        colliders = owner.GetComponentsInChildren<Collider>(true);
    }

    public float GetModelTop(Camera camera)
    {
        float top = float.NegativeInfinity;
        bool hasModel = false;

        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                IncludeBounds(renderer.bounds, camera, ref top);
                hasModel = true;
            }
        }

        if (!hasModel)
        {
            foreach (MeshRenderer renderer in meshRenderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    IncludeBounds(renderer.bounds, camera, ref top);
                    hasModel = true;
                }
            }
        }

        if (!hasModel)
        {
            foreach (Collider collider in colliders)
            {
                if (collider != null && collider.enabled && !collider.isTrigger &&
                    collider.gameObject.activeInHierarchy)
                {
                    IncludeBounds(collider.bounds, camera, ref top);
                    hasModel = true;
                }
            }
        }

        if (!hasModel)
        {
            if (!missingBoundsWarningLogged)
            {
                Debug.LogWarning("CombatOverlayPositioning：敌人没有可用的模型或碰撞体，使用默认提示高度。", owner);
                missingBoundsWarningLogged = true;
            }
            top = ProjectHeight(owner.position + Vector3.up * FallbackHeight, camera);
        }

        return top;
    }

    public void PlaceAbove(RectTransform rect, Camera camera, float top, float gap)
    {
        Vector3 anchor = owner.position;
        float halfHeight = rect.rect.height * Mathf.Abs(rect.lossyScale.y) * 0.5f;
        if (camera == null)
        {
            rect.position = new Vector3(anchor.x, top + halfHeight + gap, anchor.z);
            return;
        }

        Vector3 viewportAnchor = camera.WorldToViewportPoint(anchor);
        if (viewportAnchor.z <= 0f)
            return;

        Vector3 offset = camera.WorldToViewportPoint(anchor + camera.transform.up * (halfHeight + gap));
        float margin = offset.y - viewportAnchor.y;
        viewportAnchor.y = Mathf.Max(viewportAnchor.y, top) + margin;
        rect.position = camera.ViewportToWorldPoint(viewportAnchor);
    }

    public static float ProjectHeight(Vector3 position, Camera camera)
    {
        if (camera == null)
            return position.y;

        Vector3 viewport = camera.WorldToViewportPoint(position);
        return viewport.z > 0f ? viewport.y : float.NegativeInfinity;
    }

    private static void IncludeBounds(Bounds bounds, Camera camera, ref float top)
    {
        Vector3 topCenter = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        top = Mathf.Max(top, ProjectHeight(topCenter, camera));
    }
}
