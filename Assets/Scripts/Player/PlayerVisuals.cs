using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerVisuals : MonoBehaviour
{
    [Header("Color Adaptativo")]
    [SerializeField] private bool enableAdaptiveTint = true;
    [SerializeField] private float tintSampleRadius = 2.25f;
    [SerializeField, Range(0f, 1f)] private float tintInfluence = 0.35f;
    [SerializeField, Range(0f, 1f)] private float pastelAmount = 0.45f;
    [SerializeField, Range(0f, 1f)] private float ambientInfluence = 0.25f;
    [SerializeField] private LayerMask tintSampleLayers = ~0;
    [SerializeField] private float tintUpdateInterval = 0.08f;
    [SerializeField] private float tintSmoothing = 7f;
    [SerializeField] private int tintMaxSamples = 20;
    [SerializeField] private Color tintFallbackColor = Color.white;

    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;

    private float tintTimer;
    private Color baseSpriteColor;
    private Color targetSpriteColor;
    private Collider2D[] tintSampleBuffer;
    private ContactFilter2D tintContactFilter;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();

        baseSpriteColor = spriteRenderer.color;
        targetSpriteColor = baseSpriteColor;

        SetupAdaptiveTint();
    }

    private void Update()
    {
        UpdateAdaptiveTint();
    }

    private void SetupAdaptiveTint()
    {
        tintMaxSamples = Mathf.Max(4, tintMaxSamples);
        tintSampleBuffer = new Collider2D[tintMaxSamples];
        tintContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        tintContactFilter.SetLayerMask(tintSampleLayers.value == 0 ? Physics2D.DefaultRaycastLayers : tintSampleLayers.value);
    }

    private void UpdateAdaptiveTint()
    {
        if (!enableAdaptiveTint)
        {
            targetSpriteColor = baseSpriteColor;
            ApplyTintSmoothing();
            return;
        }

        tintTimer -= Time.deltaTime;
        if (tintTimer <= 0f)
        {
            targetSpriteColor = CalculateAdaptiveTintColor();
            tintTimer = Mathf.Max(0.02f, tintUpdateInterval);
        }

        ApplyTintSmoothing();
    }

    private void ApplyTintSmoothing()
    {
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, tintSmoothing) * Time.deltaTime);
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetSpriteColor, t);
    }

    private Color CalculateAdaptiveTintColor()
    {
        if (tintSampleRadius <= 0.01f || tintInfluence <= 0f) return baseSpriteColor;

        int desiredBufferSize = Mathf.Max(4, tintMaxSamples);
        if (tintSampleBuffer == null || tintSampleBuffer.Length != desiredBufferSize)
        {
            tintSampleBuffer = new Collider2D[desiredBufferSize];
        }

        tintContactFilter.SetLayerMask(tintSampleLayers.value == 0 ? Physics2D.DefaultRaycastLayers : tintSampleLayers.value);
        int hits = Physics2D.OverlapCircle((Vector2)transform.position, tintSampleRadius, tintContactFilter, tintSampleBuffer);

        Vector3 weightedColor = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < hits; i++)
        {
            Collider2D hit = tintSampleBuffer[i];
            if (hit == null || hit.attachedRigidbody == rb || hit.transform == transform) continue;

            SpriteRenderer nearbySprite = hit.GetComponent<SpriteRenderer>() ?? hit.GetComponentInParent<SpriteRenderer>();
            if (nearbySprite == null || nearbySprite == spriteRenderer || !nearbySprite.enabled) continue;

            float distance = Vector2.Distance(transform.position, hit.ClosestPoint(transform.position));
            float distanceWeight = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, tintSampleRadius));
            float alphaWeight = Mathf.Clamp01(nearbySprite.color.a);
            if (distanceWeight <= 0f || alphaWeight <= 0.01f) continue;

            float weight = distanceWeight * alphaWeight;
            Color c = nearbySprite.color;
            weightedColor += new Vector3(c.r, c.g, c.b) * weight;
            totalWeight += weight;
        }

        Color sampledColor = totalWeight > 0.0001f
            ? new Color(weightedColor.x / totalWeight, weightedColor.y / totalWeight, weightedColor.z / totalWeight, 1f)
            : tintFallbackColor;

        sampledColor = Color.Lerp(sampledColor, RenderSettings.ambientLight, Mathf.Clamp01(ambientInfluence));
        Color pastelColor = ToPastel(sampledColor, pastelAmount);

        Color finalColor = Color.Lerp(baseSpriteColor, pastelColor, Mathf.Clamp01(tintInfluence));
        finalColor.a = baseSpriteColor.a;
        return finalColor;
    }

    private static Color ToPastel(Color sourceColor, float strength)
    {
        Color.RGBToHSV(sourceColor, out float h, out float s, out float v);
        strength = Mathf.Clamp01(strength);

        s = Mathf.Lerp(s, s * 0.35f, strength);
        v = Mathf.Lerp(v, Mathf.Clamp01(v + 0.25f), strength);

        Color pastel = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
        float luminance = (sourceColor.r * 0.2126f) + (sourceColor.g * 0.7152f) + (sourceColor.b * 0.0722f);
        float luminanceBoost = Mathf.Lerp(0.92f, 1.08f, luminance);

        pastel.r = Mathf.Clamp01(pastel.r * luminanceBoost);
        pastel.g = Mathf.Clamp01(pastel.g * luminanceBoost);
        pastel.b = Mathf.Clamp01(pastel.b * luminanceBoost);
        return pastel;
    }
}