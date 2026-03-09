using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
public class NPC : MonoBehaviour
{
    [SerializeField] private Collider2D npcCollider;

    [Header("Interaccion")]
    [SerializeField] private bool requireInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.05f, 0f);
    [SerializeField] private int promptSortingOrderBoost = 60;

    [Header("Dialogos (Ordenados)")]
    [Tooltip("Los textos se muestran en orden, uno por cada vez que hables con el NPC.")]
    [SerializeField] public List<string> dialogueLines = new List<string>();
    [SerializeField] private bool loopDialogue = false;
    [SerializeField] private int startDialogueIndex = 0;

    [Header("Nombre NPC")]
    [Tooltip("Nombre visible del NPC al pasar o hacer click con el mouse.")]
    public string npcName = "NPC";
    [SerializeField] private bool showNameOnPointer = true;
    [SerializeField] private Vector3 nameOffset = new Vector3(0.95f, 0.28f, 0f);
    [SerializeField] private int nameSortingOrderBoost = 210;
    [SerializeField, Range(0f, 1f)] private float nameOpacity = 0.9f;
    [SerializeField] private float nameFontSize = 1.8f;
    [SerializeField] private float nameRightPadding = 0.32f;
    [SerializeField] private float nameVerticalOffset = 0.18f;
    [SerializeField] private TMP_FontAsset nameFontAsset;

    [Header("Tamaño")]
    [SerializeField] public float interactPromptFontSize = 1.4f;
    [SerializeField] public float interactPromptScale = 2.4f;
    [SerializeField] public float dialogueScreenFontSize = 38f;

    [Header("Animacion Prompt")]
    [SerializeField] private float promptShowDuration = 0.12f;
    [SerializeField] private float promptHideDuration = 0.14f;
    [SerializeField] private float promptPopScale = 1.08f;
    [SerializeField] private float promptIdleFloatAmplitude = 0.03f;
    [SerializeField] private float promptIdleFloatSpeed = 4.2f;

    private PlayerMovement playerInRange;
    private GameObject promptRoot;
    private TextMeshPro promptText;
    private GameObject nameRoot;
    private TextMeshPro nameText;
    private SpriteRenderer sourceRenderer;
    private bool promptTargetVisible;
    private float promptVisibility;
    private int currentDialogueIndex;
    private bool pointerOverNpc;
    private static TMP_FontAsset cachedDefaultNameFont;

    private const float DefaultPromptFontSize = 1.4f;
    private const float DefaultShowDuration = 0.12f;
    private const float DefaultHideDuration = 0.14f;
    private const string DefaultNameFontResourcePath = "Fonts & Materials/BoldPixels SDF";
    private static readonly Vector3 LegacyNameOffset = new Vector3(0f, -1.05f, 0f);
    private static readonly Vector3 DesiredNameOffset = new Vector3(0.95f, 0.28f, 0f);

    private void Awake()
    {
        ApplyLegacyNameSettingsIfNeeded();
        NormalizeSettings();
        EnsureNpcCollider();
        sourceRenderer = GetComponentInParent<SpriteRenderer>();
        EnsurePromptVisual();
        EnsureNameVisual();

        currentDialogueIndex = Mathf.Clamp(startDialogueIndex, 0, Mathf.Max(0, dialogueLines.Count - 1));
        promptVisibility = 0f;
        promptTargetVisible = false;
        SetPromptVisible(false);
        SetNameVisible(false);
        UpdatePromptAnimation(true);
        UpdateNameVisual();
    }

    private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => RegisterPlayer(other);
    private void OnCollisionEnter2D(Collision2D collision) => RegisterPlayer(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => RegisterPlayer(collision.collider);
    private void OnTriggerExit2D(Collider2D other) => UnregisterPlayer(other);
    private void OnCollisionExit2D(Collision2D collision) => UnregisterPlayer(collision.collider);
    private void OnMouseEnter() => pointerOverNpc = true;
    private void OnMouseOver() => pointerOverNpc = true;
    private void OnMouseExit() => pointerOverNpc = false;

    private void Update()
    {
        UpdateNameVisibilityFromPointer();

        if (playerInRange != null)
        {
            bool isReading = LetterManager.Instance != null && LetterManager.Instance.IsReading;
            if (isReading)
            {
                HidePromptImmediate();
            }
            else
            {
                SetPromptVisible(true);
                if (promptRoot != null) promptRoot.transform.localPosition = promptOffset;

                if (!requireInteractKey || WasInteractPressedThisFrame())
                {
                    TryTalk();
                }
            }
        }

        UpdatePromptAnimation();
        UpdateNameVisual();
    }

    private void RegisterPlayer(Collider2D other)
    {
        if (other == null) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        if (LetterManager.Instance != null && LetterManager.Instance.IsReading) return;

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

    private void TryTalk()
    {
        if (LetterManager.Instance == null)
        {
            Debug.LogWarning("NPC: LetterManager.Instance es null. Agrega LetterManager a la escena.", this);
            return;
        }

        if (dialogueLines == null || dialogueLines.Count == 0)
        {
            Debug.LogWarning("NPC: No hay dialogos configurados en dialogueLines.", this);
            return;
        }

        string dialogue = dialogueLines[Mathf.Clamp(currentDialogueIndex, 0, dialogueLines.Count - 1)];
        if (string.IsNullOrWhiteSpace(dialogue)) return;

        HidePromptImmediate();
        LetterManager.Instance.ReadDialogueNoPause(dialogue, dialogueScreenFontSize, transform);
        AdvanceDialogueIndex();
    }

    private void AdvanceDialogueIndex()
    {
        if (dialogueLines == null || dialogueLines.Count == 0) return;

        if (loopDialogue)
        {
            currentDialogueIndex = (currentDialogueIndex + 1) % dialogueLines.Count;
        }
        else
        {
            currentDialogueIndex = Mathf.Min(currentDialogueIndex + 1, dialogueLines.Count - 1);
        }
    }

    private void EnsureNpcCollider()
    {
        if (npcCollider == null) npcCollider = GetComponent<Collider2D>();
        if (npcCollider != null) npcCollider.isTrigger = true;
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
        promptText.enableAutoSizing = false;
        promptText.fontSize = Mathf.Max(0.2f, interactPromptFontSize);
        promptText.color = Color.white;
        promptText.fontStyle = FontStyles.Bold;
        promptText.textWrappingMode = TextWrappingModes.NoWrap;
        promptText.overflowMode = TextOverflowModes.Overflow;
        promptText.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        RectTransform textRect = promptText.rectTransform;
        float rectSize = Mathf.Max(2f, interactPromptFontSize * Mathf.Max(1f, interactPromptScale) * 1.5f);
        textRect.sizeDelta = new Vector2(rectSize, rectSize);

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

    private void EnsureNameVisual()
    {
        if (nameRoot == null)
        {
            Transform existingRoot = transform.Find("NPCNameLabel");
            if (existingRoot != null) nameRoot = existingRoot.gameObject;
        }

        if (nameRoot == null)
        {
            nameRoot = new GameObject("NPCNameLabel");
            nameRoot.transform.SetParent(transform, false);
        }
        nameRoot.transform.localPosition = nameOffset;

        if (nameText == null)
        {
            Transform label = nameRoot.transform.Find("Label");
            if (label != null) nameText = label.GetComponent<TextMeshPro>();
        }
        if (nameText == null)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(nameRoot.transform, false);
            nameText = labelObj.AddComponent<TextMeshPro>();
        }

        nameText.text = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.enableAutoSizing = false;
        nameText.fontSize = Mathf.Max(0.2f, nameFontSize);
        nameText.color = new Color(1f, 1f, 1f, nameOpacity);
        nameText.fontStyle = FontStyles.Normal;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Overflow;
        nameText.transform.localPosition = new Vector3(0f, 0f, -0.01f);

        RectTransform textRect = nameText.rectTransform;
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(20f, 3.2f);

        TMP_FontAsset resolvedNameFont = ResolveNameFontAsset();
        if (resolvedNameFont != null)
        {
            nameText.font = resolvedNameFont;
        }
        else if (promptText != null && promptText.font != null)
        {
            nameText.font = promptText.font;
        }

        MeshRenderer textRenderer = nameText.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            int baseOrder = nameSortingOrderBoost;
            if (sourceRenderer != null)
            {
                textRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                baseOrder = sourceRenderer.sortingOrder + nameSortingOrderBoost;
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

    private void SetNameVisible(bool visible)
    {
        if (nameRoot == null) return;
        if (visible && !nameRoot.activeSelf) nameRoot.SetActive(true);
        else if (!visible && nameRoot.activeSelf) nameRoot.SetActive(false);
    }

    private void HidePromptImmediate()
    {
        if (promptRoot == null || promptText == null) return;

        promptTargetVisible = false;
        promptVisibility = 0f;

        Color c = promptText.color;
        c.a = 0f;
        promptText.color = c;

        if (promptRoot.activeSelf) promptRoot.SetActive(false);
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

        promptText.fontSize = Mathf.Max(0.2f, interactPromptFontSize);
        float promptScaleMul = Mathf.Max(0.2f, interactPromptScale);
        promptText.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, eased) * popMul * promptScaleMul;
        promptText.transform.localPosition = new Vector3(0f, Mathf.Lerp(-0.03f, 0f, eased), -0.01f);

        if (!promptTargetVisible && promptVisibility <= 0.0001f && promptRoot.activeSelf) promptRoot.SetActive(false);
    }

    private void UpdateNameVisual()
    {
        if (nameRoot == null || nameText == null) return;
        float rightPadding = Mathf.Max(0f, nameRightPadding);
        float verticalOffset = nameVerticalOffset;

        Vector3 worldPos;
        if (npcCollider != null)
        {
            Bounds b = npcCollider.bounds;
            worldPos = new Vector3(b.max.x + rightPadding, b.center.y + verticalOffset, transform.position.z + nameOffset.z);
        }
        else
        {
            worldPos = transform.position + new Vector3(rightPadding, verticalOffset, nameOffset.z);
        }

        nameRoot.transform.position = worldPos;
        nameText.text = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
        nameText.fontSize = Mathf.Max(0.2f, nameFontSize);
        TMP_FontAsset resolvedNameFont = ResolveNameFontAsset();
        if (resolvedNameFont != null) nameText.font = resolvedNameFont;

        Color c = nameText.color;
        c.a = Mathf.Clamp01(nameOpacity);
        nameText.color = c;
    }

    private void UpdateNameVisibilityFromPointer()
    {
        if (!showNameOnPointer)
        {
            SetNameVisible(false);
            return;
        }

        bool pointerOver = pointerOverNpc || IsPointerOverNpc();
        SetNameVisible(pointerOver);
    }

    private bool IsPointerOverNpc()
    {
        if (npcCollider == null || !npcCollider.enabled) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        if (!TryGetPointerScreenPosition(out Vector2 pointerPos)) return false;
        if (!cam.pixelRect.Contains(pointerPos)) return false;

        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, depth));
        return npcCollider.OverlapPoint(new Vector2(world.x, world.y));
    }

    private static bool TryGetPointerScreenPosition(out Vector2 pointerPos)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            pointerPos = Mouse.current.position.ReadValue();
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pointerPos = Input.mousePosition;
        return true;
#endif
        pointerPos = default;
        return false;
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
        if (!pressed)
        {
            pressed = Input.GetKeyDown(interactKey);
        }
#endif

        return pressed;
    }

    private static float EaseInOut(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - (2f * t));
    }

    private void NormalizeSettings()
    {
        if (interactPromptFontSize <= 0f) interactPromptFontSize = DefaultPromptFontSize;
        interactPromptFontSize = Mathf.Clamp(interactPromptFontSize, 0.2f, 20f);
        if (interactPromptScale <= 0f) interactPromptScale = 2.4f;
        interactPromptScale = Mathf.Clamp(interactPromptScale, 0.2f, 12f);
        if (dialogueScreenFontSize <= 0f) dialogueScreenFontSize = 38f;
        dialogueScreenFontSize = Mathf.Clamp(dialogueScreenFontSize, 6f, 220f);
        nameFontSize = Mathf.Clamp(nameFontSize, 0.2f, 20f);
        nameOpacity = Mathf.Clamp01(nameOpacity);
        nameRightPadding = Mathf.Clamp(nameRightPadding, 0f, 10f);
        nameVerticalOffset = Mathf.Clamp(nameVerticalOffset, -5f, 5f);
        if (promptShowDuration <= 0f) promptShowDuration = DefaultShowDuration;
        if (promptHideDuration <= 0f) promptHideDuration = DefaultHideDuration;
        promptPopScale = Mathf.Max(1f, promptPopScale);
    }

    private TMP_FontAsset ResolveNameFontAsset()
    {
        if (nameFontAsset != null) return nameFontAsset;

        if (cachedDefaultNameFont == null)
        {
            cachedDefaultNameFont = Resources.Load<TMP_FontAsset>(DefaultNameFontResourcePath);
        }

        return cachedDefaultNameFont;
    }

    private void ApplyLegacyNameSettingsIfNeeded()
    {
        // Migra la configuración inicial vieja del nombre (debajo y pequeño) al nuevo estilo lateral y legible.
        if ((nameOffset - LegacyNameOffset).sqrMagnitude <= 0.0001f)
        {
            nameOffset = DesiredNameOffset;
        }

        if (Mathf.Approximately(nameFontSize, 1.1f))
        {
            nameFontSize = 1.8f;
        }

        if (nameSortingOrderBoost == 55)
        {
            nameSortingOrderBoost = 210;
        }

        if (Mathf.Approximately(nameRightPadding, 0f))
        {
            nameRightPadding = 0.32f;
        }

        if (Mathf.Approximately(nameVerticalOffset, 0f))
        {
            nameVerticalOffset = 0.18f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLegacyNameSettingsIfNeeded();
        NormalizeSettings();
        EnsureNpcCollider();
        if (promptText != null)
        {
            promptText.enableAutoSizing = false;
            promptText.fontSize = Mathf.Max(0.2f, interactPromptFontSize);
            RectTransform textRect = promptText.rectTransform;
            float rectSize = Mathf.Max(2f, interactPromptFontSize * Mathf.Max(1f, interactPromptScale) * 1.5f);
            textRect.sizeDelta = new Vector2(rectSize, rectSize);
        }
        if (promptRoot != null) promptRoot.transform.localPosition = promptOffset;
        if (nameText != null)
        {
            nameText.text = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
            nameText.fontSize = Mathf.Max(0.2f, nameFontSize);
            Color c = nameText.color;
            c.a = Mathf.Clamp01(nameOpacity);
            nameText.color = c;
        }

        currentDialogueIndex = Mathf.Clamp(startDialogueIndex, 0, Mathf.Max(0, dialogueLines.Count - 1));
    }
#endif
}
