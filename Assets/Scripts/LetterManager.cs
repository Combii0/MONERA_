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
    private const string ContinuePromptMessage = "PRESS E TO CONTINUE";

    public bool IsReading { get; private set; } 

    [Header("Referencias UI")]
    [Tooltip("El Canvas de la carta (LetterCanvas) como GameObject.")]
    [SerializeField] private GameObject letterCanvasObject; 
    [Tooltip("El texto (SignText) donde se escribirá la historia")]
    [SerializeField] private TextMeshProUGUI textSlot;

    [Header("Configuración")]
    [SerializeField] private float revealCharsPerSecond = 38f;
    [SerializeField] private bool useAutoSizeForLetterText = false;
    [SerializeField] private float letterAutoSizeMin = 28f;
    [SerializeField] private float letterAutoSizeMax = 72f;
    [SerializeField] private bool useUnifiedMainTextSize = true;
    [SerializeField] private float unifiedMainTextSize = 46f;
    [SerializeField, Range(0.35f, 0.9f)] private float continueHintSizeMultiplier = 0.45f;

    [Header("Responsive (Carteles y NPCs)")]
    [SerializeField] private bool responsiveTextEnabled = false;
    [SerializeField] private float referenceScreenHeight = 1080f;
    [SerializeField] private Vector2 responsiveScaleClamp = new Vector2(0.85f, 1.9f);
    [SerializeField] private bool reapplyOnResolutionChange = false;

    [Header("Audio")]
    [SerializeField] private AudioClip typeSfx;
    [SerializeField, Range(0f, 1f)] private float typeSfxVolume = 0.65f;

    [Header("NPC Dialogo (Sin Pausa)")]
    [SerializeField] private bool freezeActorsDuringNpcDialogue = true;
    [SerializeField] private bool showNpcContinueHint = true;
    [SerializeField] private string npcContinueHintMessage = "Press E to continue.";
    [SerializeField] private Vector3 npcContinueHintOffset = new Vector3(0f, -2.1f, 0f);
    [SerializeField, Range(0f, 1f)] private float npcContinueHintOpacity = 0.75f;
    [SerializeField] private float npcContinueHintFontSize = 1.4f;
    [SerializeField, Range(0.5f, 1.5f)] public float worldContinueHintSizeMultiplier = 1f;
    [SerializeField] private int npcContinueHintSortingOrder = 200;
    [SerializeField] private Vector2 npcMainTextScreenOffset = new Vector2(0f, 120f);
    [SerializeField] private Vector2 letterMainTextScreenOffset = new Vector2(0f, 110f);
    [SerializeField] private float npcMainTextWidth = 760f;

    [Header("Cartel Dialogo (Con Pausa)")]
    [SerializeField] private bool showLetterContinueHint = true;
    [SerializeField] private string letterContinueHintMessage = "Press E to continue.";
    [SerializeField] private Vector2 letterContinueHintOffset = new Vector2(0f, -34f);
    [SerializeField] private Vector2 continueHintScreenOffsetFromPlayer = new Vector2(0f, -48f);
    [SerializeField, Range(0f, 1f)] private float letterContinueHintOpacity = 0.8f;
    [SerializeField] private float letterContinueHintFontSize = 30f;

    private Coroutine typeRoutine;
    private float nextSfxTime;
    private PlayerMovement frozenPlayer;
    private bool activeRoutinePausesGame;
    private readonly List<FrozenBehaviourState> frozenBehaviours = new List<FrozenBehaviourState>(64);
    private readonly HashSet<Behaviour> frozenBehaviourLookup = new HashSet<Behaviour>();
    private readonly List<FrozenRigidbodyState> frozenRigidbodies = new List<FrozenRigidbodyState>(64);
    private readonly HashSet<Rigidbody2D> frozenRigidbodyLookup = new HashSet<Rigidbody2D>();
    private GameObject npcContinueHintObject;
    private TextMeshPro npcContinueHintText;
    private Transform npcContinueHintTarget;
    private Transform activeMainTextTarget;
    private Transform activeNpcDialogueSource;
    private Transform activeLetterDialogueSource;
    private GameObject letterContinueHintObject;
    private TextMeshProUGUI letterContinueHintText;
    private RectTransform letterContinueHintRect;
    private float activeMessageFontSize = 38f;
    private bool isNpcDialogueActive;
    private bool hasTextSlotLayoutCache;
    private Vector2 textSlotDefaultAnchorMin;
    private Vector2 textSlotDefaultAnchorMax;
    private Vector2 textSlotDefaultPivot;
    private Vector2 textSlotDefaultAnchoredPosition;
    private Vector2 textSlotDefaultSizeDelta;
    private TextAlignmentOptions textSlotDefaultAlignment;
    private TextWrappingModes textSlotDefaultWrapping;
    private TextOverflowModes textSlotDefaultOverflow;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;

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

    private readonly struct FrozenRigidbodyState
    {
        public readonly Rigidbody2D Rigidbody;
        public readonly bool WasSimulated;

        public FrozenRigidbodyState(Rigidbody2D rigidbody, bool wasSimulated)
        {
            Rigidbody = rigidbody;
            WasSimulated = wasSimulated;
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

        EnsureLetterCanvasRootLayout();
        if (letterCanvasObject != null) letterCanvasObject.SetActive(false);
        CacheDefaultTextSlotLayout();
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
    }

    public void ReadLetter(string message, PlayerMovement player, float messageFontSize)
    {
        ReadLetter(message, player, messageFontSize, null);
    }

    public void ReadLetter(string message, PlayerMovement player, float messageFontSize, Transform dialogueSource)
    {
        if (textSlot == null)
        {
            Debug.LogError("LetterManager: ¡Falta asignar el SignText en el Inspector!");
            return;
        }
        if (string.IsNullOrEmpty(message)) return;

        if (IsReading) return; 

        IsReading = true; 
        activeLetterDialogueSource = dialogueSource;

        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(TypeRoutine(message, player, messageFontSize, true));
    }

    public void ReadDialogueNoPause(string message, float messageFontSize)
    {
        ReadDialogueNoPause(message, messageFontSize, null);
    }

    public void ReadDialogueNoPause(string message, float messageFontSize, Transform dialogueSource)
    {
        if (textSlot == null)
        {
            Debug.LogError("LetterManager: ¡Falta asignar el SignText en el Inspector!");
            return;
        }
        if (string.IsNullOrEmpty(message)) return;
        if (IsReading) return;

        IsReading = true;
        activeNpcDialogueSource = dialogueSource;

        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = StartCoroutine(TypeRoutine(message, null, messageFontSize, false));
    }

    private IEnumerator TypeRoutine(string message, PlayerMovement player, float messageFontSize, bool pauseGame)
    {
        activeMessageFontSize = ResolveMainMessageFontSize(messageFontSize);
        isNpcDialogueActive = !pauseGame;
        activeRoutinePausesGame = pauseGame;

        if (pauseGame)
        {
            activeMainTextTarget = ResolveLetterDialogueTarget();
            npcContinueHintTarget = player != null ? player.transform : ResolvePlayerDialogueTarget();
            if (activeMainTextTarget == null) RestoreDefaultTextSlotLayout();
            Time.timeScale = 0f;
            frozenPlayer = player;
            
            if (frozenPlayer != null)
            {
                Rigidbody2D rb = frozenPlayer.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero; 
                frozenPlayer.enabled = false; 
            }

            if (showLetterContinueHint) ShowLetterContinueHint();
            else HideLetterContinueHint();
            HideNpcContinueHint();
        }
        else
        {
            if (freezeActorsDuringNpcDialogue)
            {
                FreezeActorsForNpcDialogue();
            }

            Transform dialogueTarget = ResolveNpcDialogueTarget();
            activeMainTextTarget = dialogueTarget;
            npcContinueHintTarget = ResolvePlayerDialogueTarget();
            if (activeMainTextTarget == null) RestoreDefaultTextSlotLayout();

            if (showNpcContinueHint) ShowLetterContinueHint();
            else HideLetterContinueHint();
            HideNpcContinueHint();
        }

        EnsureLetterCanvasRootLayout();
        if (letterCanvasObject != null) letterCanvasObject.SetActive(true);
        ApplyReadingFontSettings(activeMessageFontSize);
        UpdateMainTextPosition();
        UpdateLetterContinueHintPosition();
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
            ReapplyLayoutOnResolutionChangeIfNeeded();

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

            UpdateMainTextPosition();
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
            ReapplyLayoutOnResolutionChangeIfNeeded();

            bool advancePressed = pauseGame ? WasInteractPressedThisFrame() : WasNpcContinuePressedThisFrame();
            if (advancePressed) break;

            UpdateMainTextPosition();
            UpdateLetterContinueHintPosition();
            UpdateNpcContinueHintPosition();
            // El juego se queda aquí pausado infinitamente hasta que el jugador presione el botón
            yield return null; 
        }

        // 3. FASE DE CIERRE
        if (letterCanvasObject != null) letterCanvasObject.SetActive(false);
        HideNpcContinueHint();
        HideLetterContinueHint();
        RestoreDefaultTextSlotLayout();
        
        if (frozenPlayer != null)
        {
            frozenPlayer.enabled = true; 
            frozenPlayer = null;
        }
        UnfreezeActorsFromNpcDialogue();
        npcContinueHintTarget = null;
        activeMainTextTarget = null;
        activeNpcDialogueSource = null;
        activeLetterDialogueSource = null;

        if (pauseGame) Time.timeScale = 1f;
        IsReading = false; 
        isNpcDialogueActive = false;
        activeRoutinePausesGame = false;
        activeMessageFontSize = 38f;
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

    private void EnsureLetterCanvasRootLayout()
    {
        if (letterCanvasObject == null) return;

        RectTransform canvasRect = letterCanvasObject.transform as RectTransform;
        if (canvasRect == null) return;

        if (canvasRect.localScale.sqrMagnitude <= 0.0001f)
        {
            canvasRect.localScale = Vector3.one;
        }

        if (canvasRect.anchorMin == Vector2.zero &&
            canvasRect.anchorMax == Vector2.zero &&
            canvasRect.sizeDelta == Vector2.zero)
        {
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.anchoredPosition = Vector2.zero;
            canvasRect.sizeDelta = Vector2.zero;
        }
    }

    private void CleanupStateIfInterrupted()
    {
        HideNpcContinueHint();
        HideLetterContinueHint();
        RestoreDefaultTextSlotLayout();
        UnfreezeActorsFromNpcDialogue();

        if (frozenPlayer != null)
        {
            frozenPlayer.enabled = true;
            frozenPlayer = null;
        }
        npcContinueHintTarget = null;
        activeMainTextTarget = null;
        activeNpcDialogueSource = null;
        activeLetterDialogueSource = null;

        if (activeRoutinePausesGame)
        {
            Time.timeScale = 1f;
            activeRoutinePausesGame = false;
        }

        IsReading = false;
        isNpcDialogueActive = false;
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

        FreezeEnemyBehaviours(FindObjectsByType<BacteriaLogic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeEnemyBehaviours(FindObjectsByType<CoralLogic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeEnemyBehaviours(FindObjectsByType<BlueCoralLogic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeEnemyBehaviours(FindObjectsByType<AmoebaLogic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        FreezeEnemyBehaviours(FindObjectsByType<EnemyBullet>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
    }

    private void FreezeEnemyBehaviours<T>(T[] enemyBehaviours) where T : Behaviour
    {
        if (enemyBehaviours == null) return;

        for (int i = 0; i < enemyBehaviours.Length; i++)
        {
            T behaviour = enemyBehaviours[i];
            if (behaviour == null) continue;

            FreezeBehaviour(behaviour);

            Rigidbody2D body = behaviour.GetComponent<Rigidbody2D>();
            if (body == null) body = behaviour.GetComponentInParent<Rigidbody2D>();
            FreezeRigidbody(body);
        }
    }

    private void FreezeBehaviour(Behaviour behaviour)
    {
        if (behaviour == null) return;
        if (!frozenBehaviourLookup.Add(behaviour)) return;

        frozenBehaviours.Add(new FrozenBehaviourState(behaviour, behaviour.enabled));
        behaviour.enabled = false;
    }

    private void FreezeRigidbody(Rigidbody2D body)
    {
        if (body == null) return;
        if (!frozenRigidbodyLookup.Add(body)) return;

        frozenRigidbodies.Add(new FrozenRigidbodyState(body, body.simulated));
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
    }

    private void UnfreezeActorsFromNpcDialogue()
    {
        for (int i = frozenRigidbodies.Count - 1; i >= 0; i--)
        {
            Rigidbody2D body = frozenRigidbodies[i].Rigidbody;
            if (body != null)
            {
                body.simulated = frozenRigidbodies[i].WasSimulated;
                if (body.simulated)
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
        }

        frozenRigidbodies.Clear();
        frozenRigidbodyLookup.Clear();

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

        bool useNpcHintStyle = isNpcDialogueActive;
        string configuredMessage = useNpcHintStyle ? npcContinueHintMessage : letterContinueHintMessage;
        float configuredOpacity = useNpcHintStyle ? npcContinueHintOpacity : letterContinueHintOpacity;

        npcContinueHintText.text = ResolveContinuePromptMessage(configuredMessage);
        Color hintColor = npcContinueHintText.color;
        hintColor.a = configuredOpacity;
        npcContinueHintText.color = hintColor;
        npcContinueHintText.fontSize = ResolveWorldContinueHintFontSize(npcContinueHintFontSize);

        SyncNpcContinueHintSortingWithTarget();
        if (npcContinueHintObject.transform.parent != npcContinueHintTarget)
        {
            npcContinueHintObject.transform.SetParent(npcContinueHintTarget, false);
        }
        npcContinueHintObject.transform.localPosition = npcContinueHintOffset;
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

        bool useNpcHintStyle = isNpcDialogueActive;
        string configuredMessage = useNpcHintStyle ? npcContinueHintMessage : letterContinueHintMessage;
        float configuredOpacity = useNpcHintStyle ? npcContinueHintOpacity : letterContinueHintOpacity;
        float configuredHintSize = useNpcHintStyle ? npcContinueHintFontSize : letterContinueHintFontSize;

        letterContinueHintText.text = ResolveContinuePromptMessage(configuredMessage);
        Color hintColor = letterContinueHintText.color;
        hintColor.a = configuredOpacity;
        letterContinueHintText.color = hintColor;
        letterContinueHintText.fontSize = ResolveContinueHintFontSize(configuredHintSize, 6f);

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

        RectTransform parentRect = letterContinueHintRect.parent as RectTransform;
        if (IsReading &&
            npcContinueHintTarget != null &&
            TryGetAnchoredPositionInRect(GetDialogueAnchorWorldPosition(npcContinueHintTarget), parentRect, out Vector2 anchoredPosition))
        {
            Vector2 dynamicOffset = continueHintScreenOffsetFromPlayer;
            letterContinueHintRect.anchorMin = new Vector2(0.5f, 0.5f);
            letterContinueHintRect.anchorMax = new Vector2(0.5f, 0.5f);
            letterContinueHintRect.pivot = new Vector2(0.5f, 0.5f);
            letterContinueHintRect.anchoredPosition = anchoredPosition + dynamicOffset;
            return;
        }

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

        if (npcContinueHintObject.transform.parent != npcContinueHintTarget)
        {
            npcContinueHintObject.transform.position = npcContinueHintTarget.position + npcContinueHintOffset;
            return;
        }

        npcContinueHintObject.transform.localPosition = npcContinueHintOffset;
    }

    private void UpdateMainTextPosition()
    {
        if (!IsReading || textSlot == null) return;
        if (activeMainTextTarget == null) return;

        RectTransform textRect = textSlot.rectTransform;
        RectTransform parentRect = textRect.parent as RectTransform;
        if (!TryGetAnchoredPositionInRect(GetDialogueAnchorWorldPosition(activeMainTextTarget), parentRect, out Vector2 anchoredPosition)) return;

        Vector2 mainTextOffset = isNpcDialogueActive ? npcMainTextScreenOffset : letterMainTextScreenOffset;

        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = anchoredPosition + mainTextOffset;

        if (npcMainTextWidth > 0f)
        {
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, npcMainTextWidth);
        }

        textSlot.alignment = TextAlignmentOptions.Center;
        textSlot.textWrappingMode = TextWrappingModes.Normal;
        textSlot.overflowMode = TextOverflowModes.Overflow;
    }

    private Transform ResolvePlayerDialogueTarget()
    {
        if (frozenPlayer != null) return frozenPlayer.transform;

        PlayerMovement playerTarget = FindFirstObjectByType<PlayerMovement>();
        return playerTarget != null ? playerTarget.transform : null;
    }

    private Transform ResolveNpcDialogueTarget()
    {
        return activeNpcDialogueSource;
    }

    private Transform ResolveLetterDialogueTarget()
    {
        return activeLetterDialogueSource;
    }

    private static Vector3 GetDialogueAnchorWorldPosition(Transform target)
    {
        if (target == null) return Vector3.zero;

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider == null) targetCollider = target.GetComponentInChildren<Collider2D>();
        if (targetCollider == null) targetCollider = target.GetComponentInParent<Collider2D>();
        if (targetCollider != null)
        {
            Bounds bounds = targetCollider.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, target.position.z);
        }

        SpriteRenderer targetRenderer = target.GetComponentInChildren<SpriteRenderer>();
        if (targetRenderer == null) targetRenderer = target.GetComponentInParent<SpriteRenderer>();
        if (targetRenderer != null)
        {
            Bounds bounds = targetRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, target.position.z);
        }

        return target.position;
    }

    private bool TryGetAnchoredPositionInRect(Vector3 worldPosition, RectTransform targetRect, out Vector2 anchoredPosition)
    {
        anchoredPosition = Vector2.zero;
        if (targetRect == null) return false;

        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        if (canvas == null) return false;

        Camera renderCamera = null;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            renderCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(renderCamera, worldPosition);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, screenPoint, renderCamera, out anchoredPosition);
    }

    private void CacheDefaultTextSlotLayout()
    {
        if (textSlot == null || hasTextSlotLayoutCache) return;

        RectTransform textRect = textSlot.rectTransform;
        textSlotDefaultAnchorMin = textRect.anchorMin;
        textSlotDefaultAnchorMax = textRect.anchorMax;
        textSlotDefaultPivot = textRect.pivot;
        textSlotDefaultAnchoredPosition = textRect.anchoredPosition;
        textSlotDefaultSizeDelta = textRect.sizeDelta;
        textSlotDefaultAlignment = textSlot.alignment;
        textSlotDefaultWrapping = textSlot.textWrappingMode;
        textSlotDefaultOverflow = textSlot.overflowMode;
        hasTextSlotLayoutCache = true;
    }

    private void RestoreDefaultTextSlotLayout()
    {
        if (textSlot == null || !hasTextSlotLayoutCache) return;

        RectTransform textRect = textSlot.rectTransform;
        textRect.anchorMin = textSlotDefaultAnchorMin;
        textRect.anchorMax = textSlotDefaultAnchorMax;
        textRect.pivot = textSlotDefaultPivot;
        textRect.anchoredPosition = textSlotDefaultAnchoredPosition;
        textRect.sizeDelta = textSlotDefaultSizeDelta;
        textSlot.alignment = textSlotDefaultAlignment;
        textSlot.textWrappingMode = textSlotDefaultWrapping;
        textSlot.overflowMode = textSlotDefaultOverflow;
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
        npcContinueHintText.fontSize = ResolveWorldContinueHintFontSize(npcContinueHintFontSize);
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

    private void SyncNpcContinueHintSortingWithTarget()
    {
        if (npcContinueHintText == null || npcContinueHintTarget == null) return;

        MeshRenderer hintRenderer = npcContinueHintText.GetComponent<MeshRenderer>();
        if (hintRenderer == null) return;

        SpriteRenderer targetRenderer = npcContinueHintTarget.GetComponentInParent<SpriteRenderer>();
        if (targetRenderer != null)
        {
            hintRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            hintRenderer.sortingOrder = targetRenderer.sortingOrder + npcContinueHintSortingOrder;
        }
        else
        {
            hintRenderer.sortingOrder = npcContinueHintSortingOrder;
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
        }
        else
        {
            if (letterContinueHintRect == null) letterContinueHintRect = letterContinueHintObject.GetComponent<RectTransform>();
            if (letterContinueHintText == null) letterContinueHintText = letterContinueHintObject.GetComponent<TextMeshProUGUI>();
            if (letterContinueHintText == null) letterContinueHintText = letterContinueHintObject.AddComponent<TextMeshProUGUI>();
        }

        if (letterContinueHintText == null || letterContinueHintRect == null) return;

        Transform desiredParent = textSlot.canvas != null ? textSlot.canvas.transform : textSlot.transform.parent;
        if (desiredParent != null && letterContinueHintRect.parent != desiredParent)
        {
            letterContinueHintRect.SetParent(desiredParent, false);
        }

        letterContinueHintText.alignment = TextAlignmentOptions.Center;
        letterContinueHintText.raycastTarget = false;
        letterContinueHintText.enableAutoSizing = false;
        letterContinueHintText.fontSize = ResolveContinueHintFontSize(letterContinueHintFontSize, 6f);
        letterContinueHintText.textWrappingMode = TextWrappingModes.NoWrap;
        letterContinueHintText.overflowMode = TextOverflowModes.Overflow;
        letterContinueHintText.fontStyle = FontStyles.Normal;
        letterContinueHintText.color = new Color(1f, 1f, 1f, letterContinueHintOpacity);
        if (textSlot.font != null) letterContinueHintText.font = textSlot.font;
        if (letterContinueHintRect.sizeDelta.sqrMagnitude < 1f)
        {
            letterContinueHintRect.sizeDelta = new Vector2(760f, 80f);
        }
        letterContinueHintRect.SetAsLastSibling();
        UpdateLetterContinueHintPosition();
    }

    private void ApplyReadingFontSettings(float messageFontSize)
    {
        if (textSlot == null) return;
        float resolvedMainSize = ResolveMainMessageFontSize(messageFontSize);

        if (useUnifiedMainTextSize)
        {
            textSlot.enableAutoSizing = false;
            textSlot.fontSize = resolvedMainSize;
            return;
        }

        if (useAutoSizeForLetterText)
        {
            float minSize = Mathf.Clamp(GetResponsiveFontSize(letterAutoSizeMin, 1f), 1f, 300f);
            float maxSize = Mathf.Clamp(GetResponsiveFontSize(letterAutoSizeMax, minSize), minSize, 300f);
            textSlot.enableAutoSizing = true;
            textSlot.fontSizeMin = minSize;
            textSlot.fontSizeMax = maxSize;
        }
        else
        {
            textSlot.enableAutoSizing = false;
            textSlot.fontSize = GetResponsiveFontSize(resolvedMainSize, 6f);
        }
    }

    private float ResolveMainMessageFontSize(float requestedFontSize)
    {
        if (useUnifiedMainTextSize)
        {
            return Mathf.Max(6f, unifiedMainTextSize);
        }

        return Mathf.Max(6f, requestedFontSize);
    }

    private float ResolveContinueHintFontSize(float fallbackHintSize, float minValue)
    {
        float mainSize = ResolveMainMessageFontSize(activeMessageFontSize);
        float derivedHintSize = mainSize * continueHintSizeMultiplier;
        return Mathf.Max(minValue, derivedHintSize);
    }

    private float ResolveWorldContinueHintFontSize(float fallbackHintSize)
    {
        float baseWorldSize = Mathf.Max(0.2f, fallbackHintSize);
        return Mathf.Max(0.2f, baseWorldSize * worldContinueHintSizeMultiplier);
    }

    private static string ResolveContinuePromptMessage(string configuredMessage)
    {
        if (string.IsNullOrWhiteSpace(configuredMessage)) return ContinuePromptMessage;
        string normalized = configuredMessage.Trim().TrimEnd('.', '!', '?').ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? ContinuePromptMessage : normalized;
    }

    private float GetResponsiveFontSize(float baseSize, float minValue)
    {
        float resolvedBase = Mathf.Max(minValue, baseSize);
        if (!responsiveTextEnabled) return resolvedBase;

        float safeReferenceHeight = Mathf.Max(120f, referenceScreenHeight);
        float scale = Screen.height / safeReferenceHeight;
        scale = Mathf.Clamp(scale, responsiveScaleClamp.x, responsiveScaleClamp.y);
        return Mathf.Max(minValue, resolvedBase * scale);
    }

    private void ReapplyLayoutOnResolutionChangeIfNeeded()
    {
        if (!reapplyOnResolutionChange || !IsReading) return;

        int currentWidth = Screen.width;
        int currentHeight = Screen.height;
        if (currentWidth == cachedScreenWidth && currentHeight == cachedScreenHeight) return;

        cachedScreenWidth = currentWidth;
        cachedScreenHeight = currentHeight;

        ApplyReadingFontSettings(activeMessageFontSize);
        UpdateMainTextPosition();
        UpdateLetterContinueHintPosition();
        UpdateNpcContinueHintPosition();
    }

    private void ApplyLegacyHintSettingsIfNeeded()
    {
        // Migra configuraciones antiguas que dejaban texto diminuto en carteles/NPC.
        if (letterAutoSizeMin <= 10.01f && letterAutoSizeMax <= 18.01f)
        {
            letterAutoSizeMin = 28f;
            letterAutoSizeMax = 72f;
        }

        if (npcContinueHintFontSize <= 0.2f || npcContinueHintFontSize > 8f)
        {
            npcContinueHintFontSize = 1.4f;
        }

        if (letterContinueHintFontSize <= 16.01f)
        {
            letterContinueHintFontSize = 22f;
        }

        if (Mathf.Approximately(npcContinueHintOpacity, 0.45f))
        {
            npcContinueHintOpacity = 0.75f;
        }

        if (unifiedMainTextSize < 34f)
        {
            unifiedMainTextSize = 46f;
        }

        if (continueHintSizeMultiplier <= 0f)
        {
            continueHintSizeMultiplier = 0.45f;
        }

        if (worldContinueHintSizeMultiplier > 1.5f)
        {
            worldContinueHintSizeMultiplier = 1f;
        }

        letterContinueHintFontSize = unifiedMainTextSize * continueHintSizeMultiplier;
        useUnifiedMainTextSize = true;
        useAutoSizeForLetterText = false;
        responsiveTextEnabled = false;
        reapplyOnResolutionChange = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLegacyHintSettingsIfNeeded();
        EnsureLetterCanvasRootLayout();

        letterAutoSizeMin = Mathf.Clamp(letterAutoSizeMin, 1f, 300f);
        letterAutoSizeMax = Mathf.Clamp(letterAutoSizeMax, letterAutoSizeMin, 300f);
        referenceScreenHeight = Mathf.Clamp(referenceScreenHeight, 120f, 4320f);
        responsiveScaleClamp.x = Mathf.Clamp(responsiveScaleClamp.x, 0.2f, 8f);
        responsiveScaleClamp.y = Mathf.Clamp(responsiveScaleClamp.y, responsiveScaleClamp.x, 8f);
        npcContinueHintOpacity = Mathf.Clamp01(npcContinueHintOpacity);
        npcContinueHintFontSize = Mathf.Clamp(npcContinueHintFontSize, 0.2f, 8f);
        worldContinueHintSizeMultiplier = Mathf.Clamp(worldContinueHintSizeMultiplier, 0.5f, 1.5f);
        letterContinueHintOpacity = Mathf.Clamp01(letterContinueHintOpacity);
        letterContinueHintFontSize = Mathf.Clamp(letterContinueHintFontSize, 6f, 120f);
        unifiedMainTextSize = Mathf.Clamp(unifiedMainTextSize, 6f, 200f);
        continueHintSizeMultiplier = Mathf.Clamp(continueHintSizeMultiplier, 0.35f, 0.9f);
        npcMainTextWidth = Mathf.Clamp(npcMainTextWidth, 200f, 2200f);
        letterMainTextScreenOffset.y = Mathf.Clamp(letterMainTextScreenOffset.y, 10f, 500f);
        continueHintScreenOffsetFromPlayer.y = Mathf.Clamp(continueHintScreenOffsetFromPlayer.y, -500f, -10f);
    }
#endif
}
