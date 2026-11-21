using UnityEngine;

public sealed class CombatFeedback : MonoBehaviour
{
    private const int RingSegments = 32;
    private const int SparkCount = 8;
    private const float Duration = 0.3f;

    private readonly LineRenderer[] sparks = new LineRenderer[SparkCount];
    private LineRenderer ring;
    private Material material;
    private Color color;
    private float size;
    private float elapsed;

    public static void ShowHit(Vector3 position)
    {
        Create(position, new Color(1f, 0.75f, 0.35f), 0.45f);
    }

    public static void ShowParry(Vector3 position)
    {
        Create(position, new Color(0.55f, 0.9f, 1f), 0.8f);
    }

    private static void Create(Vector3 position, Color color, float size)
    {
        Shader shader = Resources.Load<Shader>("Shaders/AttackRange");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("CombatFeedback：无法加载战斗反馈着色器。");
            return;
        }

        GameObject effect = new GameObject("Combat Feedback");
        effect.transform.position = position;
        CombatFeedback feedback = effect.AddComponent<CombatFeedback>();
        feedback.Initialize(shader, color, size);
    }

    private void Initialize(Shader shader, Color effectColor, float effectSize)
    {
        color = effectColor;
        size = effectSize;
        material = new Material(shader);
        ring = CreateLine("Impact Ring", RingSegments, true);
        for (int i = 0; i < sparks.Length; i++)
            sparks[i] = CreateLine("Impact Spark", 2, false);

        UpdateGeometry();
    }

    private LineRenderer CreateLine(string lineName, int count, bool loop)
    {
        GameObject lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = count;
        line.sharedMaterial = material;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= Duration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateGeometry();
    }

    private void UpdateGeometry()
    {
        Camera camera = Camera.main;
        if (camera != null)
            transform.rotation = camera.transform.rotation;

        float progress = Mathf.Clamp01(elapsed / Duration);
        float radius = size * Mathf.Lerp(0.15f, 1f, progress);
        Color fadedColor = color;
        fadedColor.a = 1f - progress;
        ring.startColor = ring.endColor = fadedColor;
        ring.widthMultiplier = Mathf.Lerp(0.065f, 0.01f, progress);
        for (int i = 0; i < RingSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / RingSegments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }

        for (int i = 0; i < sparks.Length; i++)
        {
            float angle = 2f * Mathf.PI * i / SparkCount;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            sparks[i].startColor = sparks[i].endColor = fadedColor;
            sparks[i].widthMultiplier = Mathf.Lerp(0.05f, 0.005f, progress);
            sparks[i].SetPosition(0, direction * radius * 0.75f);
            sparks[i].SetPosition(1, direction * (radius + size * 0.3f * (1f - progress)));
        }
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}
