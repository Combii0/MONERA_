using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ProtectorDialogue : MonoBehaviour
{
    [Header("Dialogo Del Protector (Antes de Morir)")]
    [Tooltip("Lineas de dialogo estilo NPC. Se reproducen despues del zoom y antes de la animacion de muerte.")]
    public DialogueLine[] preDeathDialogueLines;

    [Header("Audio Del Dialogo")]
    [SerializeField] private AudioClip protectorTypeSfx;
    [SerializeField, Range(0f, 1f)] private float protectorTypeSfxVolume = 0.65f;

    [Header("Audio Post-Muerte")]
    [Tooltip("Este clip suena justo despues de que termina la animacion de muerte.")]
    public AudioClip postDeathAudioClip;
    [SerializeField, Min(0f)] private float postDeathAudioStartTime = 0.3f;

    [Header("Mensaje Final")]
    [SerializeField] private bool useCustomPostDeathFlow = true;
    [SerializeField, TextArea(2, 4)] private string rewardMessage =
        "Congratullations! For murdering, you are more powerfull now!(+1 more life)";
    [SerializeField, Min(0f)] private float waitAfterConfirmSeconds = 2f;
    [SerializeField, Min(1)] private int extraLivesReward = 1;
    [SerializeField, Min(18f)] private float rewardFontSize = 52f;
    [SerializeField] private Vector2 rewardBoxSize = new Vector2(1120f, 260f);

    private Canvas rewardCanvas;
    private TextMeshProUGUI rewardText;
    private TextMeshProUGUI continueText;
    private bool rewardGranted;
    private AudioSource runtimeAudioSource;
    private float nextRewardTypeSfxTime;

    public bool HasCustomPostDeathFlow => useCustomPostDeathFlow;

    public IEnumerator PlayPreDeathDialogue()
    {
        if (!HasAnyTextLine(preDeathDialogueLines)) yield break;
        if (DialogueManager.Instance == null) yield break;

        // Evita colisionar con otro dialogo si justo habia uno abierto.
        while (DialogueManager.Instance.IsReading)
        {
            yield return null;
        }

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        DialogueManager.Instance.StartDialogue(
            preDeathDialogueLines,
            player,
            protectorTypeSfx,
            protectorTypeSfxVolume,
            -1,
            0f,
            false
        );

        while (DialogueManager.Instance != null && DialogueManager.Instance.IsReading)
        {
            yield return null;
        }
    }

    public void PlayPostDeathAudio()
    {
        if (postDeathAudioClip == null) return;
        PlayRuntimeClipFromTime(postDeathAudioClip, 1f, postDeathAudioStartTime);
    }

    public IEnumerator ShowRewardMessageAndDelay()
    {
        EnsureRewardUi();
        if (rewardCanvas == null || rewardText == null) yield break;

        if (!rewardGranted && extraLivesReward > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.TryAddLife(extraLivesReward);
            rewardGranted = true;
        }

        ApplyDialogueStyleToRewardUi();
        rewardText.text = rewardMessage;
        rewardText.ForceMeshUpdate();
        rewardText.maxVisibleCharacters = 0;
        continueText.gameObject.SetActive(false);
        rewardCanvas.gameObject.SetActive(true);

        yield return AnimateRewardText();
        yield return null;

        while (!WasInteractPressedThisFrame())
        {
            yield return null;
        }

        rewardCanvas.gameObject.SetActive(false);

        if (waitAfterConfirmSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitAfterConfirmSeconds);
        }
    }

    private bool HasAnyTextLine(DialogueLine[] lines)
    {
        if (lines == null || lines.Length == 0) return false;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i].text)) return true;
        }

        return false;
    }

    private void EnsureRewardUi()
    {
        if (rewardCanvas != null && rewardText != null && continueText != null) return;

        GameObject canvasObject = new GameObject("ProtectorRewardMessageCanvas");
        rewardCanvas = canvasObject.AddComponent<Canvas>();
        rewardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rewardCanvas.sortingOrder = 5100;
        rewardCanvas.gameObject.SetActive(false);

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject boxObject = new GameObject("RewardBox");
        boxObject.transform.SetParent(canvasObject.transform, false);

        RectTransform boxRect = boxObject.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = rewardBoxSize;

        Image boxImage = boxObject.AddComponent<Image>();
        boxImage.color = new Color(0f, 0f, 0f, 0f); // Caja vacia

        Outline boxOutline = boxObject.AddComponent<Outline>();
        boxOutline.effectColor = new Color(1f, 1f, 1f, 0.95f);
        boxOutline.effectDistance = new Vector2(2f, -2f);

        GameObject textObject = new GameObject("RewardText");
        textObject.transform.SetParent(boxObject.transform, false);
        rewardText = textObject.AddComponent<TextMeshProUGUI>();
        rewardText.alignment = TextAlignmentOptions.Center;
        rewardText.fontSize = rewardFontSize;
        rewardText.textWrappingMode = TextWrappingModes.Normal;
        rewardText.color = Color.white;
        rewardText.text = rewardMessage;

        RectTransform textRect = rewardText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(46f, 38f);
        textRect.offsetMax = new Vector2(-46f, -70f);

        GameObject continueObject = new GameObject("ContinueHint");
        continueObject.transform.SetParent(boxObject.transform, false);
        continueText = continueObject.AddComponent<TextMeshProUGUI>();
        continueText.alignment = TextAlignmentOptions.Center;
        continueText.fontSize = 28f;
        continueText.textWrappingMode = TextWrappingModes.NoWrap;
        continueText.color = new Color(1f, 1f, 1f, 0.9f);
        continueText.text = "Press E";

        RectTransform continueRect = continueText.rectTransform;
        continueRect.anchorMin = new Vector2(0f, 0f);
        continueRect.anchorMax = new Vector2(1f, 0f);
        continueRect.pivot = new Vector2(0.5f, 0f);
        continueRect.anchoredPosition = new Vector2(0f, 18f);
        continueRect.sizeDelta = new Vector2(0f, 40f);
    }

    private IEnumerator AnimateRewardText()
    {
        rewardText.ForceMeshUpdate();
        int totalChars = rewardText.textInfo.characterCount;
        float visible = 0f;
        float revealSpeed = DialogueManager.Instance != null
            ? DialogueManager.Instance.RevealCharsPerSecond
            : 38f;

        yield return null;

        while (rewardText.maxVisibleCharacters < totalChars)
        {
            if (WasInteractPressedThisFrame())
            {
                rewardText.maxVisibleCharacters = totalChars;
                break;
            }

            visible += revealSpeed * Time.unscaledDeltaTime;
            int nextVisible = Mathf.Min(totalChars, Mathf.FloorToInt(visible));

            while (rewardText.maxVisibleCharacters < nextVisible)
            {
                rewardText.maxVisibleCharacters++;
                PlayRewardTypeSfx();
            }

            yield return null;
        }

        continueText.gameObject.SetActive(true);
    }

    private void ApplyDialogueStyleToRewardUi()
    {
        DialogueManager dialogueManager = DialogueManager.Instance;
        if (dialogueManager == null) return;

        if (dialogueManager.DialogueFontAsset != null)
        {
            rewardText.font = dialogueManager.DialogueFontAsset;
            continueText.font = dialogueManager.DialogueFontAsset;
        }

        if (dialogueManager.DialogueFontMaterial != null)
        {
            rewardText.fontSharedMaterial = dialogueManager.DialogueFontMaterial;
            continueText.fontSharedMaterial = dialogueManager.DialogueFontMaterial;
        }

        rewardText.fontStyle = dialogueManager.DialogueFontStyle;
        continueText.fontStyle = dialogueManager.DialogueFontStyle;
        rewardText.color = dialogueManager.DialogueTextColor;
        continueText.color = new Color(
            dialogueManager.DialogueTextColor.r,
            dialogueManager.DialogueTextColor.g,
            dialogueManager.DialogueTextColor.b,
            0.9f);
    }

    private void PlayRewardTypeSfx()
    {
        AudioClip clip = DialogueManager.Instance != null && DialogueManager.Instance.DefaultTypeSfx != null
            ? DialogueManager.Instance.DefaultTypeSfx
            : protectorTypeSfx;
        if (clip == null) return;
        if (Time.unscaledTime < nextRewardTypeSfxTime) return;

        nextRewardTypeSfxTime = Time.unscaledTime + 0.02f;

        float volume = DialogueManager.Instance != null && DialogueManager.Instance.DefaultTypeSfx != null
            ? Mathf.Clamp01(DialogueManager.Instance.DefaultTypeSfxVolume)
            : Mathf.Clamp01(protectorTypeSfxVolume);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayUiSfx(clip, volume);
            return;
        }

        AudioSource source = ResolveRuntimeAudioSource();
        if (source != null)
        {
            source.PlayOneShot(clip, volume);
        }
    }

    private void PlayRuntimeClipFromTime(AudioClip clip, float volume, float startTimeSeconds)
    {
        if (clip == null) return;

        AudioSource source = ResolveRuntimeAudioSource();
        if (source == null) return;

        float safeStartTime = Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, clip.length - 0.01f));
        source.Stop();
        source.clip = clip;
        source.time = safeStartTime;
        source.volume = Mathf.Clamp01(volume);
        source.loop = false;
        source.Play();
    }

    private AudioSource ResolveRuntimeAudioSource()
    {
        if (runtimeAudioSource != null) return runtimeAudioSource;

        runtimeAudioSource = GetComponent<AudioSource>();
        if (runtimeAudioSource == null)
        {
            runtimeAudioSource = gameObject.AddComponent<AudioSource>();
        }

        runtimeAudioSource.playOnAwake = false;
        runtimeAudioSource.loop = false;
        return runtimeAudioSource;
    }

    private bool WasInteractPressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            pressed = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed)
        {
            pressed = Input.GetKeyDown(KeyCode.E);
        }
#endif

        return pressed;
    }

    private void OnDestroy()
    {
        if (rewardCanvas != null)
        {
            Destroy(rewardCanvas.gameObject);
            rewardCanvas = null;
        }
    }
}
