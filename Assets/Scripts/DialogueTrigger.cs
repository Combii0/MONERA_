using UnityEngine;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private Collider2D triggerCollider;
    
    [Header("DIÁLOGOS DE ESTE NPC/OBJETO")]
    [Tooltip("Añade tantas líneas de diálogo como necesites. Cada una puede tener su propia imagen y texto.")]
    public DialogueLine[] dialogueLines;

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

    [Header("Audio NPC")]
    [SerializeField] private AudioClip npcTypeSfx;
    [SerializeField, Range(0f, 1f)] private float npcTypeSfxVolume = 0.65f;

    private PlayerMovement playerInRange;
    private GameObject promptRoot;
    private TextMeshPro promptText;
    private SpriteRenderer sourceRenderer;
    private bool promptTargetVisible;
    private float promptVisibility;

    private void Awake()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;

        sourceRenderer = GetComponentInParent<SpriteRenderer>();
        EnsurePromptVisual();
        SetPromptVisible(false);
        UpdatePromptAnimation(true);
    }

    private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => RegisterPlayer(other);
    private void OnTriggerExit2D(Collider2D other) => UnregisterPlayer(other);

    private void Update()
    {
        if (playerInRange != null)
        {
            bool isReading = DialogueManager.Instance != null && DialogueManager.Instance.IsReading;

            if (isReading)
            {
                SetPromptVisible(false);
            }
            else
            {
                SetPromptVisible(true);
                if (promptRoot != null) promptRoot.transform.localPosition = promptOffset;

                if (!requireInteractKey || WasInteractPressedThisFrame())
                {
                    if (DialogueManager.Instance != null)
                    {
                        // Pass the array of lines to the Manager
                        DialogueManager.Instance.StartDialogue(
                            dialogueLines,
                            playerInRange,
                            npcTypeSfx,
                            npcTypeSfxVolume,
                            -1,
                            0f,
                            false
                        );
                    }
                    else
                    {
                        Debug.LogWarning("¡Falta el DialogueManager en la escena!");
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

    // --- MÉTODOS VISUALES DEL PROMPT "E" MANTENIDOS DE TU CÓDIGO ORIGINAL ---

    private void EnsurePromptVisual()
    {
        if (promptRoot == null)
        {
            Transform existingRoot = transform.Find("InteractPrompt_E");
            if (existingRoot != null) promptRoot = existingRoot.gameObject;
            else
            {
                promptRoot = new GameObject("InteractPrompt_E");
                promptRoot.transform.SetParent(transform, false);
            }
        }
        
        promptRoot.transform.localPosition = promptOffset;
        SpriteRenderer rootSprite = promptRoot.GetComponent<SpriteRenderer>();
        if (rootSprite != null) rootSprite.enabled = false;

        if (promptText == null)
        {
            Transform label = promptRoot.transform.Find("Label");
            if (label != null) promptText = label.GetComponent<TextMeshPro>();
            else
            {
                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(promptRoot.transform, false);
                promptText = labelObj.AddComponent<TextMeshPro>();
            }
        }

        promptText.text = "E";
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.fontSize = Mathf.Max(0.8f, promptFontSize);
        promptText.color = Color.white;
        promptText.fontStyle = FontStyles.Bold;
        promptText.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        promptText.rectTransform.sizeDelta = new Vector2(2f, 2f);

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

        float eased = promptVisibility * promptVisibility * (3f - (2f * promptVisibility));
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
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed) pressed = Input.GetKeyDown(interactKey);
#endif
        return pressed;
    }
}
