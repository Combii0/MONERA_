using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Chest : MonoBehaviour
{
    [Header("Cofre")]
    [SerializeField, HideInInspector] private SpriteRenderer chestRenderer;
    [SerializeField, HideInInspector] private Collider2D triggerCollider;
    [SerializeField, HideInInspector] private Sprite closedSprite;
    [SerializeField] private Sprite openedSprite;
    [SerializeField] private int lifeReward = 1;
    [SerializeField, HideInInspector] private bool requirePlayerToPassAbove = true;
    [SerializeField, HideInInspector] private float minPlayerYFromChestCenter = -0.05f;

    [Header("Visual Corazon")]
    [SerializeField] private GameObject heartRewardPrefab;
    [SerializeField] private Vector3 heartRewardSpawnOffset = new Vector3(0f, 0.4f, 0f);
    [SerializeField] private Vector3 heartRewardRiseOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField, Min(0f)] private float heartRewardFadeInDuration = 0.18f;
    [SerializeField, Min(0f)] private float heartRewardHoldDuration = 0.16f;
    [SerializeField, Min(0.01f)] private float heartRewardFadeOutDuration = 0.32f;
    [SerializeField] private int heartRewardSortingOrderBoost = 3;

    private bool isOpened;

    private void Awake()
    {
        if (chestRenderer == null)
        {
            chestRenderer = GetComponent<SpriteRenderer>();
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (closedSprite == null && chestRenderer != null)
        {
            closedSprite = chestRenderer.sprite;
        }

        if (chestRenderer != null && closedSprite != null)
        {
            chestRenderer.sprite = closedSprite;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryOpen(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryOpen(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryOpen(collision.collider);
    }

    private void TryOpen(Collider2D other)
    {
        if (isOpened || other == null) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        if (requirePlayerToPassAbove)
        {
            float minY = transform.position.y + minPlayerYFromChestCenter;
            if (player.transform.position.y < minY)
            {
                // Allow opening from near side contact to avoid strict pivot-dependent misses.
                float horizontalDistance = Mathf.Abs(player.transform.position.x - transform.position.x);
                if (horizontalDistance > 0.95f) return;
            }
        }

        OpenChest();
    }

    private void OpenChest()
    {
        isOpened = true;
        if (chestRenderer != null && openedSprite != null)
        {
            chestRenderer.sprite = openedSprite;
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        if (heartRewardPrefab != null)
        {
            StartCoroutine(PlayHeartRewardVisual());
        }

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            gm = FindFirstObjectByType<GameManager>();
        }

        if (gm != null)
        {
            gm.TryAddLife(Mathf.Max(1, lifeReward));
        }
    }

    private IEnumerator PlayHeartRewardVisual()
    {
        Vector3 startPosition = transform.position + heartRewardSpawnOffset;
        GameObject heartObject = Instantiate(heartRewardPrefab, startPosition, Quaternion.identity);
        if (heartObject == null) yield break;

        SpriteRenderer[] renderers = heartObject.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Destroy(heartObject);
            yield break;
        }

        Color[] baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null) continue;

            baseColors[i] = renderer.color;
            Color color = baseColors[i];
            color.a = 0f;
            renderer.color = color;

            if (chestRenderer != null)
            {
                renderer.sortingLayerID = chestRenderer.sortingLayerID;
                renderer.sortingOrder = chestRenderer.sortingOrder + heartRewardSortingOrderBoost + i;
            }
        }

        float fadeInDuration = Mathf.Max(0f, heartRewardFadeInDuration);
        float holdDuration = Mathf.Max(0f, heartRewardHoldDuration);
        float fadeOutDuration = Mathf.Max(0.01f, heartRewardFadeOutDuration);
        float totalDuration = fadeInDuration + holdDuration + fadeOutDuration;
        Vector3 endPosition = startPosition + heartRewardRiseOffset;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float positionT = SmoothStep01(elapsed / Mathf.Max(0.0001f, totalDuration));
            heartObject.transform.position = Vector3.Lerp(startPosition, endPosition, positionT);

            float alpha = ResolveHeartRewardAlpha(elapsed, fadeInDuration, holdDuration, fadeOutDuration);
            ApplyHeartRewardAlpha(renderers, baseColors, alpha);
            yield return null;
        }

        ApplyHeartRewardAlpha(renderers, baseColors, 0f);
        Destroy(heartObject);
    }

    private static void ApplyHeartRewardAlpha(SpriteRenderer[] renderers, Color[] baseColors, float alpha)
    {
        float safeAlpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null) continue;

            Color color = i < baseColors.Length ? baseColors[i] : renderer.color;
            color.a = safeAlpha;
            renderer.color = color;
        }
    }

    private static float ResolveHeartRewardAlpha(float elapsed, float fadeInDuration, float holdDuration, float fadeOutDuration)
    {
        if (fadeInDuration > 0f && elapsed <= fadeInDuration)
        {
            return elapsed / fadeInDuration;
        }

        if (elapsed <= fadeInDuration + holdDuration)
        {
            return 1f;
        }

        float fadeOutElapsed = elapsed - fadeInDuration - holdDuration;
        return 1f - (fadeOutElapsed / Mathf.Max(0.0001f, fadeOutDuration));
    }

    private static float SmoothStep01(float t)
    {
        float clamped = Mathf.Clamp01(t);
        return clamped * clamped * (3f - (2f * clamped));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (chestRenderer == null)
        {
            chestRenderer = GetComponent<SpriteRenderer>();
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (closedSprite == null && chestRenderer != null)
        {
            closedSprite = chestRenderer.sprite;
        }

        if (openedSprite == null)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Spritesheets/chest.png");
            for (int i = 0; i < assets.Length; i++)
            {
                Sprite sprite = assets[i] as Sprite;
                if (sprite == null) continue;
                if (sprite.name == "chest_1")
                {
                    openedSprite = sprite;
                    break;
                }
            }
        }
    }
#endif
}
