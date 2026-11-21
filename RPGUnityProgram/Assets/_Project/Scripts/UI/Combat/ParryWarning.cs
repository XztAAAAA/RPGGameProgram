using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(110)]
public sealed class ParryWarning : MonoBehaviour
{
    private const float ScreenGap = 0.15f;
    private const float FlashFrequency = 3f;

    private CombatOverlayPositioning positioning;
    private RectTransform healthBar;
    private RectTransform warningRect;
    private CanvasGroup flashGroup;
    private float flashStartTime;
    private readonly Vector3[] corners = new Vector3[4];

    public static GameObject Create(Transform owner)
    {
        HealthBar healthBar = owner.GetComponentInChildren<HealthBar>(true);

        GameObject warning = new GameObject("Parry Warning", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),
            typeof(WorldSpaceBillboard), typeof(ParryWarning));
        RectTransform root = warning.GetComponent<RectTransform>();
        root.SetParent(owner, false);
        root.localScale = Vector3.one * 0.01f;
        root.sizeDelta = new Vector2(56f, 52f);
        ParryWarning warningComponent = warning.GetComponent<ParryWarning>();
        warningComponent.positioning = new CombatOverlayPositioning(owner);
        warningComponent.healthBar = healthBar != null ? healthBar.transform as RectTransform : null;
        warningComponent.warningRect = root;
        warningComponent.flashGroup = warning.GetComponent<CanvasGroup>();
        warningComponent.flashGroup.blocksRaycasts = false;
        warningComponent.flashGroup.interactable = false;
        warningComponent.UpdatePosition();

        Canvas canvas = warning.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        GameObject flashObject = new GameObject("Flash", typeof(RectTransform));
        RectTransform flashRect = flashObject.GetComponent<RectTransform>();
        flashRect.SetParent(root, false);
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
        flashRect.localRotation = Quaternion.Euler(0f, 180f, 0f);
        GameObject triangleObject = new GameObject("Triangle", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(ParryWarningTriangle));
        RectTransform triangleRect = triangleObject.GetComponent<RectTransform>();
        triangleRect.SetParent(flashRect, false);
        triangleRect.anchorMin = triangleRect.anchorMax = new Vector2(0.5f, 0.5f);
        triangleRect.sizeDelta = new Vector2(48f, 44f);
        ParryWarningTriangle triangle = triangleObject.GetComponent<ParryWarningTriangle>();
        triangle.color = new Color(1f, 0.08f, 0.08f);
        triangle.raycastTarget = false;

        CreateFlashPart(flashRect, "Exclamation", new Vector2(5f, 14f), new Vector2(0f, -1f));
        CreateFlashPart(flashRect, "Dot", new Vector2(5f, 5f), new Vector2(0f, -12f));
        return warning;
    }

    private static void CreateFlashPart(
        RectTransform parent, string partName, Vector2 size, Vector2 position)
    {
        GameObject part = new GameObject(partName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = part.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = part.GetComponent<Image>();
        image.color = new Color(1f, 0.85f, 0.85f);
        image.raycastTarget = false;
    }

    public sealed class ParryWarningTriangle : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = new Vector3(rect.xMin, rect.yMin);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.center.x, rect.yMax);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMin);
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(0, 1, 2);
        }
    }

    private void OnEnable()
    {
        flashStartTime = Time.time;
    }

    private void LateUpdate()
    {
        UpdatePosition();
        float pulse = 0.5f + 0.5f * Mathf.Cos((Time.time - flashStartTime) * FlashFrequency * 2f * Mathf.PI);
        flashGroup.alpha = Mathf.Lerp(0.6f, 1f, pulse);
    }

    private void UpdatePosition()
    {
        Camera camera = Camera.main;
        float top = positioning.GetModelTop(camera);

        if (healthBar != null && healthBar.gameObject.activeInHierarchy)
        {
            healthBar.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
                top = Mathf.Max(top, CombatOverlayPositioning.ProjectHeight(corner, camera));
        }

        positioning.PlaceAbove(warningRect, camera, top, ScreenGap);
    }
}
