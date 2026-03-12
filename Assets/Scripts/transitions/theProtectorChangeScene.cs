using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
public class theProtectorChangeScene : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private Collider2D triggerCollider;
    [SerializeField] private string targetSceneName = "THE PROTECTOR";

    [Header("DIÁLOGOS (como NPC, pero sin imagen)")]
    [Tooltip("Edita aquí las líneas previas al texto final obligatorio.")]
    public DialogueLine[] dialogueLines;

    [Header("Interacción")]
    [SerializeField] private bool requireInteractKey = true;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.05f, 0f);
    [SerializeField] private float promptFontSize = 1.4f;
    [SerializeField] private int promptSortingOrderBoost = 60;

    [Header("Animación Prompt")]
    [SerializeField] private float promptShowDuration = 0.12f;
    [SerializeField] private float promptHideDuration = 0.14f;
    [SerializeField] private float promptPopScale = 1.08f;
    [SerializeField] private float promptIdleFloatAmplitude = 0.03f;
    [SerializeField] private float promptIdleFloatSpeed = 4.2f;

    [Header("Audio de este Intro Boss")]
    [SerializeField] private AudioClip typeSfx;
    [SerializeField, Range(0f, 1f)] private float typeSfxVolume = 0.65f;
    [SerializeField] private AudioClip finalLineInteractSfx;

    [Header("Texto de este Intro Boss")]
    [SerializeField] public float dialogueFontSize = 46f;

    [Header("Fade a THE PROTECTOR")]
    [SerializeField, Min(0.1f)] private float transitionFadeDuration = 1.8f;
    [SerializeField, Range(2, 40)] private int transitionFadeSteps = 24;

    private const string FinalDialogueLine = "No bacteria will ever be able to pass OVER ME!";

    private PlayerMovement playerInRange;
    private GameObject promptRoot;
    private TextMeshPro promptText;
    private SpriteRenderer sourceRenderer;
    private bool promptTargetVisible;
    private float promptVisibility;

    private bool introDialogueStarted;
    private bool transitionTriggered;
    private bool wasReadingLastFrame;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;
    }

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
        bool isReading = DialogueManager.Instance != null && DialogueManager.Instance.IsReading;
        HandleDialogueStateTransition(isReading);

        if (playerInRange != null && !transitionTriggered)
        {
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
                    StartIntroDialogue();
                }
            }
        }

        UpdatePromptAnimation();
        wasReadingLastFrame = isReading;
    }

    private void HandleDialogueStateTransition(bool isReading)
    {
        if (!introDialogueStarted || transitionTriggered) return;
        if (wasReadingLastFrame && !isReading)
        {
            TriggerSceneTransition();
        }
    }

    private void StartIntroDialogue()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("Falta DialogueManager en la escena.");
            return;
        }

        if (playerInRange == null) return;
        if (DialogueManager.Instance.IsReading) return;

        DialogueLine[] lines = BuildDialogueSequence();
        if (lines == null || lines.Length == 0) return;

        introDialogueStarted = true;
        wasReadingLastFrame = true;
        SetPromptVisible(false);
        DialogueManager.Instance.StartDialogue(
            lines,
            playerInRange,
            typeSfx,
            typeSfxVolume,
            lines.Length - 1,
            dialogueFontSize,
            true
        );
    }

    private DialogueLine[] BuildDialogueSequence()
    {
        List<DialogueLine> lines = new List<DialogueLine>();

        if (dialogueLines != null)
        {
            for (int i = 0; i < dialogueLines.Length; i++)
            {
                string text = dialogueLines[i].text;
                if (string.IsNullOrWhiteSpace(text)) continue;

                lines.Add(new DialogueLine
                {
                    portrait = null, // Siempre oculto para este trigger
                    interactSfx = dialogueLines[i].interactSfx,
                    interactSfxVolume = dialogueLines[i].interactSfxVolume,
                    text = text
                });
            }
        }

        lines.Add(new DialogueLine
        {
            portrait = null,
            interactSfx = finalLineInteractSfx,
            interactSfxVolume = 1f,
            text = FinalDialogueLine
        });

        return lines.ToArray();
    }

    private void TriggerSceneTransition()
    {
        _ = transitionFadeDuration; // Compat: GameManager actual no expone duracion/pasos por llamada.
        _ = transitionFadeSteps;

        if (transitionTriggered) return;
        transitionTriggered = true;
        SetPromptVisible(false);

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("theProtectorChangeScene: targetSceneName está vacío.");
            return;
        }

        if (GameManager.Instance != null)
        {
            int sceneIndex = ResolveBuildSceneIndex(targetSceneName);
            if (sceneIndex >= 0)
            {
                GameManager.Instance.TransitionToScene(sceneIndex);
                return;
            }
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private static int ResolveBuildSceneIndex(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return -1;

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(scenePath)) continue;

            string buildSceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (string.Equals(buildSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void RegisterPlayer(Collider2D other)
    {
        if (other == null) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        playerInRange = player;
        bool canShowPrompt = !transitionTriggered && !(DialogueManager.Instance != null && DialogueManager.Instance.IsReading);
        SetPromptVisible(canShowPrompt);
    }

    private void UnregisterPlayer(Collider2D other)
    {
        if (other == null || playerInRange == null) return;
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null || player != playerInRange) return;

        playerInRange = null;
        SetPromptVisible(false);
    }

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
