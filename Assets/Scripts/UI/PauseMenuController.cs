using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.04f, 0.07f, 0.15f, 0.88f);
    private static readonly Color PanelColor = new Color(0.13f, 0.17f, 0.30f, 0.98f);
    private static readonly Color PanelAccentColor = new Color(0.46f, 0.80f, 0.99f, 1f);
    private static readonly Color SubtitleColor = new Color(0.84f, 0.93f, 1f, 0.92f);
    private static readonly Color ResumeColor = new Color(0.36f, 0.70f, 1f, 1f);
    private static readonly Color ResumeHoverColor = new Color(0.16f, 0.34f, 0.70f, 1f);
    private static readonly Color SettingsColor = new Color(0.73f, 0.75f, 0.80f, 1f);
    private static readonly Color SettingsHoverColor = new Color(0.50f, 0.53f, 0.60f, 1f);
    private static readonly Color MenuColor = new Color(0.98f, 0.86f, 0.33f, 1f);
    private static readonly Color MenuHoverColor = new Color(0.97f, 0.59f, 0.22f, 1f);
    private static readonly Color QuitColor = new Color(0.95f, 0.63f, 0.42f, 1f);
    private static readonly Color QuitHoverColor = new Color(0.84f, 0.36f, 0.25f, 1f);
    private static readonly Color UtilityBlueColor = new Color(0.50f, 0.76f, 0.97f, 1f);
    private static readonly Color UtilityBlueHoverColor = new Color(0.24f, 0.48f, 0.76f, 1f);
    private static readonly Color UtilityGrayColor = new Color(0.78f, 0.82f, 0.89f, 1f);
    private static readonly Color UtilityGrayHoverColor = new Color(0.58f, 0.62f, 0.72f, 1f);
    private static readonly Vector2 ButtonSize = new Vector2(320f, 70f);

    private static PauseMenuController instance;

    [Header("Theme")]
    [SerializeField] private TMP_FontAsset pixelFontAsset;

    [Header("Gameplay")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private Behaviour[] additionalBehavioursToDisable;

    [Header("Input")]
    [SerializeField] private InputActionReference pauseActionReference;
    [SerializeField] private bool useRuntimeFallbackPauseBinding = true;

    [Header("Cursor")]
    [SerializeField] private bool showCursorWhilePaused = true;

    private readonly List<Behaviour> gameplayBehaviours = new List<Behaviour>(8);
    private readonly List<BehaviourPauseState> pausedStates = new List<BehaviourPauseState>(8);

    private InputAction runtimePauseAction;
    private InputAction activePauseAction;
    private bool enabledPauseActionInternally;
    private bool isPaused;
    private bool cursorWasVisible;
    private CursorLockMode cursorLockModeBeforePause;
    private bool generatedPauseMenuRoot;

    private GameObject pauseMenuRoot;
    private GameObject mainPanel;
    private GameObject settingsPanel;
    private RectTransform mainCardRect;
    private RectTransform settingsCardRect;
    private Vector2 mainCardBasePosition;
    private Vector2 settingsCardBasePosition;
    private RectTransform volumeArtRect;
    private TextMeshProUGUI mainTitleText;
    private TextMeshProUGUI settingsTitleText;
    private TextMeshProUGUI volumeValueText;
    private TextMeshProUGUI volumeModeButtonText;
    private TextMeshProUGUI holdFireToggleButtonText;
    private Slider volumeSlider;
    private TMP_InputField volumeInputField;
    private GameObject volumeSliderRoot;
    private GameObject volumeNumberRoot;
    private ButtonRefs resumeButton;
    private ButtonRefs settingsButton;
    private ButtonRefs menuButton;
    private ButtonRefs quitButton;
    private ButtonRefs settingsBackButton;
    private ButtonRefs volumeModeButton;
    private ButtonRefs holdFireToggleButton;
    private Coroutine panelAnimationRoutine;

    public bool IsPaused => isPaused;

    private struct BehaviourPauseState
    {
        public Behaviour behaviour;
        public bool wasEnabled;
    }

    private struct ButtonRefs
    {
        public GameObject root;
        public Button button;
        public TextMeshProUGUI label;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<PauseMenuController>() != null) return;

        GameObject controllerObject = new GameObject("PauseMenuController");
        DontDestroyOnLoad(controllerObject);
        controllerObject.AddComponent<PauseMenuController>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            instance.CopyConfigurationFrom(this);
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        LoadThemeDefaultsIfNeeded();
        ResolveReferences();
        CacheGameplayBehaviours();
        EnsureEventSystemExists();
        EnsureGeneratedMenuExists();
        SetPauseMenuVisible(false);
        RefreshSettingsUi();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameSettings.Changed += HandleGameSettingsChanged;
        BindPauseAction();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameSettings.Changed -= HandleGameSettingsChanged;
        UnbindPauseAction();

        if (isPaused)
        {
            ForceResumeState();
        }
    }

    private void OnDestroy()
    {
        if (runtimePauseAction != null)
        {
            runtimePauseAction.Dispose();
            runtimePauseAction = null;
        }

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        AnimateVisiblePanels();
    }

    private void BindPauseAction()
    {
        activePauseAction = pauseActionReference != null ? pauseActionReference.action : null;
        enabledPauseActionInternally = false;

        if (activePauseAction == null && useRuntimeFallbackPauseBinding)
        {
            if (runtimePauseAction == null)
            {
                runtimePauseAction = new InputAction(name: "Pause", type: InputActionType.Button);
                runtimePauseAction.AddBinding("<Keyboard>/escape");
                runtimePauseAction.AddBinding("<Gamepad>/start");
            }

            runtimePauseAction.Enable();
            activePauseAction = runtimePauseAction;
        }

        if (activePauseAction == null) return;

        if (!activePauseAction.enabled)
        {
            activePauseAction.Enable();
            enabledPauseActionInternally = true;
        }

        activePauseAction.performed += OnPausePerformed;
    }

    private void UnbindPauseAction()
    {
        if (activePauseAction != null)
        {
            activePauseAction.performed -= OnPausePerformed;
        }

        if (activePauseAction == runtimePauseAction)
        {
            runtimePauseAction.Disable();
        }
        else if (activePauseAction != null && enabledPauseActionInternally)
        {
            activePauseAction.Disable();
        }

        activePauseAction = null;
        enabledPauseActionInternally = false;
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (!isPaused)
        {
            PauseGame();
            return;
        }

        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            BackToPauseHome();
            return;
        }

        ResumeGame();
    }

    public void PauseGame()
    {
        if (isPaused || !CanPauseNow()) return;

        LoadThemeDefaultsIfNeeded();
        ResolveReferences();
        CacheGameplayBehaviours();
        EnsureGeneratedMenuExists();
        RefreshSettingsUi();
        StoreAndDisableGameplayBehaviours();

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
        }

        CacheCursorState();
        isPaused = true;
        Time.timeScale = 0f;

        if (showCursorWhilePaused)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        SetPauseMenuVisible(true);
        OpenMainPanel(true);
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        ForceResumeState();
    }

    public void OpenSettings()
    {
        if (!isPaused) return;
        EnsureGeneratedMenuExists();
        RefreshSettingsUi();

        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);

        if (settingsCardRect != null)
        {
            settingsCardRect.anchoredPosition = settingsCardBasePosition;
            StartPanelPop(settingsCardRect);
        }

        SelectObject(volumeModeButton.root != null ? volumeModeButton.root : settingsBackButton.root);
    }

    public void BackToPauseHome()
    {
        if (!isPaused) return;
        OpenMainPanel(true);
    }

    public void ToggleVolumeInputMode()
    {
        GameSettings.VolumeInputMode nextMode = GameSettings.VolumeMode == GameSettings.VolumeInputMode.Slider
            ? GameSettings.VolumeInputMode.Number
            : GameSettings.VolumeInputMode.Slider;

        GameSettings.SetVolumeInputMode(nextMode);
    }

    public void ToggleHoldFire()
    {
        GameSettings.SetAllowHoldFire(!GameSettings.AllowHoldFire);
    }

    public void RestartLevel()
    {
        ForceResumeState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadRestartScene();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        ForceResumeState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadMenuScene();
            return;
        }

        SceneManager.LoadScene(0);
    }

    public void QuitGame()
    {
        ForceResumeState();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandleGameSettingsChanged()
    {
        RefreshSettingsUi();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isPaused)
        {
            ForceResumeState();
        }

        ResolveReferences();
        CacheGameplayBehaviours();
        EnsureEventSystemExists();
        EnsureGeneratedMenuExists();
        RefreshSettingsUi();
        SetPauseMenuVisible(false);
    }

    private bool CanPauseNow()
    {
        if (!gameObject.activeInHierarchy) return false;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsReading) return false;
        if (LetterManager.Instance != null && LetterManager.Instance.IsReading) return false;
        if (Time.timeScale <= 0f) return false;

        return true;
    }

    private void ForceResumeState()
    {
        isPaused = false;
        SetPauseMenuVisible(false);
        RestoreGameplayBehaviours();
        RestoreCursorState();
        Time.timeScale = 1f;

        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        ClearSelectedObject();
    }

    private void LoadThemeDefaultsIfNeeded()
    {
        if (pixelFontAsset == null)
        {
            pixelFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/BoldPixels SDF");
        }

        if (pixelFontAsset == null)
        {
            pixelFontAsset = TMP_Settings.defaultFontAsset;
        }
    }

    private void CopyConfigurationFrom(PauseMenuController other)
    {
        if (other == null) return;

        if (other.pixelFontAsset != null) pixelFontAsset = other.pixelFontAsset;
        if (other.pauseActionReference != null) pauseActionReference = other.pauseActionReference;
        if (other.additionalBehavioursToDisable != null && other.additionalBehavioursToDisable.Length > 0)
        {
            additionalBehavioursToDisable = other.additionalBehavioursToDisable;
        }

        if (generatedPauseMenuRoot && pauseMenuRoot != null)
        {
            Destroy(pauseMenuRoot);
            ResetGeneratedUiReferences();
            EnsureGeneratedMenuExists();
            RefreshSettingsUi();
        }
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
        }

        if (playerRigidbody == null && player != null)
        {
            playerRigidbody = player.GetComponent<Rigidbody2D>();
        }
    }

    private void CacheGameplayBehaviours()
    {
        gameplayBehaviours.Clear();

        AddGameplayBehaviour(player);

        if (player != null)
        {
            AddGameplayBehaviour(player.GetComponent<PlayerShooting>());
            AddGameplayBehaviour(player.GetComponent<PlayerAnimation>());
            AddGameplayBehaviour(player.GetComponent<PlayerHealth>());
        }

        if (additionalBehavioursToDisable == null) return;

        for (int i = 0; i < additionalBehavioursToDisable.Length; i++)
        {
            AddGameplayBehaviour(additionalBehavioursToDisable[i]);
        }
    }

    private void AddGameplayBehaviour(Behaviour behaviour)
    {
        if (behaviour == null || behaviour == this) return;

        for (int i = 0; i < gameplayBehaviours.Count; i++)
        {
            if (gameplayBehaviours[i] == behaviour) return;
        }

        gameplayBehaviours.Add(behaviour);
    }

    private void StoreAndDisableGameplayBehaviours()
    {
        pausedStates.Clear();

        for (int i = 0; i < gameplayBehaviours.Count; i++)
        {
            Behaviour behaviour = gameplayBehaviours[i];
            if (behaviour == null) continue;

            pausedStates.Add(new BehaviourPauseState
            {
                behaviour = behaviour,
                wasEnabled = behaviour.enabled
            });

            behaviour.enabled = false;
        }
    }

    private void RestoreGameplayBehaviours()
    {
        for (int i = 0; i < pausedStates.Count; i++)
        {
            BehaviourPauseState state = pausedStates[i];
            if (state.behaviour == null) continue;

            state.behaviour.enabled = state.wasEnabled;
        }

        pausedStates.Clear();
    }

    private void CacheCursorState()
    {
        cursorWasVisible = Cursor.visible;
        cursorLockModeBeforePause = Cursor.lockState;
    }

    private void RestoreCursorState()
    {
        if (!showCursorWhilePaused) return;

        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorLockModeBeforePause;
    }

    private void SetPauseMenuVisible(bool visible)
    {
        if (pauseMenuRoot == null) return;
        if (pauseMenuRoot.activeSelf == visible) return;
        pauseMenuRoot.SetActive(visible);
    }

    private void OpenMainPanel(bool animate)
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (mainCardRect != null)
        {
            mainCardRect.anchoredPosition = mainCardBasePosition;
            if (animate) StartPanelPop(mainCardRect);
        }

        SelectObject(resumeButton.root);
    }

    private void StartPanelPop(RectTransform target)
    {
        if (target == null) return;

        if (panelAnimationRoutine != null)
        {
            StopCoroutine(panelAnimationRoutine);
        }

        panelAnimationRoutine = StartCoroutine(PanelPopRoutine(target));
    }

    private IEnumerator PanelPopRoutine(RectTransform target)
    {
        target.localScale = Vector3.one * 0.82f;
        const float duration = 0.18f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float scale = t < 0.65f
                ? Mathf.Lerp(0.82f, 1.08f, t / 0.65f)
                : Mathf.Lerp(1.08f, 1f, (t - 0.65f) / 0.35f);

            target.localScale = Vector3.one * scale;
            yield return null;
        }

        target.localScale = Vector3.one;
        panelAnimationRoutine = null;
    }

    private void AnimateVisiblePanels()
    {
        if (!isPaused) return;

        if (mainPanel != null && mainPanel.activeSelf && mainCardRect != null)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 1.8f) * 6f;
            mainCardRect.anchoredPosition = mainCardBasePosition + new Vector2(0f, bob);

            if (mainTitleText != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.015f;
                mainTitleText.rectTransform.localScale = Vector3.one * pulse;
            }
        }

        if (settingsPanel != null && settingsPanel.activeSelf && settingsCardRect != null)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 1.8f) * 6f;
            settingsCardRect.anchoredPosition = settingsCardBasePosition + new Vector2(0f, bob);

            if (settingsTitleText != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.015f;
                settingsTitleText.rectTransform.localScale = Vector3.one * pulse;
            }

            if (volumeArtRect != null)
            {
                float angle = Mathf.Sin(Time.unscaledTime * 1.6f) * 4f;
                float scale = 1f + Mathf.Sin(Time.unscaledTime * 2.1f) * 0.03f;
                volumeArtRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                volumeArtRect.localScale = Vector3.one * scale;
            }
        }
    }

    private void RefreshSettingsUi()
    {
        int volumePercent = Mathf.RoundToInt(GameSettings.MasterVolume * 100f);

        if (volumeValueText != null)
        {
            volumeValueText.text = $"{volumePercent}%";
        }

        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
        }

        if (volumeInputField != null)
        {
            volumeInputField.SetTextWithoutNotify(volumePercent.ToString());
        }

        if (volumeModeButtonText != null)
        {
            volumeModeButtonText.text = GameSettings.VolumeMode == GameSettings.VolumeInputMode.Slider
                ? "MODE: SLIDER"
                : "MODE: NUMBER";
        }

        if (volumeSliderRoot != null)
        {
            volumeSliderRoot.SetActive(GameSettings.VolumeMode == GameSettings.VolumeInputMode.Slider);
        }

        if (volumeNumberRoot != null)
        {
            volumeNumberRoot.SetActive(GameSettings.VolumeMode == GameSettings.VolumeInputMode.Number);
        }

        if (holdFireToggleButtonText != null)
        {
            holdFireToggleButtonText.text = GameSettings.AllowHoldFire
                ? "HOLD FIRE: ON"
                : "HOLD FIRE: OFF";
        }
    }

    private void OnVolumeSliderChanged(float value)
    {
        GameSettings.SetMasterVolume(value);
    }

    private void OnVolumeNumberSubmitted(string value)
    {
        if (!int.TryParse(value, out int volumePercent))
        {
            RefreshSettingsUi();
            return;
        }

        GameSettings.SetMasterVolume(Mathf.Clamp(volumePercent, 0, 100) / 100f);
    }

    private void EnsureGeneratedMenuExists()
    {
        if (pauseMenuRoot != null && mainPanel != null && settingsPanel != null) return;

        Canvas canvas = CreateRootCanvas();
        pauseMenuRoot = canvas.gameObject;
        generatedPauseMenuRoot = true;

        mainPanel = CreateCardPanel(pauseMenuRoot.transform, "PauseMainPanel", new Vector2(760f, 760f), out mainCardRect);
        settingsPanel = CreateCardPanel(pauseMenuRoot.transform, "PauseSettingsPanel", new Vector2(820f, 760f), out settingsCardRect);
        settingsPanel.SetActive(false);

        mainCardBasePosition = Vector2.zero;
        settingsCardBasePosition = Vector2.zero;

        BuildMainPanel(mainPanel.transform);
        BuildSettingsPanel(settingsPanel.transform);
        SetPauseMenuVisible(false);
    }

    private Canvas CreateRootCanvas()
    {
        GameObject canvasObject = CreateUiObject("PauseMenuCanvas", transform);
        canvasObject.transform.SetParent(null, false);
        DontDestroyOnLoad(canvasObject);

        RectTransform rectTransform = canvasObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        Image background = canvasObject.AddComponent<Image>();
        background.color = OverlayColor;

        return canvas;
    }

    private GameObject CreateCardPanel(Transform parent, string name, Vector2 size, out RectTransform cardRect)
    {
        GameObject card = CreateUiObject(name, parent);
        cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = size;

        Image panelImage = card.AddComponent<Image>();
        panelImage.color = PanelColor;

        Outline panelOutline = card.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.72f, 0.92f, 1f, 0.15f);
        panelOutline.effectDistance = new Vector2(6f, -6f);

        GameObject accent = CreateUiObject("Accent", card.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 18f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = PanelAccentColor;

        return card;
    }

    private void BuildMainPanel(Transform parent)
    {
        GameObject content = CreateFillVerticalContainer(parent, "MainContent", 56, 56, 56, 56, 18f);
        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperCenter;

        mainTitleText = CreateTitleText(content.transform, "PAUSE", 58f);
        mainTitleText.gameObject.GetComponent<LayoutElement>().preferredHeight = 74f;

        CreateFlexibleSpacer(content.transform, 12f);

        GameObject buttonArea = CreateUiObject("ButtonArea", content.transform);
        LayoutElement buttonAreaLayout = buttonArea.AddComponent<LayoutElement>();
        buttonAreaLayout.preferredHeight = 348f;
        buttonAreaLayout.flexibleWidth = 1f;

        VerticalLayoutGroup buttonLayout = buttonArea.GetComponent<VerticalLayoutGroup>();
        if (buttonLayout == null) buttonLayout = buttonArea.AddComponent<VerticalLayoutGroup>();
        buttonLayout.padding = new RectOffset(0, 0, 0, 0);
        buttonLayout.spacing = 14f;
        buttonLayout.childAlignment = TextAnchor.UpperCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = false;

        resumeButton = CreateStyledButton(buttonArea.transform, "RESUME", ResumeColor, ResumeHoverColor, ResumeGame, new Vector2(0f, 72f), 30f);
        settingsButton = CreateStyledButton(buttonArea.transform, "SETTINGS", SettingsColor, SettingsHoverColor, OpenSettings, new Vector2(0f, 72f), 30f);
        menuButton = CreateStyledButton(buttonArea.transform, "BACK TO MENU", MenuColor, MenuHoverColor, LoadMainMenu, new Vector2(0f, 72f), 28f);
        quitButton = CreateStyledButton(buttonArea.transform, "QUIT", QuitColor, QuitHoverColor, QuitGame, new Vector2(0f, 72f), 30f);

        CreateFlexibleSpacer(content.transform, 12f);
    }

    private void BuildSettingsPanel(Transform parent)
    {
        GameObject content = CreateFillVerticalContainer(parent, "SettingsContent", 52, 52, 50, 42, 18f);
        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperCenter;

        settingsTitleText = CreateTitleText(content.transform, "SETTINGS", 54f);
        settingsTitleText.gameObject.GetComponent<LayoutElement>().preferredHeight = 72f;

        GameObject settingsBody = CreateUiObject("SettingsBody", content.transform);
        LayoutElement settingsBodyLayout = settingsBody.AddComponent<LayoutElement>();
        settingsBodyLayout.flexibleHeight = 1f;
        settingsBodyLayout.flexibleWidth = 1f;

        VerticalLayoutGroup settingsBodyGroup = settingsBody.AddComponent<VerticalLayoutGroup>();
        settingsBodyGroup.spacing = 18f;
        settingsBodyGroup.childAlignment = TextAnchor.UpperCenter;
        settingsBodyGroup.childControlWidth = true;
        settingsBodyGroup.childControlHeight = true;
        settingsBodyGroup.childForceExpandWidth = true;
        settingsBodyGroup.childForceExpandHeight = false;

        GameObject volumeSection = CreateInsetCard(settingsBody.transform, "VolumeSection", new Vector2(0f, 280f));
        GameObject volumeContent = CreateFillVerticalContainer(volumeSection.transform, "VolumeContent", 28, 28, 28, 28, 14f);
        CreateSectionLabel(volumeContent.transform, "VOLUME");
        volumeValueText = CreateSingleText(volumeContent.transform, "VolumeValue", "100%", 30f, Color.white, TextAlignmentOptions.Center);
        volumeValueText.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        volumeSliderRoot = CreateSliderControl(volumeContent.transform);
        volumeNumberRoot = CreateVolumeNumberInput(volumeContent.transform);
        volumeModeButton = CreateStyledButton(volumeContent.transform, "MODE: SLIDER", UtilityGrayColor, UtilityGrayHoverColor, ToggleVolumeInputMode, new Vector2(0f, 60f), 22f);
        volumeModeButtonText = volumeModeButton.label;

        GameObject fireSection = CreateInsetCard(settingsBody.transform, "FireSection", new Vector2(0f, 170f));
        GameObject fireContent = CreateFillVerticalContainer(fireSection.transform, "FireContent", 28, 28, 28, 28, 14f);
        CreateSectionLabel(fireContent.transform, "SHOOTING");
        holdFireToggleButton = CreateStyledButton(fireContent.transform, "HOLD FIRE: OFF", UtilityBlueColor, UtilityBlueHoverColor, ToggleHoldFire, new Vector2(0f, 68f), 24f);
        holdFireToggleButtonText = holdFireToggleButton.label;

        GameObject footer = CreateUiObject("SettingsFooter", content.transform);
        footer.AddComponent<LayoutElement>().preferredHeight = 72f;

        HorizontalLayoutGroup footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = false;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = false;
        footerLayout.childForceExpandHeight = false;

        settingsBackButton = CreateStyledButton(footer.transform, "BACK", UtilityBlueColor, UtilityBlueHoverColor, BackToPauseHome, new Vector2(320f, 68f), 28f);
    }

    private GameObject CreateFillVerticalContainer(Transform parent, string name, int left, int right, int top, int bottom, float spacing)
    {
        GameObject container = CreateUiObject(name, parent);
        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return container;
    }

    private GameObject CreateAutoHorizontalContainer(Transform parent, string name, float spacing)
    {
        GameObject container = CreateUiObject(name, parent);
        HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return container;
    }

    private GameObject CreateInsetCard(Transform parent, string name, Vector2 size)
    {
        GameObject card = CreateUiObject(name, parent);
        LayoutElement layout = card.AddComponent<LayoutElement>();
        if (size.x > 0f) layout.preferredWidth = size.x;
        if (size.y > 0f) layout.preferredHeight = size.y;

        Image image = card.AddComponent<Image>();
        image.color = new Color(0.18f, 0.24f, 0.39f, 0.98f);

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        outline.effectDistance = new Vector2(3f, -3f);

        return card;
    }

    private TextMeshProUGUI CreateTitleText(Transform parent, string value, float fontSize)
    {
        TextMeshProUGUI text = CreateSingleText(parent, "Title", value, fontSize, Color.white, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 78f;
        AddTextShadow(text, new Color(0f, 0f, 0f, 0.45f));
        return text;
    }

    private TextMeshProUGUI CreateSectionLabel(Transform parent, string value)
    {
        TextMeshProUGUI text = CreateSingleText(parent, value + "Label", value, 24f, PanelAccentColor, TextAlignmentOptions.Left);
        text.fontStyle = FontStyles.Bold;
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 36f;
        return text;
    }

    private TextMeshProUGUI CreateSingleText(Transform parent, string name, string value, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = pixelFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private void CreateSpacer(Transform parent, float height)
    {
        GameObject spacer = CreateUiObject("Spacer", parent);
        LayoutElement layout = spacer.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.flexibleWidth = 1f;
    }

    private void CreateFlexibleSpacer(Transform parent, float minHeight = 0f)
    {
        GameObject spacer = CreateUiObject("FlexibleSpacer", parent);
        LayoutElement layout = spacer.AddComponent<LayoutElement>();
        layout.minHeight = minHeight;
        layout.flexibleHeight = 1f;
    }

    private ButtonRefs CreateStyledButton(
        Transform parent,
        string label,
        Color normalColor,
        Color hoverColor,
        UnityEngine.Events.UnityAction callback,
        Vector2? sizeOverride = null,
        float fontSize = 30f)
    {
        Vector2 size = sizeOverride ?? ButtonSize;
        GameObject buttonObject = CreateUiObject(label + "Button", parent);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        if (size.x > 0f)
        {
            layout.preferredWidth = size.x;
        }
        else
        {
            layout.flexibleWidth = 1f;
        }

        if (size.y > 0f)
        {
            layout.preferredHeight = size.y;
        }

        Image image = buttonObject.AddComponent<Image>();
        image.color = normalColor;

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.28f);
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = hoverColor;
        colors.selectedColor = hoverColor;
        colors.pressedColor = hoverColor;
        colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.5f);
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(callback);

        PixelButtonHover hover = buttonObject.AddComponent<PixelButtonHover>();
        hover.Initialize(image, normalColor, hoverColor);
        hover.SetScaleProfile(1.18f, 1.08f);

        TextMeshProUGUI buttonText = CreateSingleText(buttonObject.transform, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center);
        RectTransform textRect = buttonText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-16f, -10f);
        buttonText.fontStyle = FontStyles.Bold;
        AddTextShadow(buttonText, new Color(0f, 0f, 0f, 0.42f));

        return new ButtonRefs
        {
            root = buttonObject,
            button = button,
            label = buttonText
        };
    }

    private GameObject CreateSliderControl(Transform parent)
    {
        GameObject sliderObject = CreateInsetCard(parent, "VolumeSliderCard", new Vector2(0f, 92f));
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(0f, 92f);

        volumeSlider = sliderObject.AddComponent<Slider>();
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.wholeNumbers = false;
        volumeSlider.direction = Slider.Direction.LeftToRight;

        GameObject fillArea = CreateUiObject("FillArea", sliderObject.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.anchoredPosition = Vector2.zero;
        fillAreaRect.offsetMin = new Vector2(28f, -9f);
        fillAreaRect.offsetMax = new Vector2(-28f, 9f);

        GameObject background = CreateUiObject("Background", fillArea.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.11f, 0.14f, 0.22f, 1f);

        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = UtilityBlueColor;

        GameObject handle = CreateUiObject("Handle", sliderObject.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(34f, 34f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.93f, 0.98f, 1f, 1f);

        volumeSlider.fillRect = fillRect;
        volumeSlider.handleRect = handleRect;
        volumeSlider.targetGraphic = handleImage;
        volumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);

        return sliderObject;
    }

    private GameObject CreateVolumeNumberInput(Transform parent)
    {
        GameObject root = CreateInsetCard(parent, "VolumeNumberCard", new Vector2(0f, 92f));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(0f, 92f);

        GameObject inputObject = CreateUiObject("VolumeInput", root.transform);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 0.5f);
        inputRect.anchorMax = new Vector2(0.5f, 0.5f);
        inputRect.pivot = new Vector2(0.5f, 0.5f);
        inputRect.sizeDelta = new Vector2(220f, 58f);

        Image inputBackground = inputObject.AddComponent<Image>();
        inputBackground.color = new Color(0.11f, 0.14f, 0.22f, 1f);

        TMP_InputField inputField = inputObject.AddComponent<TMP_InputField>();
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        inputField.characterLimit = 3;

        GameObject textArea = CreateUiObject("TextArea", inputObject.transform);
        RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(18f, 10f);
        textAreaRect.offsetMax = new Vector2(-18f, -10f);

        GameObject placeholderObject = CreateUiObject("Placeholder", textArea.transform);
        RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = Vector2.zero;
        placeholderRect.offsetMax = Vector2.zero;
        TextMeshProUGUI placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
        placeholder.text = "0-100";
        placeholder.font = pixelFontAsset;
        placeholder.fontSize = 24f;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        placeholder.alignment = TextAlignmentOptions.Center;

        GameObject textObject = CreateUiObject("Text", textArea.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.font = pixelFontAsset;
        textComponent.fontSize = 24f;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;

        inputField.textViewport = textAreaRect;
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholder;
        inputField.onEndEdit.AddListener(OnVolumeNumberSubmitted);

        volumeInputField = inputField;
        return root;
    }

    private Image CreateImage(Transform parent, string name, Sprite sprite, bool preserveAspect, Vector2 size)
    {
        GameObject imageObject = CreateUiObject(name, parent);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    private void AddTextShadow(Graphic targetGraphic, Color shadowColor)
    {
        Shadow shadow = targetGraphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = new Vector2(3f, -3f);
    }

    private void SelectObject(GameObject target)
    {
        if (target == null || EventSystem.current == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(target);
    }

    private void ClearSelectedObject()
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void ResetGeneratedUiReferences()
    {
        pauseMenuRoot = null;
        mainPanel = null;
        settingsPanel = null;
        mainCardRect = null;
        settingsCardRect = null;
        volumeArtRect = null;
        mainTitleText = null;
        settingsTitleText = null;
        volumeValueText = null;
        volumeModeButtonText = null;
        holdFireToggleButtonText = null;
        volumeSlider = null;
        volumeInputField = null;
        volumeSliderRoot = null;
        volumeNumberRoot = null;
        resumeButton = default;
        settingsButton = default;
        menuButton = default;
        quitButton = default;
        settingsBackButton = default;
        volumeModeButton = default;
        holdFireToggleButton = default;
    }

    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }
}
