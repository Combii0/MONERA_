using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
public class LetterTrigger : MonoBehaviour
{
    [SerializeField, HideInInspector] private Collider2D triggerCollider;

    [Header("Interaccion")]
    [SerializeField] private bool requireInteractKey = true;
    [SerializeField, HideInInspector] private KeyCode interactKey = KeyCode.E;
    [SerializeField, HideInInspector] private Vector3 promptOffset = new Vector3(0f, 1.05f, 0f);
    [SerializeField, HideInInspector] private float promptFontSize = 1.4f;
    [SerializeField, HideInInspector] private int promptSortingOrderBoost = 60;
    [SerializeField, HideInInspector] private TMP_FontAsset promptFont;
    [SerializeField, HideInInspector] private float promptPopDuration = 0.16f;
    [SerializeField, HideInInspector] private float promptPulseAmplitude = 0.045f;
    [SerializeField, HideInInspector] private float promptPulseSpeed = 7.5f;

    private PlayerMovement playerInRange;
    private GameObject promptRoot;
    private TextMeshPro promptText;
    private SpriteRenderer sourceRenderer;
    private GameManager cachedGameManager;
    private bool promptSuppressedByStoryMessage;
    private float promptAnimTimer;

    private const float DefaultPromptFontSize = 1.4f;

    private void Awake()
    {
        NormalizePromptSettings();
        EnsureTriggerCollider();
        sourceRenderer = GetComponentInParent<SpriteRenderer>();
        EnsurePromptVisual();
        SetPromptVisible(false);
        CacheGameManager();
    }

    private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => RegisterPlayer(other);
    private void OnCollisionEnter2D(Collision2D collision) => RegisterPlayer(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => RegisterPlayer(collision.collider);
    private void OnTriggerExit2D(Collider2D other) => UnregisterPlayer(other);
    private void OnCollisionExit2D(Collision2D collision) => UnregisterPlayer(collision.collider);

    private void Update()
    {
        if (playerInRange == null) return;

        if (promptRoot != null)
        {
            promptRoot.transform.localPosition = promptOffset;
        }

        if (IsStoryMessageActive())
        {
            promptSuppressedByStoryMessage = true;
            SetPromptVisible(false);
            return;
        }

        if (promptSuppressedByStoryMessage)
        {
            promptSuppressedByStoryMessage = false;
        }

        SetPromptVisible(true);
        UpdatePromptAnimation();

        if (!requireInteractKey || WasInteractPressedThisFrame())
        {
            TryTriggerLevelEnd(playerInRange);
        }
    }

    private void RegisterPlayer(Collider2D other)
    {
        if (other == null) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        playerInRange = player;

        if (IsStoryMessageActive())
        {
            promptSuppressedByStoryMessage = true;
            SetPromptVisible(false);
            return;
        }

        SetPromptVisible(true);
    }

    private void UnregisterPlayer(Collider2D other)
    {
        if (other == null || playerInRange == null) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || player != playerInRange) return;

        playerInRange = null;
        promptSuppressedByStoryMessage = false;
        SetPromptVisible(false);
    }

    private void TryTriggerLevelEnd(PlayerMovement player)
    {
        if (player == null) return;

        CacheGameManager();
        if (cachedGameManager == null) return;
        if (!cachedGameManager.TriggerLetterEnding(transform)) return;

        playerInRange = null;
        promptSuppressedByStoryMessage = true;
        SetPromptVisible(false);
    }

    private void CacheGameManager()
    {
        if (cachedGameManager != null) return;
        cachedGameManager = GameManager.Instance;
        if (cachedGameManager == null)
        {
            cachedGameManager = FindFirstObjectByType<GameManager>();
        }
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void EnsurePromptVisual()
    {
        CachePromptReferencesFromHierarchy();

        if (promptRoot == null)
        {
            promptRoot = new GameObject("InteractPrompt_E");
            promptRoot.transform.SetParent(transform, false);
        }

        promptRoot.transform.localPosition = promptOffset;

        SpriteRenderer rootSprite = promptRoot.GetComponent<SpriteRenderer>();
        if (rootSprite != null) rootSprite.enabled = false;

        if (promptText == null)
        {
            Transform label = promptRoot.transform.Find("Label");
            if (label != null)
            {
                promptText = label.GetComponent<TextMeshPro>();
            }
        }

        if (promptText == null)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(promptRoot.transform, false);
            promptText = labelObj.AddComponent<TextMeshPro>();
        }

        promptText.text = "E";
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = Mathf.Max(0.8f, promptFontSize);
        promptText.color = Color.white;
        promptText.fontStyle = FontStyles.Bold;
        promptText.textWrappingMode = TextWrappingModes.NoWrap;
        promptText.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        RectTransform textRect = promptText.rectTransform;
        textRect.sizeDelta = new Vector2(2f, 2f);

        TMP_FontAsset resolvedPromptFont = ResolvePromptFont();
        if (resolvedPromptFont != null)
        {
            promptText.enabled = true;
            promptText.font = resolvedPromptFont;
            if (resolvedPromptFont.material != null)
            {
                promptText.fontSharedMaterial = resolvedPromptFont.material;
            }
        }
        else
        {
            promptText.enabled = false;
        }

        MeshRenderer textRenderer = promptText.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            int baseOrder = promptSortingOrderBoost;
            if (sourceRenderer != null)
            {
                textRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                baseOrder = sourceRenderer.sortingOrder + promptSortingOrderBoost;
            }
            textRenderer.sortingOrder = baseOrder;
        }
    }

    private void CachePromptReferencesFromHierarchy()
    {
        if (promptRoot == null)
        {
            Transform existingRoot = transform.Find("InteractPrompt_E");
            if (existingRoot != null)
            {
                promptRoot = existingRoot.gameObject;
            }
        }

        if (promptText == null && promptRoot != null)
        {
            Transform label = promptRoot.transform.Find("Label");
            if (label != null)
            {
                promptText = label.GetComponent<TextMeshPro>();
            }
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot == null) return;

        bool wasVisible = promptRoot.activeSelf;
        if (visible)
        {
            if (!wasVisible)
            {
                promptAnimTimer = 0f;
                promptRoot.transform.localScale = Vector3.one * 0.72f;
                promptRoot.SetActive(true);
            }
            return;
        }

        if (wasVisible)
        {
            promptRoot.SetActive(false);
        }
        promptAnimTimer = 0f;
        promptRoot.transform.localScale = Vector3.one;
    }

    private void UpdatePromptAnimation()
    {
        if (promptRoot == null || !promptRoot.activeSelf) return;

        promptAnimTimer += Time.unscaledDeltaTime;

        float popDuration = Mathf.Max(0.05f, promptPopDuration);
        float popT = Mathf.Clamp01(promptAnimTimer / popDuration);
        float popScale = Mathf.Lerp(0.72f, 1f, popT);

        float pulseScale = 1f;
        if (popT >= 0.999f)
        {
            float amplitude = Mathf.Clamp(promptPulseAmplitude, 0f, 0.35f);
            float speed = Mathf.Max(0.1f, promptPulseSpeed);
            pulseScale += Mathf.Sin(promptAnimTimer * speed) * amplitude;
        }

        float finalScale = Mathf.Max(0.01f, popScale * pulseScale);
        promptRoot.transform.localScale = new Vector3(finalScale, finalScale, 1f);
    }

    private bool IsStoryMessageActive()
    {
        CacheGameManager();
        return cachedGameManager != null && cachedGameManager.IsStoryMessageVisible;
    }

    private TMP_FontAsset ResolvePromptFont()
    {
        if (promptFont != null) return promptFont;

        promptFont = TMP_Settings.defaultFontAsset;
        if (promptFont == null)
        {
            promptFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        return promptFont;
    }

    private bool WasInteractPressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (interactKey == KeyCode.E) pressed = Keyboard.current.eKey.wasPressedThisFrame;
            else if (interactKey == KeyCode.F) pressed = Keyboard.current.fKey.wasPressedThisFrame;
            else if (interactKey == KeyCode.Return) pressed = Keyboard.current.enterKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(interactKey);
#endif

        return pressed;
    }

    private void NormalizePromptSettings()
    {
        if (promptFontSize <= 0f) promptFontSize = DefaultPromptFontSize;
        promptFontSize = Mathf.Clamp(promptFontSize, 0.8f, 1.8f);
        promptPopDuration = Mathf.Max(0.05f, promptPopDuration);
        promptPulseAmplitude = Mathf.Clamp(promptPulseAmplitude, 0f, 0.35f);
        promptPulseSpeed = Mathf.Max(0.1f, promptPulseSpeed);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizePromptSettings();
        EnsureTriggerCollider();
        CachePromptReferencesFromHierarchy();

        if (promptText != null)
        {
            promptText.fontSize = Mathf.Max(0.8f, promptFontSize);
            TMP_FontAsset resolvedPromptFont = ResolvePromptFont();
            if (resolvedPromptFont != null)
            {
                promptText.enabled = true;
                promptText.font = resolvedPromptFont;
                if (resolvedPromptFont.material != null)
                {
                    promptText.fontSharedMaterial = resolvedPromptFont.material;
                }
            }
            else
            {
                promptText.enabled = false;
            }
        }

        if (promptRoot != null)
        {
            promptRoot.transform.localPosition = promptOffset;
        }
    }
#endif
}
