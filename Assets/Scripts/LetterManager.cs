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

    [Header("Audio")]
    [SerializeField] private AudioClip typeSfx;
    [SerializeField, Range(0f, 1f)] private float typeSfxVolume = 0.65f;

    private Coroutine typeRoutine;
    private float nextSfxTime;
    private PlayerMovement frozenPlayer;

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

    public void ReadLetter(string message, PlayerMovement player)
    {
        if (textSlot == null)
        {
            Debug.LogError("LetterManager: ¡Falta asignar el SignText en el Inspector!");
            return;
        }
        if (string.IsNullOrEmpty(message)) return;

        if (IsReading) return; 

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

        textSlot.text = message;
        textSlot.ForceMeshUpdate();
        
        int totalChars = textSlot.textInfo.characterCount;
        textSlot.maxVisibleCharacters = 0;
        float visible = 0f;

        // Esperamos 1 frame para evitar que la misma tecla 'E' que 
        // abrió la carta accidentalmente salte el texto al instante.
        yield return null;

        // 1. FASE DE ESCRITURA (Click para saltar animación)
        while (textSlot.maxVisibleCharacters < totalChars)
        {
            if (WasInteractPressedThisFrame())
            {
                // ¡El jugador hizo click! Llenamos el texto de golpe y rompemos el loop.
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

        // Esperamos 1 frame para evitar que el click que saltó el texto
        // también cierre la carta en el mismo frame.
        yield return null;

        // 2. FASE DE LECTURA (Click para cerrar)
        while (!WasInteractPressedThisFrame())
        {
            // El juego se queda aquí pausado infinitamente hasta que el jugador presione el botón
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
                      Input.GetMouseButtonDown(0); // Left Click
        }
#endif

        return pressed;
    }

    private void PlayTypeSfx()
    {
        if (typeSfx == null) return;
        if (Time.unscaledTime < nextSfxTime) return;
        
        nextSfxTime = Time.unscaledTime + 0.02f;
        
        AudioSource src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.PlayOneShot(typeSfx, typeSfxVolume);
    }
}