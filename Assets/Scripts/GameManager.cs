using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class GameManager : MonoBehaviour
{
    private static readonly Color HpDefaultColor = Color.white;
    private static readonly Color HpDamageFlashColor = Color.red;
    private static readonly Color HpHealFlashColor = new Color(0.2f, 1f, 0.35f, 1f);

    [Header("Vida")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private bool allowLivesAboveMax = true;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string hpPrefix = "HPS: ";

    [Header("Persistencia De Vida")]
    [SerializeField] private string caveSceneName = "The Cave";
    [SerializeField] private string protectorSceneName = "THE PROTECTOR";
    [SerializeField] private string postProtectorSceneName = "The Tunnel";
    [SerializeField, Min(1)] private int caveStartLives = 3;
    [SerializeField, Min(1)] private int protectorMinimumEntryLives = 3;
    [SerializeField, Min(1)] private int postProtectorMinimumEntryLives = 4;
    
    [Header("Colores Vida")]
    [SerializeField] private Color hpColorMedium = new Color(1f, 0.65f, 0f, 1f); // 2 (Naranja/Amarillo)
    [SerializeField] private Color hpColorLow = Color.red; // 1 (Rojo)
    
    [Header("Tiempos Vida")]
    [SerializeField] private float hpFlashDuration = 0.5f;
    [SerializeField] private float hpHealFlashDuration = 0.5f;
    [SerializeField] private float hpColorTransitionDuration = 0.18f;
    [SerializeField] private float hitCooldown = 0.6f;
    [SerializeField] private int hpScaleStartLives = 3;
    [SerializeField] private float hpScalePerExtraLife = 0.08f;
    [SerializeField] private float hpScaleMaxMultiplier = 1.45f;
    [SerializeField] private bool keepHpScaleConstant = true;

    [Header("Layout HP (HUD)")]
    [SerializeField] private bool autoFixHpLayout = true;
    [SerializeField] private Vector2 hpScreenOffset = new Vector2(24f, -24f);
    [SerializeField] private Vector2 hpScreenSize = new Vector2(240f, 72f);
    [SerializeField] private TextAlignmentOptions hpTextAlignment = TextAlignmentOptions.TopLeft;
    [SerializeField] private Vector4 hpTextMargins = Vector4.zero;

    [Header("Referencias")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private ChangeScene sceneChanger;

    [Header("Pantalla Muerte & Fades")]
    [SerializeField] private float deathFadeDuration = 0.8f;
    [SerializeField] private Color deathBackgroundColor = Color.black;
    [SerializeField, Range(2, 20)] private int pixelFadeSteps = 12;

    [Header("Niveles")]
    [SerializeField] private int firstLevelSceneIndex = 1;

    [Header("Muerte por Caida")]
    [SerializeField] private float instantDeathY = -12f;
    [SerializeField, Min(0f)] private float fallDeathExtraDepth = 10f;

    [Header("Audio Muerte por Caida")]
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioClip fallDeathSfx;
    [SerializeField, Range(0f, 1f)] private float fallDeathSfxVolume = 1f;

    [Header("Audio UI / NPC / Letters")]
    [SerializeField] private AudioSource uiSfxAudioSource;
    [SerializeField, Range(0f, 1f)] private float uiSfxGlobalVolume = 1f;

    [Header("Audio Principal")]
    [SerializeField, FormerlySerializedAs("caveMusicSource")] private AudioSource mainMusicSource;
    [SerializeField] private AudioClip mainMusicClip;

    [Header("Audio Background")]
    [SerializeField, FormerlySerializedAs("protectorMusicSource")] private AudioSource backgroundMusicSource;
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField, Range(0f, 1f)] private float mainMusicMinNearProtector = 0f;
    [SerializeField, Range(1f, 3f)] private float backgroundBoostNearProtector = 1.6f;
    [SerializeField] private bool autoDetectMusicSources = true;

    [Header("Visor de Pantalla (Hierarchical UI)")]
    [SerializeField] private Canvas deathCanvas;
    [SerializeField] private Image deathFadeImage;
    [SerializeField] private CanvasGroup deathContentGroup;
    
    [SerializeField] private Canvas transitionCanvas;
    [SerializeField] private Image transitionFadeImage;

    private int currentLives;
    private float nextAllowedHitTime;
    private Coroutine hpFlashRoutine;
    private Coroutine hpColorRoutine;
    private Vector3 hpBaseScale = Vector3.one;
    private bool hasHpBaseScale;
    private bool hpLayoutConfigured;
    private Color currentBaseHpColor;
    
    private bool deathSequenceStarted;
    private bool gameplayStopped;
    private bool transitionStarted;
    private bool musicPlaybackLocked;
    private bool musicSourcesResolved;
    private bool musicVolumesCached;
    private float mainMusicBaseVolume = 1f;
    private float backgroundMusicBaseVolume = 1f;
    private Coroutine protectorMusicTransitionRoutine;
    private bool protectorBackgroundSuppressed;
    private static bool hasPendingSceneIntroFadeOverride;
    private static float pendingSceneIntroFadeDuration = -1f;
    private static int pendingSceneIntroFadeSteps = -1;
    public static int PersistentPlayerLives { get; private set; }
    public static int CurrentSceneCheckpointLives { get; private set; }
    public static string CurrentSceneCheckpointName { get; private set; }
    private static bool persistentLivesInitialized;

    public static GameManager Instance { get; private set; }
    public float InstantDeathY => instantDeathY;
    private float FallDeathTriggerY => instantDeathY - fallDeathExtraDepth;

    public static void ConfigureNextSceneIntroFade(float durationSeconds, int stepCount)
    {
        hasPendingSceneIntroFadeOverride = true;
        pendingSceneIntroFadeDuration = Mathf.Max(0.05f, durationSeconds);
        pendingSceneIntroFadeSteps = Mathf.Max(2, stepCount);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeLivesForCurrentScene();
        ResolveReferences();
        SetProtectorAudioBlend(0f);
        RefreshHpLabel();
    }

    private void Start()
    {
        if (transitionCanvas != null && transitionFadeImage != null)
        {
            StartCoroutine(FadeInRoutine());
        }
    }

    private void Update()
    {
        if (deathSequenceStarted || transitionStarted) return;
        
        if (player == null)
        {
            ResolveReferences();
            if (player == null) return;
        }

        if (player.transform.position.y < FallDeathTriggerY)
        {
            TriggerInstantDeathByFall();
        }
    }

    // --- PLAYER STATS & COMBAT ---

    public bool TryDamagePlayer(Vector2 damageSourcePosition, float knockbackForce, float knockbackUpward, float flashDuration)
    {
        if (deathSequenceStarted) return false;

        if (player == null)
        {
            ResolveReferences();
            if (player == null) return false;
        }

        if (currentLives <= 0 || Time.time < nextAllowedHitTime) return false;

        nextAllowedHitTime = Time.time + hitCooldown;
        SetCurrentLives(Mathf.Max(0, currentLives - 1), refreshLabel: false);
        RefreshHpLabel();
        FlashHpText(HpDamageFlashColor, hpFlashDuration);

        Vector2 awayDirection = (Vector2)player.transform.position - damageSourcePosition;
        
        if (player.TryGetComponent(out PlayerHealth playerHealth))
        {
            playerHealth.ApplyDamageFeedback(awayDirection, knockbackForce, knockbackUpward, flashDuration);
        }

        if (currentLives == 0) StartDeathSequence();

        return true;
    }

    public bool TryAddLife(int amount = 1)
    {
        if (deathSequenceStarted || amount <= 0) return false;

        int previousLives = currentLives;
        int nextLives = currentLives + amount;
        if (!allowLivesAboveMax) nextLives = Mathf.Min(nextLives, maxLives);

        if (nextLives == previousLives) return false;

        SetCurrentLives(nextLives, refreshLabel: false);
        RefreshHpLabel();
        FlashHpText(HpHealFlashColor, hpHealFlashDuration);
        return true;
    }

    private void ResolveReferences()
    {
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();

        if (hpText == null) return;

        if (!hpLayoutConfigured)
        {
            ConfigureHpLayout();
            hpLayoutConfigured = true;
        }

        if (!hasHpBaseScale)
        {
            hpBaseScale = hpText.rectTransform.localScale;
            hasHpBaseScale = true;
        }
    }

    private void ConfigureHpLayout()
    {
        RectTransform hpRect = hpText.rectTransform;

        if (autoFixHpLayout)
        {
            hpRect.anchorMin = new Vector2(0f, 1f);
            hpRect.anchorMax = new Vector2(0f, 1f);
            hpRect.pivot = new Vector2(0f, 1f);
            hpRect.anchoredPosition = hpScreenOffset;

            if (hpScreenSize.x > 0f && hpScreenSize.y > 0f)
            {
                hpRect.sizeDelta = hpScreenSize;
            }
        }

        hpText.margin = hpTextMargins;
        hpText.alignment = hpTextAlignment;
        hpText.textWrappingMode = TextWrappingModes.NoWrap;
        hpText.overflowMode = TextOverflowModes.Overflow;
    }

    private void RefreshHpLabel()
    {
        if (hpText == null) return;

        if (currentLives <= 0)
        {
            hpText.enabled = false;
            return;
        }

        hpText.enabled = true;
        hpText.text = hpPrefix + currentLives;

        if (currentLives >= 3) currentBaseHpColor = HpDefaultColor;
        else if (currentLives == 2) currentBaseHpColor = hpColorMedium;
        else currentBaseHpColor = hpColorLow;

        if (hpFlashRoutine == null) TransitionToHpColor(currentBaseHpColor, hpColorTransitionDuration);

        UpdateHpScale();
    }

    private void UpdateHpScale()
    {
        if (hpText == null) return;
        if (!hasHpBaseScale)
        {
            hpBaseScale = hpText.rectTransform.localScale;
            hasHpBaseScale = true;
        }

        if (keepHpScaleConstant)
        {
            hpText.rectTransform.localScale = hpBaseScale;
            return;
        }

        int extraLives = Mathf.Max(0, currentLives - hpScaleStartLives);
        float targetMultiplier = Mathf.Clamp(1f + (extraLives * hpScalePerExtraLife), 1f, hpScaleMaxMultiplier);
        hpText.rectTransform.localScale = hpBaseScale * targetMultiplier;
    }

    private void FlashHpText(Color color, float duration)
    {
        if (hpText == null || !hpText.enabled) return;
        
        if (hpFlashRoutine != null) StopCoroutine(hpFlashRoutine);
        hpFlashRoutine = StartCoroutine(HpFlashRoutine(color, duration));
    }

    private IEnumerator HpFlashRoutine(Color flashColor, float duration)
    {
        if (hpColorRoutine != null)
        {
            StopCoroutine(hpColorRoutine);
            hpColorRoutine = null;
        }

        yield return LerpHpColor(hpText.color, flashColor, hpColorTransitionDuration);
        yield return new WaitForSeconds(duration);
        yield return LerpHpColor(hpText.color, currentBaseHpColor, hpColorTransitionDuration);
        hpFlashRoutine = null;
    }

    private void TransitionToHpColor(Color targetColor, float duration)
    {
        if (hpText == null || !hpText.enabled) return;

        if (hpColorRoutine != null) StopCoroutine(hpColorRoutine);
        hpColorRoutine = StartCoroutine(HpColorTransitionRoutine(targetColor, duration));
    }

    private IEnumerator HpColorTransitionRoutine(Color targetColor, float duration)
    {
        yield return LerpHpColor(hpText.color, targetColor, duration);
        hpColorRoutine = null;
    }

    private IEnumerator LerpHpColor(Color startColor, Color targetColor, float duration)
    {
        if (duration <= 0f)
        {
            hpText.color = targetColor;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            hpText.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        hpText.color = targetColor;
    }

    private void StopGameplaySystems(bool pauseTime)
    {
        if (gameplayStopped)
        {
            if (pauseTime) Time.timeScale = 0f;
            return;
        }

        gameplayStopped = true;

        if (hpFlashRoutine != null) StopCoroutine(hpFlashRoutine);
        if (hpColorRoutine != null) StopCoroutine(hpColorRoutine);
        if (hpText != null) hpText.color = currentBaseHpColor;

        if (player != null)
        {
            if (player.TryGetComponent(out Rigidbody2D playerRb)) playerRb.linearVelocity = Vector2.zero;
            
            player.enabled = false;
            if (player.TryGetComponent(out PlayerShooting shooting)) shooting.enabled = false;
            if (player.TryGetComponent(out PlayerAnimation anim)) anim.enabled = false;
            if (player.TryGetComponent(out PlayerHealth health)) health.enabled = false;
        }

        if (pauseTime) Time.timeScale = 0f;
    }

    // --- SCREEN SPACE UI & TRANSITIONS ---

    private void StartDeathSequence()
    {
        if (deathSequenceStarted) return;
        deathSequenceStarted = true;

        if (!musicPlaybackLocked)
        {
            LockAndStopMusicPlaybackImmediate();
        }

        StopGameplaySystems(false);
        StartCoroutine(DeathScreenRoutine());
    }

    private void TriggerInstantDeathByFall()
    {
        LockAndStopMusicPlaybackImmediate();
        PlayFallDeathSfx();
        SetCurrentLives(0, refreshLabel: false);
        RefreshHpLabel();
        StartDeathSequence();
    }

    private void PlayFallDeathSfx()
    {
        if (fallDeathSfx == null) return;

        AudioSource source = ResolveFallSfxSource();
        if (source == null) return;

        source.PlayOneShot(fallDeathSfx, Mathf.Clamp01(fallDeathSfxVolume));
    }

    public void PlayUiSfx(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        AudioSource source = ResolveUiSfxSource();
        if (source == null) return;

        float finalVolume = Mathf.Clamp01(volume) * Mathf.Clamp01(uiSfxGlobalVolume);
        source.PlayOneShot(clip, finalVolume);
    }

    public void PlayEncounterMusic(
        AudioClip encounterMainClip,
        float encounterMainVolume,
        AudioClip encounterBackgroundClip,
        float encounterBackgroundVolume)
    {
        PlayEncounterBackgroundLoop(encounterBackgroundClip, encounterBackgroundVolume);
        PlayEncounterMainTheme(encounterMainClip, encounterMainVolume);
    }

    public void PlayEncounterBackgroundLoop(AudioClip encounterBackgroundClip, float encounterBackgroundVolume)
    {
        if (deathSequenceStarted || encounterBackgroundClip == null) return;

        musicPlaybackLocked = false;
        protectorBackgroundSuppressed = false;
        ResolveMusicSourcesIfNeeded();
        StopProtectorMusicTransitionRoutine();

        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = CreateRuntimeMusicSource("RuntimeBackgroundMusicSource");
        }

        backgroundMusicClip = encounterBackgroundClip;
        backgroundMusicBaseVolume = Mathf.Clamp01(encounterBackgroundVolume);
        musicVolumesCached = true;

        PrepareMusicSource(backgroundMusicSource, loop: true, backgroundMusicBaseVolume);
        backgroundMusicSource.clip = backgroundMusicClip;
        backgroundMusicSource.Stop();
        backgroundMusicSource.time = 0f;
        backgroundMusicSource.Play();
    }

    public void StartProtectorCombatMusic(
        AudioClip encounterMainClip,
        float encounterMainVolume,
        float backgroundFadeOutDuration = 0.12f)
    {
        if (deathSequenceStarted) return;

        musicPlaybackLocked = false;
        protectorBackgroundSuppressed = true;
        ResolveMusicSourcesIfNeeded();
        StopProtectorMusicTransitionRoutine();

        PlayEncounterMainTheme(encounterMainClip, encounterMainVolume);

        if (backgroundMusicSource == null) return;

        if (backgroundFadeOutDuration <= 0.0001f)
        {
            backgroundMusicSource.volume = 0f;
            StopMusicSource(backgroundMusicSource);
            return;
        }

        protectorMusicTransitionRoutine = StartCoroutine(
            FadeMusicSourceRoutine(
                backgroundMusicSource,
                0f,
                backgroundFadeOutDuration,
                stopWhenFinished: true));
    }

    public void PlayEncounterMainTheme(AudioClip encounterMainClip, float encounterMainVolume)
    {
        if (deathSequenceStarted || encounterMainClip == null) return;

        musicPlaybackLocked = false;
        ResolveMusicSourcesIfNeeded();

        if (mainMusicSource == null)
        {
            mainMusicSource = CreateRuntimeMusicSource("RuntimeMainMusicSource");
        }

        mainMusicClip = encounterMainClip;
        mainMusicBaseVolume = Mathf.Clamp01(encounterMainVolume);
        musicVolumesCached = true;

        PrepareMusicSource(mainMusicSource, loop: false, mainMusicBaseVolume);
        mainMusicSource.Stop();
        mainMusicSource.clip = mainMusicClip;
        mainMusicSource.time = 0f;
        mainMusicSource.Play();
    }

    public void TransitionProtectorDeathMusic(float backgroundFadeInDuration = 0.35f)
    {
        if (deathSequenceStarted) return;

        musicPlaybackLocked = false;
        protectorBackgroundSuppressed = false;
        ResolveMusicSourcesIfNeeded();
        if (!musicVolumesCached) CacheMusicBaseVolumes();
        StopProtectorMusicTransitionRoutine();

        if (mainMusicSource != null)
        {
            mainMusicSource.volume = 0f;
            StopMusicSource(mainMusicSource);
        }

        if (backgroundMusicClip == null) return;

        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = CreateRuntimeMusicSource("RuntimeBackgroundMusicSource");
        }

        PrepareMusicSource(backgroundMusicSource, loop: true, 0f);
        backgroundMusicSource.clip = backgroundMusicClip;
        if (!backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Stop();
            backgroundMusicSource.time = 0f;
            backgroundMusicSource.Play();
        }

        float targetVolume = Mathf.Clamp01(backgroundMusicBaseVolume);
        if (backgroundFadeInDuration <= 0.0001f)
        {
            backgroundMusicSource.volume = targetVolume;
            return;
        }

        protectorMusicTransitionRoutine = StartCoroutine(
            FadeMusicSourceRoutine(
                backgroundMusicSource,
                targetVolume,
                backgroundFadeInDuration,
                stopWhenFinished: false));
    }

    public void StopConfiguredMusicImmediate()
    {
        StopProtectorMusicTransitionRoutine();
        LockAndStopMusicPlaybackImmediate();
    }

    public void StopMainMusicKeepBackground()
    {
        if (deathSequenceStarted) return;

        musicPlaybackLocked = false;
        protectorBackgroundSuppressed = false;
        ResolveMusicSourcesIfNeeded();
        if (!musicVolumesCached) CacheMusicBaseVolumes();
        StopProtectorMusicTransitionRoutine();

        StopMusicSource(mainMusicSource);

        if (backgroundMusicSource != null)
        {
            PrepareMusicSource(backgroundMusicSource, loop: true, backgroundMusicBaseVolume);
            backgroundMusicSource.volume = Mathf.Clamp01(backgroundMusicBaseVolume);
            EnsureMusicPlayback(backgroundMusicSource, backgroundMusicClip);
        }
    }

    public void SetProtectorAudioBlend(float blend01)
    {
        if (musicPlaybackLocked || deathSequenceStarted) return;

        float t = Mathf.Clamp01(blend01);
        ResolveMusicSourcesIfNeeded();
        if (!musicVolumesCached) CacheMusicBaseVolumes();

        if (mainMusicSource != null)
        {
            float mainFactor = Mathf.Lerp(1f, Mathf.Clamp01(mainMusicMinNearProtector), t);
            mainMusicSource.volume = Mathf.Clamp01(mainMusicBaseVolume * mainFactor);
            EnsureMusicPlayback(mainMusicSource, mainMusicClip);
        }

        if (backgroundMusicSource != null)
        {
            if (protectorBackgroundSuppressed)
            {
                backgroundMusicSource.volume = 0f;
            }
            else
            {
                float boostMultiplier = Mathf.Max(1f, backgroundBoostNearProtector);
                float targetVolume = backgroundMusicBaseVolume * Mathf.Lerp(1f, boostMultiplier, t);
                backgroundMusicSource.volume = Mathf.Clamp01(targetVolume);
                EnsureMusicPlayback(backgroundMusicSource, backgroundMusicClip);
            }
        }
    }

    public void ResetProtectorAudioBlend()
    {
        SetProtectorAudioBlend(0f);
    }

    private IEnumerator FadeInRoutine()
    {
        transitionCanvas.gameObject.SetActive(true);
        transitionCanvas.sortingOrder = 2000;
        
        transitionFadeImage.gameObject.SetActive(true);
        Color targetColor = deathBackgroundColor;

        float introDuration = Mathf.Max(0.05f, deathFadeDuration);
        int introSteps = Mathf.Max(2, pixelFadeSteps);
        if (hasPendingSceneIntroFadeOverride)
        {
            introDuration = Mathf.Max(0.05f, pendingSceneIntroFadeDuration);
            introSteps = Mathf.Max(2, pendingSceneIntroFadeSteps);
            hasPendingSceneIntroFadeOverride = false;
            pendingSceneIntroFadeDuration = -1f;
            pendingSceneIntroFadeSteps = -1;
        }
        
        float timer = 0f;
        while (timer < introDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = 1f - PixelStepWithCustomSteps(timer / introDuration, introSteps);
            transitionFadeImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, alpha);
            yield return null;
        }

        transitionFadeImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
        transitionCanvas.gameObject.SetActive(false); 
    }

    private IEnumerator PixelFadeRoutine(Image fadeImage, CanvasGroup contentGroup, Canvas targetCanvas, int sortingOrder)
    {
        if (targetCanvas == null)
        {
            Debug.LogError("GameManager: The Canvas reference is missing in the Inspector!");
            yield break;
        }

        targetCanvas.gameObject.SetActive(true); 
        targetCanvas.sortingOrder = sortingOrder; 

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true); 
            Color initialFadeColor = deathBackgroundColor;
            initialFadeColor.a = 0f;
            fadeImage.color = initialFadeColor;
        }

        if (contentGroup != null)
        {
            contentGroup.gameObject.SetActive(true); 
            contentGroup.alpha = 0f;
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;
        }

        // STEP 1: Fade only the background
        float timer = 0f;
        Color targetColor = deathBackgroundColor;
        targetColor.a = 1f;

        while (timer < deathFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float progress = PixelStep01(timer / deathFadeDuration);

            if (fadeImage != null)
            {
                fadeImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, progress);
            }
            yield return null;
        }

        if (fadeImage != null) fadeImage.color = targetColor;
        
        // STEP 2: Fade the content in only AFTER the background is completely finished
        if (contentGroup != null)
        {
            timer = 0f;
            float contentFadeDuration = 0.35f; // A fast fade for the UI elements
            
            while (timer < contentFadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                contentGroup.alpha = PixelStep01(timer / contentFadeDuration);
                yield return null;
            }

            contentGroup.alpha = 1f;
            contentGroup.interactable = true;
            contentGroup.blocksRaycasts = true;
        }
    }

    private IEnumerator DeathScreenRoutine()
    {
        LockAndStopMusicPlaybackImmediate();
        
        if (deathCanvas == null)
        {
            Debug.LogError("GameManager: You need to drag the Death Canvas into the GameManager Inspector!");
            Time.timeScale = 0f;
            yield break;
        }

        yield return PixelFadeRoutine(deathFadeImage, deathContentGroup, deathCanvas, 1000);
        Time.timeScale = 0f;
    }

    public void LoadRestartScene()
    {
        PreparePersistentLivesForRestart();
        sceneChanger?.Restart();
    }
    public void LoadMenuScene() => sceneChanger?.Menu();
    
    public void LoadFirstLevel()
    {
        ResetPersistentLivesSession();
        TransitionToScene(firstLevelSceneIndex);
    }

    public void TransitionToScene(int sceneIndex)
    {
        if (transitionStarted) return;
        transitionStarted = true;
        StopGameplaySystems(false); 
        StartCoroutine(TransitionRoutine(sceneIndex));
    }

    private IEnumerator TransitionRoutine(int sceneIndex)
    {
        StartCoroutine(FadeActiveAudioSources(0f, deathFadeDuration));

        if (transitionCanvas == null)
        {
            Debug.LogError("GameManager: You need to drag the Transition Canvas into the GameManager Inspector!");
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneIndex);
            yield break;
        }

        yield return PixelFadeRoutine(transitionFadeImage, null, transitionCanvas, 2000);
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneIndex);
    }

    private IEnumerator FadeActiveAudioSources(float targetMultiplier, float duration)
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (sources == null || sources.Length == 0) yield break;

        float[] startVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++) if (sources[i] != null) startVolumes[i] = sources[i].volume;

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = PixelStep01(timer / duration);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null) sources[i].volume = Mathf.Lerp(startVolumes[i], startVolumes[i] * targetMultiplier, t);
            }
            yield return null;
        }
    }

    private float PixelStep01(float raw) => Mathf.Floor(Mathf.Clamp01(raw) * pixelFadeSteps) / pixelFadeSteps;
    private static float PixelStepWithCustomSteps(float raw, int steps)
    {
        int safeSteps = Mathf.Max(2, steps);
        return Mathf.Floor(Mathf.Clamp01(raw) * safeSteps) / safeSteps;
    }

    private void LockAndStopMusicPlaybackImmediate()
    {
        musicPlaybackLocked = true;
        ResolveMusicSourcesIfNeeded();
        StopProtectorMusicTransitionRoutine();

        StopMusicSource(mainMusicSource);
        StopMusicSource(backgroundMusicSource);
    }

    private static void StopMusicSource(AudioSource source)
    {
        if (source == null) return;
        if (source.isPlaying) source.Stop();
    }

    private static void PrepareMusicSource(AudioSource source, bool loop, float volume)
    {
        if (source == null) return;

        source.playOnAwake = false;
        source.loop = loop;
        source.mute = false;
        source.pitch = 1f;
        source.volume = Mathf.Clamp01(volume);
    }

    private AudioSource CreateRuntimeMusicSource(string objectName)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        return source;
    }

    private AudioSource ResolveFallSfxSource()
    {
        if (sfxAudioSource != null)
        {
            sfxAudioSource.playOnAwake = false;
            sfxAudioSource.loop = false;
            return sfxAudioSource;
        }

        sfxAudioSource = GetComponent<AudioSource>();
        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }

        sfxAudioSource.playOnAwake = false;
        sfxAudioSource.loop = false;
        return sfxAudioSource;
    }

    private AudioSource ResolveUiSfxSource()
    {
        if (uiSfxAudioSource != null) return uiSfxAudioSource;
        if (sfxAudioSource != null) return sfxAudioSource;

        uiSfxAudioSource = GetComponent<AudioSource>();
        if (uiSfxAudioSource == null)
        {
            uiSfxAudioSource = gameObject.AddComponent<AudioSource>();
            uiSfxAudioSource.playOnAwake = false;
            uiSfxAudioSource.loop = false;
        }

        return uiSfxAudioSource;
    }

    private void ResolveMusicSourcesIfNeeded()
    {
        if (musicSourcesResolved) return;
        musicSourcesResolved = true;

        if (mainMusicSource != null && mainMusicClip != null) mainMusicSource.clip = mainMusicClip;
        if (backgroundMusicSource != null && backgroundMusicClip != null) backgroundMusicSource.clip = backgroundMusicClip;

        if (!autoDetectMusicSources)
        {
            CacheMusicBaseVolumes();
            return;
        }

        if (mainMusicSource == null || backgroundMusicSource == null)
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null || source.clip == null) continue;

                string clipName = source.clip.name.ToLowerInvariant();
                if (mainMusicSource == null && (clipName.Contains("main") || clipName.Contains("principal") || clipName.Contains("cave")))
                {
                    mainMusicSource = source;
                    continue;
                }

                if (backgroundMusicSource == null &&
                    source != mainMusicSource &&
                    (clipName.Contains("background") || clipName.Contains("ambience") || clipName.Contains("ambient") || clipName.Contains("protector") || clipName.Contains("drone")))
                {
                    backgroundMusicSource = source;
                }
            }
        }

        CacheMusicBaseVolumes();
    }

    private void CacheMusicBaseVolumes()
    {
        if (mainMusicSource != null) mainMusicBaseVolume = Mathf.Clamp01(mainMusicSource.volume);
        if (backgroundMusicSource != null) backgroundMusicBaseVolume = Mathf.Clamp01(backgroundMusicSource.volume);
        musicVolumesCached = true;
    }

    private void EnsureMusicPlayback(AudioSource source, AudioClip overrideClip)
    {
        if (source == null) return;

        if (overrideClip != null && source.clip != overrideClip)
        {
            source.clip = overrideClip;
        }

        source.playOnAwake = false;
        source.mute = false;
        source.pitch = 1f;

        if (!source.isPlaying && source.clip != null)
        {
            source.Play();
        }
    }

    private void StopProtectorMusicTransitionRoutine()
    {
        if (protectorMusicTransitionRoutine == null) return;

        StopCoroutine(protectorMusicTransitionRoutine);
        protectorMusicTransitionRoutine = null;
    }

    private IEnumerator FadeMusicSourceRoutine(
        AudioSource source,
        float targetVolume,
        float duration,
        bool stopWhenFinished)
    {
        if (source == null)
        {
            protectorMusicTransitionRoutine = null;
            yield break;
        }

        float safeTargetVolume = Mathf.Clamp01(targetVolume);
        float safeDuration = Mathf.Max(0.0001f, duration);
        float startVolume = Mathf.Max(0f, source.volume);
        float timer = 0f;

        while (timer < safeDuration)
        {
            if (source == null)
            {
                protectorMusicTransitionRoutine = null;
                yield break;
            }

            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            source.volume = Mathf.Lerp(startVolume, safeTargetVolume, t);
            yield return null;
        }

        if (source != null)
        {
            source.volume = safeTargetVolume;
            if (stopWhenFinished && safeTargetVolume <= 0.001f)
            {
                source.Stop();
            }
        }

        protectorMusicTransitionRoutine = null;
    }

    private IEnumerator FadeConfiguredMusicSources(float targetMultiplier, float duration, bool stopWhenFinished)
    {
        ResolveMusicSourcesIfNeeded();

        AudioSource[] musicSources = new AudioSource[2] { mainMusicSource, backgroundMusicSource };
        float[] startVolumes = new float[musicSources.Length];
        bool hasAnySource = false;

        for (int i = 0; i < musicSources.Length; i++)
        {
            AudioSource source = musicSources[i];
            if (source == null) continue;

            hasAnySource = true;
            startVolumes[i] = Mathf.Max(0f, source.volume);
        }

        if (!hasAnySource) yield break;

        float safeDuration = Mathf.Max(0.0001f, duration);
        float timer = 0f;
        while (timer < safeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = PixelStep01(timer / safeDuration);
            for (int i = 0; i < musicSources.Length; i++)
            {
                AudioSource source = musicSources[i];
                if (source == null) continue;

                float targetVolume = startVolumes[i] * Mathf.Max(0f, targetMultiplier);
                source.volume = Mathf.Lerp(startVolumes[i], targetVolume, t);
            }
            yield return null;
        }

        for (int i = 0; i < musicSources.Length; i++)
        {
            AudioSource source = musicSources[i];
            if (source == null) continue;

            float finalVolume = startVolumes[i] * Mathf.Max(0f, targetMultiplier);
            source.volume = finalVolume;
            if (stopWhenFinished && finalVolume <= 0.001f && source.isPlaying) source.Stop();
        }
    }

    private void OnDestroy()
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;
        if (Instance == this) Instance = null;
    }

    private void InitializeLivesForCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        int startLives = ResolveStartLivesForScene(sceneName);

        currentLives = Mathf.Max(0, startLives);
        PersistentPlayerLives = currentLives;
        CurrentSceneCheckpointLives = currentLives;
        CurrentSceneCheckpointName = sceneName;
        persistentLivesInitialized = true;
    }

    private void SetCurrentLives(int lives, bool refreshLabel = true)
    {
        currentLives = Mathf.Max(0, lives);
        PersistentPlayerLives = currentLives;

        if (refreshLabel)
        {
            RefreshHpLabel();
        }
    }

    private void PreparePersistentLivesForRestart()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        int restartLives = ResolveRestartLivesForScene(sceneName);

        PersistentPlayerLives = restartLives;
        CurrentSceneCheckpointLives = restartLives;
        CurrentSceneCheckpointName = sceneName;
    }

    public static void ResetPersistentLivesSession()
    {
        PersistentPlayerLives = 0;
        CurrentSceneCheckpointLives = 0;
        CurrentSceneCheckpointName = string.Empty;
        persistentLivesInitialized = false;
    }

    private int ResolveStartLivesForScene(string sceneName)
    {
        if (SceneNameEquals(sceneName, caveSceneName))
        {
            return Mathf.Max(1, caveStartLives);
        }

        int baseLives = persistentLivesInitialized
            ? Mathf.Max(1, PersistentPlayerLives)
            : Mathf.Max(1, maxLives);

        if (SceneNameEquals(sceneName, protectorSceneName))
        {
            return Mathf.Max(baseLives, Mathf.Max(1, protectorMinimumEntryLives));
        }

        if (SceneNameEquals(sceneName, postProtectorSceneName))
        {
            return Mathf.Max(baseLives, Mathf.Max(1, postProtectorMinimumEntryLives));
        }

        return baseLives;
    }

    private int ResolveRestartLivesForScene(string sceneName)
    {
        if (SceneNameEquals(sceneName, caveSceneName))
        {
            return Mathf.Max(1, caveStartLives);
        }

        if (SceneNameEquals(CurrentSceneCheckpointName, sceneName))
        {
            if (SceneNameEquals(sceneName, protectorSceneName))
            {
                return Mathf.Max(CurrentSceneCheckpointLives, Mathf.Max(1, protectorMinimumEntryLives));
            }

            if (SceneNameEquals(sceneName, postProtectorSceneName))
            {
                return Mathf.Max(CurrentSceneCheckpointLives, Mathf.Max(1, postProtectorMinimumEntryLives));
            }

            return Mathf.Max(1, CurrentSceneCheckpointLives);
        }

        return ResolveStartLivesForScene(sceneName);
    }

    private static bool SceneNameEquals(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
    }
}
