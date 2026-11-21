using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(110)]
public sealed class ParryWarning : MonoBehaviour
{
    private const float ScreenGap = 0.15f;

    private CombatOverlayPositioning positioning;
    private RectTransform healthBar;
    private RectTransform warningRect;
    private readonly Vector3[] corners = new Vector3[4];

    public static GameObject Create(Transform owner)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts/Source/NotoSansSC-VariableFont_wght SDF");
        if (font == null)
        {
            Debug.LogError("ParryWarning：无法加载格挡提示的中文字体。", owner);
            return null;
        }

        if (!font.HasCharacters("格挡！", out uint[] missingCharacters, true, true))
        {
            Debug.LogError("ParryWarning：中文字体缺少格挡提示所需的字符。", owner);
            return null;
        }

        HealthBar healthBar = owner.GetComponentInChildren<HealthBar>(true);

        GameObject warning = new GameObject("Parry Warning", typeof(RectTransform), typeof(Canvas),
            typeof(WorldSpaceBillboard), typeof(ParryWarning));
        RectTransform root = warning.GetComponent<RectTransform>();
        root.SetParent(owner, false);
        root.localScale = Vector3.one * 0.01f;
        root.sizeDelta = new Vector2(180f, 50f);
        ParryWarning warningComponent = warning.GetComponent<ParryWarning>();
        warningComponent.positioning = new CombatOverlayPositioning(owner);
        warningComponent.healthBar = healthBar != null ? healthBar.transform as RectTransform : null;
        warningComponent.warningRect = root;
        warningComponent.UpdatePosition();

        Canvas canvas = warning.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(root, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localRotation = Quaternion.Euler(0f, 180f, 0f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = "格挡！";
        text.fontSize = 42f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.85f, 0.15f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        return warning;
    }

    private void LateUpdate()
    {
        UpdatePosition();
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
