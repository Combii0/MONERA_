using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TheCaveToTheProtector : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Collider2D transitionTrigger;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera sceneCamera;

    [Header("Puntos del Fade (0-1 sobre ancho del collider)")]
    [SerializeField, Range(0.05f, 1f)] private float musicFadeEnd = 0.25f;
    [SerializeField, Range(0.05f, 1f)] private float visualsFadeEnd = 0.75f;

    [Header("Black Screen (Boss_fader)")]
    [SerializeField] private SpriteRenderer blackScreenRenderer;
    [SerializeField] private string blackScreenChildName = "Black Screen";
    [SerializeField] private bool autoFindBlackScreen = true;
    [SerializeField] private bool forceBlackScreenOnTop = true;
    [SerializeField] private int playerOrderBoost = 100;
    [SerializeField] private bool hideOverlayWhenZero = true;

    private bool playerInside;
    private Color blackScreenBaseColor = Color.black;
    private bool blackScreenConfigured;
    private SpriteRenderer configuredBlackScreen;

    private bool playerSortingOverridden;
    private readonly List<PlayerRendererState> playerRendererStates = new List<PlayerRendererState>(8);

    private struct PlayerRendererState
    {
        public SpriteRenderer renderer;
        public int sortingLayerId;
        public int sortingOrder;
    }

    private void Reset()
    {
        transitionTrigger = GetComponent<Collider2D>();
        if (transitionTrigger != null) transitionTrigger.isTrigger = true;
    }

    private void Awake()
    {
        if (transitionTrigger == null) transitionTrigger = GetComponent<Collider2D>();
        if (transitionTrigger != null) transitionTrigger.isTrigger = true;

        if (playerTransform == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null) playerTransform = player.transform;
        }

        if (sceneCamera == null) sceneCamera = Camera.main;

        TryResolveBlackScreen();
        CachePlayerRenderers();
        ApplyProgress(0f);
    }

    private void OnDisable()
    {
        ApplyProgress(0f);
        RestorePlayerSorting();
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                playerTransform = player.transform;
                CachePlayerRenderers();
            }
        }

        float progress = 0f;
        if (playerInside && playerTransform != null && transitionTrigger != null)
        {
            Bounds bounds = transitionTrigger.bounds;
            float width = bounds.max.x - bounds.min.x;
            if (width > 0.0001f)
            {
                progress = Mathf.InverseLerp(bounds.min.x, bounds.max.x, playerTransform.position.x);
            }
        }

        ApplyProgress(progress);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other)) return;
        playerInside = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayerCollider(other)) return;
        playerInside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other)) return;
        playerInside = false;
        ApplyProgress(0f);
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null) return false;

        if (playerTransform != null && other.transform.IsChildOf(playerTransform))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerMovement>() != null;
    }

    private void ApplyProgress(float colliderProgress01)
    {
        float clampedProgress = Mathf.Clamp01(colliderProgress01);
        float musicBlend = Mathf.Clamp01(clampedProgress / Mathf.Max(0.001f, musicFadeEnd));
        float visualBlend = Mathf.Clamp01(clampedProgress / Mathf.Max(0.001f, visualsFadeEnd));

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetProtectorAudioBlend(musicBlend);
        }

        ApplyOverlay(visualBlend);
    }

    private void ApplyOverlay(float blend01)
    {
        if (!TryResolveBlackScreen()) return;

        if (hideOverlayWhenZero)
        {
            bool active = blend01 > 0.0001f;
            if (blackScreenRenderer.gameObject.activeSelf != active) blackScreenRenderer.gameObject.SetActive(active);
            if (!active)
            {
                RestorePlayerSorting();
                return;
            }
        }
        else if (!blackScreenRenderer.gameObject.activeSelf)
        {
            blackScreenRenderer.gameObject.SetActive(true);
        }

        Color color = blackScreenBaseColor;
        color.a = Mathf.Clamp01(blend01);
        blackScreenRenderer.color = color;

        if (blend01 > 0.0001f)
        {
            RaisePlayerSortingAboveOverlay();
        }
        else
        {
            RestorePlayerSorting();
        }
    }

    private bool TryResolveBlackScreen()
    {
        if (blackScreenRenderer == null && autoFindBlackScreen)
        {
            if (!string.IsNullOrEmpty(blackScreenChildName))
            {
                Transform namedChild = transform.Find(blackScreenChildName);
                if (namedChild != null) blackScreenRenderer = namedChild.GetComponent<SpriteRenderer>();
            }

            if (blackScreenRenderer == null)
            {
                SpriteRenderer[] children = GetComponentsInChildren<SpriteRenderer>(true);
                SpriteRenderer fallback = null;
                for (int i = 0; i < children.Length; i++)
                {
                    SpriteRenderer candidate = children[i];
                    if (candidate == null) continue;
                    if (candidate.transform == transform) continue;
                    if (fallback == null) fallback = candidate;

                    string candidateName = candidate.name.ToLowerInvariant();
                    if (candidateName.Contains("black"))
                    {
                        blackScreenRenderer = candidate;
                        break;
                    }
                }

                if (blackScreenRenderer == null) blackScreenRenderer = fallback;
            }
        }

        if (blackScreenRenderer == null)
        {
            return false;
        }

        if (blackScreenConfigured && configuredBlackScreen == blackScreenRenderer)
        {
            return true;
        }

        if (forceBlackScreenOnTop)
        {
            FindTopSorting(out int topLayerId, out int topOrder);
            blackScreenRenderer.sortingLayerID = topLayerId;
            blackScreenRenderer.sortingOrder = topOrder + 1;
        }

        blackScreenBaseColor = blackScreenRenderer.color;
        blackScreenBaseColor.r = 0f;
        blackScreenBaseColor.g = 0f;
        blackScreenBaseColor.b = 0f;

        if (!blackScreenRenderer.gameObject.activeSelf && !hideOverlayWhenZero)
        {
            blackScreenRenderer.gameObject.SetActive(true);
        }

        if (hideOverlayWhenZero)
        {
            Color color = blackScreenBaseColor;
            color.a = 0f;
            blackScreenRenderer.color = color;
        }

        configuredBlackScreen = blackScreenRenderer;
        blackScreenConfigured = true;
        return true;
    }

    private void CachePlayerRenderers()
    {
        playerRendererStates.Clear();
        playerSortingOverridden = false;

        if (playerTransform == null) return;

        SpriteRenderer[] renderers = playerTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null) continue;
            playerRendererStates.Add(new PlayerRendererState
            {
                renderer = renderer,
                sortingLayerId = renderer.sortingLayerID,
                sortingOrder = renderer.sortingOrder
            });
        }
    }

    private void RaisePlayerSortingAboveOverlay()
    {
        if (blackScreenRenderer == null) return;
        if (playerRendererStates.Count == 0) CachePlayerRenderers();
        if (playerRendererStates.Count == 0) return;
        if (playerSortingOverridden) return;

        for (int i = 0; i < playerRendererStates.Count; i++)
        {
            SpriteRenderer renderer = playerRendererStates[i].renderer;
            if (renderer == null) continue;

            renderer.sortingLayerID = blackScreenRenderer.sortingLayerID;
            renderer.sortingOrder = blackScreenRenderer.sortingOrder + playerOrderBoost + i;
        }

        playerSortingOverridden = true;
    }

    private void RestorePlayerSorting()
    {
        if (!playerSortingOverridden) return;

        for (int i = 0; i < playerRendererStates.Count; i++)
        {
            SpriteRenderer renderer = playerRendererStates[i].renderer;
            if (renderer == null) continue;

            renderer.sortingLayerID = playerRendererStates[i].sortingLayerId;
            renderer.sortingOrder = playerRendererStates[i].sortingOrder;
        }

        playerSortingOverridden = false;
    }

    private void FindTopSorting(out int topLayerId, out int topOrder)
    {
        topLayerId = SortingLayer.NameToID("Default");
        topOrder = 0;
        int topLayerValue = int.MinValue;

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;
            if (blackScreenRenderer != null && renderer == blackScreenRenderer) continue;
            if (playerTransform != null && renderer.transform.IsChildOf(playerTransform)) continue;

            int layerValue = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);

            if (layerValue > topLayerValue)
            {
                topLayerValue = layerValue;
                topLayerId = renderer.sortingLayerID;
                topOrder = renderer.sortingOrder;
            }
            else if (layerValue == topLayerValue && renderer.sortingOrder > topOrder)
            {
                topOrder = renderer.sortingOrder;
            }
        }
    }
}
