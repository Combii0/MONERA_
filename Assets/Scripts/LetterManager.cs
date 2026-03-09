using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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
    [SerializeField] private bool useAutoSizeForLetterText = true;
    [SerializeField] private float letterAutoSizeMin = 10f;
    [SerializeField] private float letterAutoSizeMax = 18f;

    [Header("Audio")]
    [SerializeField] private AudioClip typeSfx;
    [SerializeField, Range(0f, 1f)] private float typeSfxVolume = 0.65f;

    [Header("NPC Dialogo (Sin Pausa)")]
    [SerializeField] private bool freezeActorsDuringNpcDialogue = true;
    [SerializeField] private bool showNpcContinueHint = true;
    [SerializeField] private string npcContinueHintMessage = "Press E to continue.";
    [SerializeField] private Vector3 npcContinueHintOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField, Range(0f, 1f)] private float npcContinueHintOpacity = 0.75f;
    [SerializeField] private float npcContinueHintFontSize = 2.4f;
    [SerializeField] private int npcContinueHintSortingOrder = 200;

    [Header("Cartel Dialogo (Con Pausa)")]
    [SerializeField] private bool showLetterContinueHint = true;
    [SerializeField] private string letterContinueHintMessage = "Press E to continue.";
    [SerializeField] private Vector2 letterContinueHintOffset = new Vector2(0f, -34f);
    [SerializeField, Range(0f, 1f)] private float letterContinueHintOpacity = 0.8f;
    [SerializeField] private float letterContinueHintFontSize = 16f;

    private Coroutine typeRoutine;
    private float nextSfxTime;
    private PlayerMovement frozenPlayer;
    private bool activeRoutinePausesGame;
    private readonly List<FrozenBehaviourState> frozenBehaviours = new List<FrozenBehaviourState>(64);
    private readonly HashSet<Behaviour> frozenBehaviourLookup = new HashSet<Behaviour>();
    private GameObject npcContinueHintObject;
    private TextMeshPro npcContinueHintText;
    private Transform npcContinueHintTarget;
    private GameObject letterContinueHintObject;
    private TextMeshProUGUI letterContinueHintText;
    private RectTransform letterContinueHintRect;

    private readonly struct FrozenBehaviourState
    {
        public readonly Behaviour Behaviour;
        public readonly bool WasEnabled;

        public FrozenBehaviourState(Behaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }
    }

    private void Awake()
    {
        ApplyLegacyHintSettingsIfNeeded();

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (letterCanvasObject != null) letterCanvasObject.SetActive(false);
    }

    public void ReadLetter(string message, PlayerMovement player, float messageFontSize)
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
        typeRoutine = StartCoroutine(TypeRoutine(message, player, messageFontSize, true));
    }

    public void ReadDialogueNoPause(string message, float messageFontSize, Transform continueHintTarget = null)
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
        typeRoutine = StartCoroutine(TypeRoutine(message, null, messageFontSize, false, continueHintTarget));
    }

    private IEnumerator TypeRoutine(string message, PlayerMovement player, float messageFontSize, bool pauseGame, Transform continueHintTarget = null)
    {
        activeRoutinePausesGame = pauseGame;
        if (pauseGame)
        {
            Time.timeScale = 0f;
            frozenPlayer = player;
            
            if (frozenPlayer != null)
            {
                Rigidbody2D rb = frozenPlayer.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero; 
                frozenPlayer.enabled = false; 
            }

            if (showLetterContinueHint) ShowLetterContinueHint();
            HideNpcContinueHint();
            npcContinueHintTarget = null;
        }
        else
        {
            if (freezeActorsDuringNpcDialogue)
            {
                FreezeActorsForNpcDialogue();
                npcContinueHintTarget = continueHintTarget != null
                    ? continueHintTarget
                    : (frozenPlayer != null ? frozenPlayer.transform : null);
            }
            else
            {
                frozenPlayer = null;
                if (continueHintTarget != null)
                {
                    npcContinueHintTarget = continueHintTarget;
                }
                else
                {
                    PlayerMovement playerTarget = FindFirstObjectByType<PlayerMovement>();
                    npcContinueHintTarget = playerTarget != null ? playerTarget.transform : null;
                }
            }

            if (showNpcContinueHint)
            {
                ShowNpcContinueHint();
            }

            HideLetterContinueHint();
        }

        if (letterCanvasObject != null) letterCanvasObject.SetActive(true);

        if (useAutoSizeForLetterText)
        {
            float minSize = Mathf.Clamp(letterAutoSizeMin, 1f, 300f);
            float maxSize = Mathf.Clamp(letterAutoSizeMax, minSize, 300f);
            textSlot.enableAutoSizing = true;
            textSlot.fontSizeMin = minSize;
            textSlot.fontSizeMax = maxSize;
        }
        else
        {
            textSlot.enableAutoSizing = false;
            textSlot.fontSize = Mathf.Max(6f, messageFontSize);
        }
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
            bool advancePressed = pauseGame ? WasInteractPressedThisFrame() : WasNpcContinuePressedThisFrame();
            if (advancePressed)
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

            UpdateLetterContinueHintPosition();
            UpdateNpcContinueHintPosition();
            yield return null;
        }

        // Esperamos 1 frame para evitar que el click que saltó el texto
        // también cierre la carta en el mismo frame.
        yield return null;

        // 2. FASE DE LECTURA (Click para cerrar)
        while (true)
        {
            bool advancePressed = pauseGame ? WasInteractPressedThisFrame() : WasNpcContinuePressedThisFrame();
            if (advancePressed) break;

            UpdateLetterContinueHintPosition();
            UpdateNpcContinueHintPosition();
            // El juego se queda aquí pausado infinitamente hasta que el jugador presione el botón
            yield return null; 
        }

        // 3. FASE DE CIERRE
        if (letterCanvasObject != null) letterCanvasObject.SetActive(false);
        HideNpcContinueHint();
        HideLetterContinueHint();
        
        if (frozenPlayer != null)
        {
            frozenPlayer.enabled = true; 
            frozenPlayer = null;
        }
        UnfreezeActorsFromNpcDialogue();
        npcContinueHintTarget = null;

        if (pauseGame) Time.timeScale = 1f;
        IsReading = false; 
        activeRoutinePausesGame = false;
        typeRoutine = null;
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
                      Input.GetMouseButtonDown(0); // Left Click
        }
#endif

        return pressed;
    }

    private static bool WasNpcContinuePressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) pressed = true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed) pressed = Input.GetKeyDown(KeyCode.E);
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

    private void CleanupStateIfInterrupted()
    {
        HideNpcContinueHint();
        HideLetterContinueHint();
        UnfreezeActorsFromNpcDialogue();

        if (frozenPlayer != null)
        {
            frozenPlayer.enabled = true;
            frozenPlayer = null;
        }
        npcContinueHintTarget = null;

        if (activeRoutinePausesGame)
        {
            Time.timeScale = 1f;
            activeRoutinePausesGame = false;
        }

        IsReading = false;
    }

    private void FreezeActorsForNpcDialogue()
    {
        UnfreezeActorsFromNpcDialogue();

        frozenPlayer = FindFirstObjectByType<PlayerMovement>();
        if (frozenPlayer != null)
        {
            Rigidbody2D playerRb = frozenPlayer.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
            }
            FreezeBehaviour(frozenPlayer);
        }

        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;
            GameObject enemyObj = enemies[i].gameObject;
            Rigidbody2D enemyRb = enemyObj.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                enemyRb.linearVelocity = Vector2.zero;
                enemyRb.angularVelocity = 0f;
            }

            FreezeBehaviour(enemyObj.GetComponent<BacteriaLogic>());
            FreezeBehaviour(enemyObj.GetComponent<CoralLogic>());
            FreezeBehaviour(enemyObj.GetComponent<BlueCoralLogic>());
            FreezeBehaviour(enemyObj.GetComponent<AmoebaLogic>());
        }
    }

    private void FreezeBehaviour(Behaviour behaviour)
    {
        if (behaviour == null) return;
        if (!frozenBehaviourLookup.Add(behaviour)) return;

        frozenBehaviours.Add(new FrozenBehaviourState(behaviour, behaviour.enabled));
        behaviour.enabled = false;
    }

    private void UnfreezeActorsFromNpcDialogue()
    {
        for (int i = frozenBehaviours.Count - 1; i >= 0; i--)
        {
            Behaviour behaviour = frozenBehaviours[i].Behaviour;
            if (behaviour != null)
            {
                behaviour.enabled = frozenBehaviours[i].WasEnabled;
            }
        }

        frozenBehaviours.Clear();
        frozenBehaviourLookup.Clear();
    }

    private void ShowNpcContinueHint()
    {
        if (npcContinueHintTarget == null) return;
        EnsureNpcContinueHintObject();
        if (npcContinueHintObject == null || npcContinueHintText == null) return;

        npcContinueHintText.text = string.IsNullOrWhiteSpace(npcContinueHintMessage)
            ? "Press E to continue."
            : npcContinueHintMessage;
        Color hintColor = npcContinueHintText.color;
        hintColor.a = npcContinueHintOpacity;
        npcContinueHintText.color = hintColor;
        npcContinueHintText.fontSize = Mathf.Max(0.2f, npcContinueHintFontSize);

        npcContinueHintObject.SetActive(true);
        UpdateNpcContinueHintPosition();
    }

    private void HideNpcContinueHint()
    {
        if (npcContinueHintObject != null && npcContinueHintObject.activeSelf)
        {
            npcContinueHintObject.SetActive(false);
        }
    }

    private void ShowLetterContinueHint()
    {
        EnsureLetterContinueHintObject();
        if (letterContinueHintObject == null || letterContinueHintText == null) return;

        letterContinueHintText.text = string.IsNullOrWhiteSpace(letterContinueHintMessage)
            ? "Press E to continue."
            : letterContinueHintMessage;
        Color hintColor = letterContinueHintText.color;
        hintColor.a = letterContinueHintOpacity;
        letterContinueHintText.color = hintColor;
        letterContinueHintText.fontSize = Mathf.Max(6f, letterContinueHintFontSize);

        letterContinueHintObject.SetActive(true);
        UpdateLetterContinueHintPosition();
    }

    private void HideLetterContinueHint()
    {
        if (letterContinueHintObject != null && letterContinueHintObject.activeSelf)
        {
            letterContinueHintObject.SetActive(false);
        }
    }

    private void UpdateLetterContinueHintPosition()
    {
        if (letterContinueHintRect == null) return;

        letterContinueHintRect.anchorMin = new Vector2(0.5f, 0f);
        letterContinueHintRect.anchorMax = new Vector2(0.5f, 0f);
        letterContinueHintRect.pivot = new Vector2(0.5f, 1f);
        letterContinueHintRect.anchoredPosition = letterContinueHintOffset;
    }

    private void UpdateNpcContinueHintPosition()
    {
        if (npcContinueHintObject == null || !npcContinueHintObject.activeSelf) return;
        if (npcContinueHintTarget == null)
        {
            HideNpcContinueHint();
            return;
        }

        npcContinueHintObject.transform.position = npcContinueHintTarget.position + npcContinueHintOffset;
    }

    private void EnsureNpcContinueHintObject()
    {
        if (npcContinueHintObject == null)
        {
            npcContinueHintObject = new GameObject("NPCContinueHint");
            npcContinueHintText = npcContinueHintObject.AddComponent<TextMeshPro>();
        }
        else if (npcContinueHintText == null)
        {
            npcContinueHintText = npcContinueHintObject.GetComponent<TextMeshPro>();
            if (npcContinueHintText == null) npcContinueHintText = npcContinueHintObject.AddComponent<TextMeshPro>();
        }

        npcContinueHintText.alignment = TextAlignmentOptions.Center;
        npcContinueHintText.enableAutoSizing = false;
        npcContinueHintText.fontSize = Mathf.Max(0.2f, npcContinueHintFontSize);
        npcContinueHintText.textWrappingMode = TextWrappingModes.NoWrap;
        npcContinueHintText.overflowMode = TextOverflowModes.Overflow;
        npcContinueHintText.fontStyle = FontStyles.Normal;
        npcContinueHintText.color = new Color(1f, 1f, 1f, npcContinueHintOpacity);

        if (textSlot != null && textSlot.font != null && npcContinueHintText.font == null)
        {
            npcContinueHintText.font = textSlot.font;
        }

        MeshRenderer renderer = npcContinueHintText.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = npcContinueHintSortingOrder;
        }
    }

    private void EnsureLetterContinueHintObject()
    {
        if (textSlot == null) return;

        if (letterContinueHintObject == null)
        {
            letterContinueHintObject = new GameObject("LetterContinueHint", typeof(RectTransform), typeof(CanvasRenderer));
            letterContinueHintRect = letterContinueHintObject.GetComponent<RectTransform>();
            letterContinueHintText = letterContinueHintObject.AddComponent<TextMeshProUGUI>();
            letterContinueHintRect.SetParent(textSlot.transform, false);
        }
        else
        {
            if (letterContinueHintRect == null) letterContinueHintRect = letterContinueHintObject.GetComponent<RectTransform>();
            if (letterContinueHintText == null) letterContinueHintText = letterContinueHintObject.GetComponent<TextMeshProUGUI>();
            if (letterContinueHintText == null) letterContinueHintText = letterContinueHintObject.AddComponent<TextMeshProUGUI>();
            if (letterContinueHintRect != null && letterContinueHintRect.parent != textSlot.transform)
            {
                letterContinueHintRect.SetParent(textSlot.transform, false);
            }
        }

        if (letterContinueHintText == null || letterContinueHintRect == null) return;

        letterContinueHintText.alignment = TextAlignmentOptions.Center;
        letterContinueHintText.enableAutoSizing = false;
        letterContinueHintText.fontSize = Mathf.Max(6f, letterContinueHintFontSize);
        letterContinueHintText.textWrappingMode = TextWrappingModes.NoWrap;
        letterContinueHintText.overflowMode = TextOverflowModes.Overflow;
        letterContinueHintText.fontStyle = FontStyles.Normal;
        letterContinueHintText.color = new Color(1f, 1f, 1f, letterContinueHintOpacity);
        if (textSlot.font != null) letterContinueHintText.font = textSlot.font;
        UpdateLetterContinueHintPosition();
    }

    private void ApplyLegacyHintSettingsIfNeeded()
    {
        // Migra valores anteriores para que el look actual se aplique en escenas ya configuradas.
        if (Mathf.Approximately(letterContinueHintFontSize, 26f))
        {
            letterContinueHintFontSize = 16f;
        }

        if (Mathf.Approximately(npcContinueHintFontSize, 1.6f))
        {
            npcContinueHintFontSize = 2.4f;
        }

        if (Mathf.Approximately(npcContinueHintOpacity, 0.45f))
        {
            npcContinueHintOpacity = 0.75f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLegacyHintSettingsIfNeeded();

        letterAutoSizeMin = Mathf.Clamp(letterAutoSizeMin, 1f, 300f);
        letterAutoSizeMax = Mathf.Clamp(letterAutoSizeMax, letterAutoSizeMin, 300f);
        npcContinueHintOpacity = Mathf.Clamp01(npcContinueHintOpacity);
        npcContinueHintFontSize = Mathf.Clamp(npcContinueHintFontSize, 0.2f, 100f);
        letterContinueHintOpacity = Mathf.Clamp01(letterContinueHintOpacity);
        letterContinueHintFontSize = Mathf.Clamp(letterContinueHintFontSize, 6f, 120f);
    }
#endif
}
