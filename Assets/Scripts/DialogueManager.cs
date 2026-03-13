using UnityEngine;
using UnityEngine.UI; // Needed for the Image component
using TMPro;
using System.Collections;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// This struct allows you to set an Image and Text together in the Inspector
[System.Serializable]
public struct DialogueLine
{
    [Tooltip("La imagen del personaje que habla.")]
    public Sprite portrait;

    [Tooltip("Opcional: SFX que suena al confirmar esta linea.")]
    public AudioClip interactSfx;
    [Range(0f, 1f)]
    public float interactSfxVolume;
    
    [TextArea(3, 6)]
    [Tooltip("El texto que dirá el personaje.")]
    public string text;
}

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    public bool IsReading { get; private set; } 
    public TMP_FontAsset DialogueFontAsset => textSlot != null ? textSlot.font : null;
    public Material DialogueFontMaterial => textSlot != null ? textSlot.fontSharedMaterial : null;
    public FontStyles DialogueFontStyle => textSlot != null ? textSlot.fontStyle : FontStyles.Normal;
    public Color DialogueTextColor => textSlot != null ? textSlot.color : Color.white;
    public float RevealCharsPerSecond => revealCharsPerSecond;
    public AudioClip DefaultTypeSfx => typeSfx;
    public float DefaultTypeSfxVolume => typeSfxVolume;

    [Header("Referencias UI")]
    [Tooltip("El Canvas del diálogo (DialogueCanvas)")]
    [SerializeField] private GameObject dialogueCanvasObject; 
    [Tooltip("El componente Image (DialogueImage)")]
    [SerializeField] private Image imageSlot;
    [Tooltip("El texto (Dialogue) donde se escribirá la historia")]
    [SerializeField] private TextMeshProUGUI textSlot;

    [Header("Configuración")]
    [SerializeField] private float revealCharsPerSecond = 38f;

    [Header("Audio")]
    [SerializeField] private AudioClip typeSfx;
    [SerializeField, Range(0f, 1f)] private float typeSfxVolume = 0.65f;

    private Coroutine dialogueRoutine;
    private float nextSfxTime;
    private PlayerMovement frozenPlayer;
    private AudioClip runtimeTypeSfxOverride;
    private float runtimeTypeSfxVolumeOverride = -1f;
    private int runtimeFinalLineIndex = -1;
    private float runtimeDialogueFontSize = -1f;
    private bool runtimeHidePortraitOnFinalLine;
    private float cachedFontSize;
    private bool cachedFontSizeValid;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dialogueCanvasObject != null) dialogueCanvasObject.SetActive(false);
    }

    public void StartDialogue(DialogueLine[] dialogueLines, PlayerMovement player)
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;
        if (IsReading) return; 

        ResetRuntimeOverrides();

        IsReading = true; 

        if (dialogueRoutine != null) StopCoroutine(dialogueRoutine);
        dialogueRoutine = StartCoroutine(DialogueSequence(dialogueLines, player));
    }

    public void StartDialogue(
        DialogueLine[] dialogueLines,
        PlayerMovement player,
        AudioClip customTypeSfx,
        float customTypeSfxVolume,
        int finalLineIndex,
        float dialogueFontSize,
        bool hidePortraitOnFinalLine)
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;
        if (IsReading) return;

        runtimeTypeSfxOverride = customTypeSfx;
        runtimeTypeSfxVolumeOverride = Mathf.Clamp01(customTypeSfxVolume);
        runtimeFinalLineIndex = finalLineIndex;
        runtimeDialogueFontSize = dialogueFontSize;
        runtimeHidePortraitOnFinalLine = hidePortraitOnFinalLine;

        IsReading = true; 

        if (dialogueRoutine != null) StopCoroutine(dialogueRoutine);
        dialogueRoutine = StartCoroutine(DialogueSequence(dialogueLines, player));
    }

    private IEnumerator DialogueSequence(DialogueLine[] lines, PlayerMovement player)
    {
        Time.timeScale = 0f;
        frozenPlayer = player;
        
        if (frozenPlayer != null)
        {
            Rigidbody2D rb = frozenPlayer.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero; 
            frozenPlayer.enabled = false; 
        }

        if (dialogueCanvasObject != null) dialogueCanvasObject.SetActive(true);
        if (textSlot != null)
        {
            cachedFontSize = textSlot.fontSize;
            cachedFontSizeValid = true;
            if (runtimeDialogueFontSize > 0f) textSlot.fontSize = runtimeDialogueFontSize;
        }

        // Loop through every line of dialogue provided by the NPC
        for (int i = 0; i < lines.Length; i++)
        {
            DialogueLine currentLine = lines[i];
            bool hidePortraitThisLine = runtimeHidePortraitOnFinalLine && i == runtimeFinalLineIndex;

            // Update Image
            if (!hidePortraitThisLine && currentLine.portrait != null)
            {
                imageSlot.gameObject.SetActive(true);
                imageSlot.sprite = currentLine.portrait;
            }
            else
            {
                // Hide the image slot if no portrait is assigned for this line
                imageSlot.gameObject.SetActive(false); 
            }

            // Setup Text
            textSlot.text = currentLine.text;
            textSlot.ForceMeshUpdate();
            
            int totalChars = textSlot.textInfo.characterCount;
            textSlot.maxVisibleCharacters = 0;
            float visible = 0f;

            yield return null; // Wait 1 frame to prevent instant skipping

            // 1. FASE DE ESCRITURA
            while (textSlot.maxVisibleCharacters < totalChars)
            {
                if (WasInteractPressedThisFrame())
                {
                    textSlot.maxVisibleCharacters = totalChars;
                    break; // Skip typing animation
                }

                visible += revealCharsPerSecond * Time.unscaledDeltaTime;
                int nextVisible = Mathf.Min(totalChars, Mathf.FloorToInt(visible));

                while (textSlot.maxVisibleCharacters < nextVisible)
                {
                    textSlot.maxVisibleCharacters++;
                    PlayTypeSfx();
                }
                yield return null;
            }

            yield return null; // Wait 1 frame to prevent double-click issues

            // 2. FASE DE ESPERA (Wait for click to proceed to NEXT line or close)
            while (true)
            {
                if (WasInteractPressedThisFrame())
                {
                    PlayInteractSfx(currentLine);
                    break;
                }

                yield return null;
            }
        }

        // 3. FASE DE CIERRE (All lines finished)
        if (dialogueCanvasObject != null) dialogueCanvasObject.SetActive(false);
        
        if (frozenPlayer != null)
        {
            frozenPlayer.enabled = true; 
            frozenPlayer = null;
        }

        if (cachedFontSizeValid && textSlot != null)
        {
            textSlot.fontSize = cachedFontSize;
        }

        Time.timeScale = 1f;
        IsReading = false; 
        dialogueRoutine = null;
        ResetRuntimeOverrides();
    }

    private bool WasInteractPressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame) pressed = true;
            else if (Keyboard.current.fKey.wasPressedThisFrame) pressed = true;
            else if (Keyboard.current.enterKey.wasPressedThisFrame) pressed = true;
        }
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) pressed = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed)
        {
            pressed = Input.GetKeyDown(KeyCode.E) || 
                      Input.GetKeyDown(KeyCode.F) || 
                      Input.GetKeyDown(KeyCode.Return) || 
                      Input.GetMouseButtonDown(0);
        }
#endif

        return pressed;
    }

    private void PlayTypeSfx()
    {
        AudioClip activeTypeSfx = runtimeTypeSfxOverride != null ? runtimeTypeSfxOverride : typeSfx;
        if (activeTypeSfx == null) return;
        if (Time.unscaledTime < nextSfxTime) return;
        
        nextSfxTime = Time.unscaledTime + 0.02f;

        float volume = runtimeTypeSfxOverride != null
            ? Mathf.Clamp01(runtimeTypeSfxVolumeOverride)
            : Mathf.Clamp01(typeSfxVolume);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayUiSfx(activeTypeSfx, volume);
            return;
        }

        AudioSource src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.PlayOneShot(activeTypeSfx, volume);
    }

    private void PlayInteractSfx(DialogueLine line)
    {
        if (line.interactSfx == null) return;

        float volume = line.interactSfxVolume > 0f ? line.interactSfxVolume : 1f;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayUiSfx(line.interactSfx, volume);
            return;
        }

        AudioSource src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.PlayOneShot(line.interactSfx, Mathf.Clamp01(volume));
    }

    private void ResetRuntimeOverrides()
    {
        runtimeTypeSfxOverride = null;
        runtimeTypeSfxVolumeOverride = -1f;
        runtimeFinalLineIndex = -1;
        runtimeDialogueFontSize = -1f;
        runtimeHidePortraitOnFinalLine = false;
        cachedFontSizeValid = false;
    }
}
