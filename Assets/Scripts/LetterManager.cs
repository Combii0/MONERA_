using UnityEngine;
using TMPro;
using System.Collections;

// Necesario si usas el nuevo Input System
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LetterManager : MonoBehaviour
{
    public static LetterManager Instance { get; private set; }

    public bool IsReading { get; private set; } 

    [Header("Referencias UI")]
    [Tooltip("El Canvas de la carta (LetterCanvas) como GameObject.")]
    [SerializeField] private GameObject letterCanvasObject; 
    [Tooltip("El texto (SignText) donde se escribirá la historia")]
    [SerializeField] private TextMeshProUGUI textSlot;

    [Header("Configuración")]
    [SerializeField] private float revealCharsPerSecond = 38f;
    [SerializeField] private float letterAutoSizeMin = 28f;
    [SerializeField] private float letterAutoSizeMax = 72f;

    [Header("Audio")]
    [SerializeField] private AudioClip typeSfx;
    [SerializeField, Range(0f, 1f)] private float typeSfxVolume = 0.65f;

    private Coroutine typeRoutine;
    private float nextSfxTime;
    private PlayerMovement frozenPlayer;
    private AudioClip runtimeTypeSfxOverride;
    private float runtimeTypeSfxVolumeOverride = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (letterCanvasObject != null) letterCanvasObject.SetActive(false);
    }

    // Nota: Se han eliminado los parámetros de tamaño de fuente extra y Transform,
    // ya que ahora todo se maneja desde el Inspector y el Canvas estático.
    public void ReadLetter(string message, PlayerMovement player)
    {
        if (textSlot == null)
        {
            Debug.LogError("LetterManager: ¡Falta asignar el SignText en el Inspector!");
            return;
        }
        if (string.IsNullOrEmpty(message)) return;

        if (IsReading) return; 

        runtimeTypeSfxOverride = null;
        runtimeTypeSfxVolumeOverride = -1f;
        IsReading = true; 

        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(TypeRoutine(message, player));
    }

    public void ReadLetter(string message, PlayerMovement player, AudioClip customTypeSfx, float customTypeSfxVolume)
    {
        if (textSlot == null)
        {
            Debug.LogError("LetterManager: ¡Falta asignar el SignText en el Inspector!");
            return;
        }
        if (string.IsNullOrEmpty(message)) return;
        if (IsReading) return;

        runtimeTypeSfxOverride = customTypeSfx;
        runtimeTypeSfxVolumeOverride = Mathf.Clamp01(customTypeSfxVolume);
        IsReading = true; 

        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(TypeRoutine(message, player));
    }

    private IEnumerator TypeRoutine(string message, PlayerMovement player)
    {
        Time.timeScale = 0f;
        frozenPlayer = player;
        
        if (frozenPlayer != null)
        {
            Rigidbody2D rb = frozenPlayer.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero; 
            frozenPlayer.enabled = false; 
        }

        if (letterCanvasObject != null) letterCanvasObject.SetActive(true);
        
        // Forzar el auto-size basado en tus mínimos y máximos
        textSlot.enableAutoSizing = true;
        textSlot.fontSizeMin = letterAutoSizeMin;
        textSlot.fontSizeMax = letterAutoSizeMax;
        
        textSlot.text = message;
        textSlot.ForceMeshUpdate();
        
        int totalChars = textSlot.textInfo.characterCount;
        textSlot.maxVisibleCharacters = 0;
        float visible = 0f;

        yield return null;

        // 1. FASE DE ESCRITURA
        while (textSlot.maxVisibleCharacters < totalChars)
        {
            if (WasInteractPressedThisFrame())
            {
                textSlot.maxVisibleCharacters = totalChars;
                break;
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

        yield return null;

        // 2. FASE DE LECTURA
        while (true)
        {
            if (WasInteractPressedThisFrame()) break;
            yield return null; 
        }

        // 3. FASE DE CIERRE
        if (letterCanvasObject != null) letterCanvasObject.SetActive(false);
        
        if (frozenPlayer != null)
        {
            frozenPlayer.enabled = true; 
            frozenPlayer = null;
        }
        
        Time.timeScale = 1f;
        IsReading = false; 
        typeRoutine = null;
        runtimeTypeSfxOverride = null;
        runtimeTypeSfxVolumeOverride = -1f;
    }

    private void OnDisable()
    {
        CleanupStateIfInterrupted();
    }

    private void OnDestroy()
    {
        CleanupStateIfInterrupted();
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

    private void CleanupStateIfInterrupted()
    {
        if (frozenPlayer != null)
        {
            frozenPlayer.enabled = true;
            frozenPlayer = null;
        }

        Time.timeScale = 1f;
        IsReading = false;
        runtimeTypeSfxOverride = null;
        runtimeTypeSfxVolumeOverride = -1f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        letterAutoSizeMin = Mathf.Clamp(letterAutoSizeMin, 1f, 300f);
        letterAutoSizeMax = Mathf.Clamp(letterAutoSizeMax, letterAutoSizeMin, 300f);
    }
#endif
}
