using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
public class Letter : MonoBehaviour
{
    [SerializeField] private Collider2D letterCollider;
    
    [Tooltip("Desmarca esto para que el jugador pueda leer el cartel desde cualquier altura")]
    [SerializeField] private bool requirePlayerAbove = false;
    [SerializeField] private float minPlayerYFromLetterCenter = -0.05f;
    
    [Header("TEXTO DE ESTA CARTA")]
    [TextArea(3, 6)] 
    [Tooltip("Escribe aquí lo que dirá esta carta en específico.")]
    public string screenMessage = "Escribe tu texto aqui...";

    [Header("Interaccion")]
    [SerializeField] private bool requireInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.05f, 0f);
    [SerializeField] private float promptFontSize = 1.4f;
    [SerializeField] private int promptSortingOrderBoost = 60;

    [Header("Animacion Prompt")]
    [SerializeField] private float promptShowDuration = 0.12f;
    [SerializeField] private float promptHideDuration = 0.14f;
    [SerializeField] private float promptPopScale = 1.08f;
    [SerializeField] private float promptIdleFloatAmplitude = 0.03f;
    [SerializeField] private float promptIdleFloatSpeed = 4.2f;

    private PlayerMovement playerInRange;
    private GameObject promptRoot;
    private TextMeshPro promptText;
    private SpriteRenderer sourceRenderer;
    private bool promptTargetVisible;
    private float promptVisibility;

    private const float DefaultPromptFontSize = 1.4f;
    private const float DefaultShowDuration = 0.12f;
    private const float DefaultHideDuration = 0.14f;

    private void Awake()
    {
        NormalizePromptSettings();
        EnsureLetterCollider();
        sourceRenderer = GetComponentInParent<SpriteRenderer>();
        EnsurePromptVisual();
        promptVisibility = 0f;
        promptTargetVisible = false;
        SetPromptVisible(false);
        UpdatePromptAnimation(true);
    }

    private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => RegisterPlayer(other);
    private void OnCollisionEnter2D(Collision2D collision) => RegisterPlayer(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => RegisterPlayer(collision.collider);
    private void OnTriggerExit2D(Collider2D other) => UnregisterPlayer(other);
    private void OnCollisionExit2D(Collision2D collision) => UnregisterPlayer(collision.collider);

    private void Update()
    {
        if (playerInRange != null)
        {
            if (!CanInteractWithPlayer(playerInRange))
            {
                playerInRange = null;
                SetPromptVisible(false);
            }
            else
            {
                // Verifica si ya hay una carta abierta
                bool isReading = LetterManager.Instance != null && LetterManager.Instance.IsReading;

                if (isReading)
                {
                    // Si el manager está leyendo, ocultamos la letra "E" y no leemos el Input
                    SetPromptVisible(false);
                }
                else
                {
                    // Si no está leyendo, mostramos el prompt y esperamos el Input
                    SetPromptVisible(true);
                    if (promptRoot != null) promptRoot.transform.localPosition = promptOffset;

                    if (!requireInteractKey || WasInteractPressedThisFrame())
                    {
                        TryTriggerEnding(playerInRange);
                    }
                }
            }
        }

        UpdatePromptAnimation();
    }

    private void RegisterPlayer(Collider2D other)
    {
        if (other == null) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        if (!CanInteractWithPlayer(player)) return;

        playerInRange = player;
        SetPromptVisible(true);
    }

    private void UnregisterPlayer(Collider2D other)
    {
        if (other == null || playerInRange == null) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || player != playerInRange) return;

        playerInRange = null;
        SetPromptVisible(false);
    }

    private bool CanInteractWithPlayer(PlayerMovement player)
    {
        if (player == null) return false;
        if (requirePlayerAbove)
        {
            float minY = transform.position.y + minPlayerYFromLetterCenter;
            if (player.transform.position.y < minY) return false;
        }
        return true;
    }

    private void TryTriggerEnding(PlayerMovement player)
    {
        if (player == null) return;

        // ENVIAR EL TEXTO Y EL JUGADOR AL LETTER MANAGER
        if (LetterManager.Instance != null)
        {
            LetterManager.Instance.ReadLetter(screenMessage, player); // <-- ¡Añadido el player aquí!
        }
        else
        {
            Debug.LogWarning("LetterManager.Instance is null! Make sure LetterManager is in the scene.");
        }
    }

    private void EnsureLetterCollider()
    {
        if (letterCollider == null) letterCollider = GetComponent<Collider2D>();
        if (letterCollider != null) letterCollider.isTrigger = true;
    }

    private void EnsurePromptVisual()
    {
        if (promptRoot == null)
        {
            Transform existingRoot = transform.Find("InteractPrompt_E");
            if (existingRoot != null) promptRoot = existingRoot.gameObject;
        }

        if (promptRoot == null)
        {
            promptRoot = new GameObject("InteractPrompt_E");
            promptRoot.transform.SetParent(transform, false);
        }
        promptRoot.transform.localPosition = promptOffset;

        RemovePromptChild("Ring");
        RemovePromptChild("Circle");
        SpriteRenderer rootSprite = promptRoot.GetComponent<SpriteRenderer>();
        if (rootSprite != null) rootSprite.enabled = false;

        if (promptText == null)
        {
            Transform label = promptRoot.transform.Find("Label");
            if (label != null) promptText = label.GetComponent<TextMeshPro>();
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

    private void RemovePromptChild(string childName)
    {
        if (promptRoot == null) return;
        Transform child = promptRoot.transform.Find(childName);
        if (child == null) return;

        if (Application.isPlaying) Destroy(child.gameObject);
        else DestroyImmediate(child.gameObject);
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot == null) return;
        promptTargetVisible = visible;
        if (promptTargetVisible && !promptRoot.activeSelf) promptRoot.SetActive(true);
    }

    private void UpdatePromptAnimation(bool immediate = false)
    {
        if (promptRoot == null || promptText == null) return;

        float target = promptTargetVisible ? 1f : 0f;
        float duration = target > promptVisibility ? Mathf.Max(0.01f, promptShowDuration) : Mathf.Max(0.01f, promptHideDuration);

        float step = immediate ? 1f : Time.deltaTime / duration;
        promptVisibility = Mathf.MoveTowards(promptVisibility, target, step);

        float eased = EaseInOut(promptVisibility);
        float popMul = Mathf.Lerp(Mathf.Max(1f, promptPopScale), 1f, eased);

        float bob = Mathf.Sin(Time.unscaledTime * Mathf.Max(0.1f, promptIdleFloatSpeed)) * Mathf.Max(0f, promptIdleFloatAmplitude) * eased;
        promptRoot.transform.localPosition = promptOffset + new Vector3(0f, bob, 0f);

        Color textColor = promptText.color;
        textColor.a = eased;
        promptText.color = textColor;

        promptText.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, eased) * popMul;
        promptText.transform.localPosition = new Vector3(0f, Mathf.Lerp(-0.03f, 0f, eased), -0.01f);

        if (!promptTargetVisible && promptVisibility <= 0.0001f && promptRoot.activeSelf) promptRoot.SetActive(false);
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - (2f * t));
    }

    private bool WasInteractPressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (interactKey == KeyCode.E && Keyboard.current.eKey.wasPressedThisFrame) pressed = true;
            else if (interactKey == KeyCode.F && Keyboard.current.fKey.wasPressedThisFrame) pressed = true;
            else if (interactKey == KeyCode.Return && Keyboard.current.enterKey.wasPressedThisFrame) pressed = true;
        }
#endif

// Solo compilará la vieja entrada si el proyecto lo permite
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed)
        {
            pressed = Input.GetKeyDown(interactKey);
        }
#endif

        return pressed;
    }

    private void NormalizePromptSettings()
    {
        if (promptFontSize <= 0f) promptFontSize = DefaultPromptFontSize;
        promptFontSize = Mathf.Clamp(promptFontSize, 0.8f, 1.8f);
        if (promptShowDuration <= 0f) promptShowDuration = DefaultShowDuration;
        if (promptHideDuration <= 0f) promptHideDuration = DefaultHideDuration;
        promptPopScale = Mathf.Max(1f, promptPopScale);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizePromptSettings();
        EnsureLetterCollider();
        if (promptText != null) promptText.fontSize = Mathf.Max(0.8f, promptFontSize);
        if (promptRoot != null) promptRoot.transform.localPosition = promptOffset;
    }
#endif
}