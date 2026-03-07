using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int maxLives = 3;
    [SerializeField, HideInInspector] private bool allowLivesAboveMax = true;
    [SerializeField] private TMP_Text hpText;
    [SerializeField, HideInInspector] private string hpPrefix = "HPS: ";
    [SerializeField, HideInInspector] private Color hpNormalColor = Color.white;
    [SerializeField, HideInInspector] private Color hpDamageColor = Color.red;
    [SerializeField, HideInInspector] private Color hpHealColor = new Color(0.2f, 1f, 0.35f, 1f);
    [SerializeField, HideInInspector] private float hpFlashDuration = 0.5f;
    [SerializeField, HideInInspector] private float hpHealFlashDuration = 0.5f;
    [SerializeField, HideInInspector] private float hitCooldown = 0.6f;

    [Header("Escala HP")]
    [SerializeField, HideInInspector] private int hpScaleStartLives = 3;
    [SerializeField, HideInInspector] private float hpScalePerExtraLife = 0.08f;
    [SerializeField, HideInInspector] private float hpScaleMaxMultiplier = 1.45f;

    [Header("Referencias")]
    [SerializeField] private PlayerMovement player;

    [Header("Mensaje Cartel")]
    [SerializeField, TextArea(1, 3)] private string storyMessageText = "The Archeotype makes a bacteria the king of the world.";
    [SerializeField, HideInInspector] private float storyMessageDuration = 6f;
    [SerializeField, HideInInspector] private Vector3 storyMessageLocalOffset = new Vector3(0f, 1.88f, 0f);
    [SerializeField, HideInInspector] private Vector2 storyMessageCanvasSize = new Vector2(780f, 210f);
    [SerializeField, Range(0.002f, 0.05f), HideInInspector] private float storyMessageCanvasScale = 0.02f;
    [SerializeField, Range(0.25f, 2f), HideInInspector] private float storyMessageFontSizeRatio = 0.65f;
    [SerializeField, HideInInspector] private int storyMessageSortingOrder = 500;
    [SerializeField, HideInInspector] private float storyMessageRevealCharsPerSecond = 38f;
    [SerializeField, HideInInspector] private float storyMessagePopDuration = 0.22f;
    [SerializeField, HideInInspector] private TMP_FontAsset fallbackUiFont;

    [Header("Audio Mensaje")]
    [SerializeField, HideInInspector] private AudioSource storyMessageAudioSource;
    [SerializeField, HideInInspector] private AudioClip storyMessageTypeSfx;
    [SerializeField, Range(0f, 1f), HideInInspector] private float storyMessageTypeSfxVolume = 0.65f;
    [SerializeField, HideInInspector] private float storyMessageTypeSfxMinInterval = 0.02f;

    [Header("Final de Nivel")]
    [SerializeField, TextArea(2, 5)] private string letterEndMessage = "We are still hungry... There will be more... We need a bigger host and go to the surface.";
    [SerializeField, HideInInspector] private Vector3 letterMessageLocalOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField, HideInInspector] private Vector2 letterMessageCanvasSize = new Vector2(780f, 210f);
    [SerializeField, Range(0.002f, 0.05f), HideInInspector] private float letterMessageCanvasScale = 0.02f;
    [SerializeField, Range(0.25f, 2f), HideInInspector] private float letterMessageFontSizeRatio = 1.2f;
    [SerializeField, HideInInspector] private float letterRevealCharsPerSecond = 38f;
    [SerializeField, HideInInspector] private float letterMessageHoldSeconds = 10f;

    [Header("Pantalla Muerte")]
    [SerializeField] private string restartSceneName = "The Cave";
    [SerializeField] private string menuSceneName = "Monera";
    [SerializeField, HideInInspector] private string deathTitleText = "YOU DIED";
    [SerializeField, HideInInspector] private string demoCompletedText = "That was Monera DEMO!";
    [SerializeField, HideInInspector] private float deathFadeDuration = 0.8f;
    [SerializeField, HideInInspector] private Color deathBackgroundColor = Color.black;
    [SerializeField, HideInInspector] private Color deathTitleColor = Color.white;
    [SerializeField, HideInInspector] private Color deathButtonColor = new Color(0.14f, 0.14f, 0.14f, 0.95f);
    [SerializeField, HideInInspector] private Color deathButtonTextColor = Color.white;
    [SerializeField, HideInInspector] private Color deathButtonHoverColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField, Range(2, 20), HideInInspector] private int pixelFadeSteps = 12;
    [SerializeField, HideInInspector] private float musicFadeDuration = 1f;

    [Header("Muerte por Caida")]
    [SerializeField] private float instantDeathY = -30f;

    private int currentLives;
    private float nextAllowedHitTime;
    private Coroutine hpFlashRoutine;
    private Vector3 hpBaseScale = Vector3.one;
    private bool hasHpBaseScale;

    private bool levelEnded;
    private bool deathSequenceStarted;
    private bool gameplayStopped;
    private bool demoSequenceStarted;

    private Canvas deathCanvas;
    private Image deathFadeImage;
    private CanvasGroup deathContentGroup;

    private Canvas demoCanvas;
    private Image demoFadeImage;
    private CanvasGroup demoContentGroup;

    private GameObject letterMessageCanvas;
    private TextMeshProUGUI letterMessageText;
    private string activeLetterMessage = string.Empty;

    private GameObject storyMessageCanvas;
    private TextMeshProUGUI storyMessageTmp;
    private Coroutine storyMessageRoutine;
    private float nextStoryTypeSfxTime;
    private string activeStoryMessageText = string.Empty;

    public static GameManager Instance { get; private set; }
    public float InstantDeathY => instantDeathY;
    public bool IsStoryMessageVisible => storyMessageCanvas != null || storyMessageRoutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        instantDeathY = Mathf.Min(instantDeathY, -30f);
        storyMessageDuration = 2f;
        letterMessageHoldSeconds = 2f;

        currentLives = Mathf.Max(1, maxLives);
        ResolveReferences();
        RefreshHpLabel();
    }

    private void Update()
    {
        if (levelEnded || deathSequenceStarted || demoSequenceStarted) return;

        ResolveReferences();
        if (player == null) return;

        if (player.transform.position.y < instantDeathY)
        {
            TriggerInstantDeathByFall();
        }
    }

    public bool TryDamagePlayer(Vector2 damageSourcePosition, float knockbackForce, float knockbackUpward, float flashDuration)
    {
        if (levelEnded || deathSequenceStarted) return false;

        ResolveReferences();
        if (player == null || currentLives <= 0) return false;
        if (Time.time < nextAllowedHitTime) return false;

        nextAllowedHitTime = Time.time + Mathf.Max(0.01f, hitCooldown);
        currentLives = Mathf.Max(0, currentLives - 1);

        RefreshHpLabel();
        FlashHpText(hpDamageColor, hpFlashDuration);

        Vector2 awayDirection = (Vector2)player.transform.position - damageSourcePosition;
        player.ApplyDamageFeedback(awayDirection, knockbackForce, knockbackUpward, flashDuration);

        if (currentLives == 0)
        {
            StartDeathSequence();
        }

        return true;
    }

    public bool TryAddLife(int amount = 1)
    {
        if (levelEnded || deathSequenceStarted || amount <= 0) return false;

        int previousLives = currentLives;
        int nextLives = currentLives + amount;
        if (!allowLivesAboveMax)
        {
            nextLives = Mathf.Min(nextLives, Mathf.Max(1, maxLives));
        }

        if (nextLives == previousLives) return false;

        currentLives = nextLives;
        RefreshHpLabel();
        FlashHpText(hpHealColor, hpHealFlashDuration);
        return true;
    }

    public bool TriggerLetterEnding(Transform messageTarget, string customMessage = null)
    {
        if (deathSequenceStarted) return false;

        ResolveReferences();
        if (messageTarget == null && player != null)
        {
            messageTarget = player.transform;
        }

        if (customMessage != null)
        {
            string messageToShow = string.IsNullOrWhiteSpace(customMessage) ? storyMessageText : customMessage;
            ShowStoryMessage(messageTarget, messageToShow);
            return true;
        }

        if (levelEnded || demoSequenceStarted || messageTarget == null) return false;

        levelEnded = true;
        demoSequenceStarted = true;
        StopGameplaySystems(true);
        activeLetterMessage = letterEndMessage;
        CreateLetterMessage(messageTarget);
        StartCoroutine(LetterEndingRoutine());
        return true;
    }

    public void LoadRestartScene() => LoadSceneSafe(restartSceneName);

    public void LoadMenuScene() => LoadSceneSafe(menuSceneName);

    public void TransitionToScene(int sceneIndex)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneIndex);
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
        }

        if (hpText == null)
        {
            TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TMP_Text candidate = allTexts[i];
                if (candidate == null) continue;
                if (!candidate.name.ToLowerInvariant().Contains("hp")) continue;

                hpText = candidate;
                break;
            }

            if (hpText == null && allTexts.Length > 0)
            {
                hpText = allTexts[0];
            }
        }

        if (hpText != null)
        {
            hpNormalColor = hpText.color;
            if (!hasHpBaseScale)
            {
                hpBaseScale = hpText.rectTransform.localScale;
                hasHpBaseScale = true;
            }
        }
    }

    private TMP_FontAsset ResolveUiFont()
    {
        if (hpText != null && hpText.font != null)
        {
            return hpText.font;
        }

        if (fallbackUiFont != null)
        {
            return fallbackUiFont;
        }

        fallbackUiFont = TMP_Settings.defaultFontAsset;
        if (fallbackUiFont == null)
        {
            fallbackUiFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        return fallbackUiFont;
    }

    private Material ResolveUiFontMaterial(TMP_FontAsset resolvedFont)
    {
        if (hpText != null && hpText.fontSharedMaterial != null)
        {
            return hpText.fontSharedMaterial;
        }

        return resolvedFont != null ? resolvedFont.material : null;
    }

    private void ApplyTmpFont(TMP_Text text)
    {
        if (text == null) return;

        TMP_FontAsset resolvedFont = ResolveUiFont();
        if (resolvedFont != null)
        {
            text.font = resolvedFont;
        }

        Material resolvedMaterial = ResolveUiFontMaterial(resolvedFont);
        if (resolvedMaterial != null)
        {
            text.fontSharedMaterial = resolvedMaterial;
        }
    }

    private void RefreshHpLabel()
    {
        if (hpText == null) return;
        hpText.text = hpPrefix + currentLives;
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

        int extraLives = Mathf.Max(0, currentLives - Mathf.Max(1, hpScaleStartLives));
        float targetMultiplier = 1f + (extraLives * Mathf.Max(0f, hpScalePerExtraLife));
        targetMultiplier = Mathf.Min(targetMultiplier, Mathf.Max(1f, hpScaleMaxMultiplier));
        hpText.rectTransform.localScale = hpBaseScale * targetMultiplier;
    }

    private void FlashHpText(Color color, float duration)
    {
        if (hpText == null) return;

        if (hpFlashRoutine != null)
        {
            StopCoroutine(hpFlashRoutine);
        }

        hpFlashRoutine = StartCoroutine(HpFlashRoutine(color, duration));
    }

    private IEnumerator HpFlashRoutine(Color color, float duration)
    {
        hpText.color = color;
        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
        hpText.color = hpNormalColor;
        hpFlashRoutine = null;
    }

    private void ShowStoryMessage(Transform target, string message)
    {
        if (target == null || string.IsNullOrWhiteSpace(message)) return;

        if (storyMessageRoutine != null)
        {
            StopCoroutine(storyMessageRoutine);
            storyMessageRoutine = null;
        }

        ClearStoryMessage();

        storyMessageCanvas = CreateWorldMessageCanvas(
            "StoryMessageCanvas",
            target,
            storyMessageLocalOffset,
            storyMessageCanvasSize,
            storyMessageCanvasScale,
            storyMessageSortingOrder,
            message,
            storyMessageFontSizeRatio,
            9f,
            16f,
            out storyMessageTmp
        );

        activeStoryMessageText = message;
        storyMessageRoutine = StartCoroutine(StoryMessageLifetimeRoutine());
    }

    private IEnumerator StoryMessageLifetimeRoutine()
    {
        if (storyMessageTmp == null)
        {
            storyMessageRoutine = null;
            yield break;
        }

        RectTransform textRect = storyMessageTmp.rectTransform;
        Vector3 initialScale = textRect.localScale;
        textRect.localScale = initialScale * 0.82f;

        storyMessageTmp.ForceMeshUpdate();
        int totalChars = Mathf.Max(0, storyMessageTmp.textInfo.characterCount);
        float visible = 0f;
        float revealSpeed = Mathf.Max(1f, storyMessageRevealCharsPerSecond);
        float popTimer = 0f;
        float popDuration = Mathf.Max(0.05f, storyMessagePopDuration);

        while (storyMessageTmp != null && (storyMessageTmp.maxVisibleCharacters < totalChars || popTimer < popDuration))
        {
            float dt = Time.unscaledDeltaTime;
            visible += revealSpeed * dt;

            int nextVisible = Mathf.Min(totalChars, Mathf.FloorToInt(visible));
            while (storyMessageTmp.maxVisibleCharacters < nextVisible)
            {
                storyMessageTmp.maxVisibleCharacters++;
                PlayStoryTypeSfx(storyMessageTmp.maxVisibleCharacters - 1);
            }

            popTimer += dt;
            float popT = PixelStep01(popTimer / popDuration);
            textRect.localScale = initialScale * Mathf.Lerp(0.82f, 1f, popT);
            yield return null;
        }

        if (storyMessageTmp != null)
        {
            storyMessageTmp.maxVisibleCharacters = totalChars;
            textRect.localScale = initialScale;
        }

        float hold = Mathf.Max(0f, storyMessageDuration);
        if (hold > 0f)
        {
            yield return new WaitForSecondsRealtime(hold);
        }

        ClearStoryMessage();
        storyMessageRoutine = null;
    }

    private void PlayStoryTypeSfx(int characterIndex)
    {
        if (storyMessageTypeSfx == null) return;
        if (string.IsNullOrEmpty(activeStoryMessageText)) return;
        if (characterIndex < 0 || characterIndex >= activeStoryMessageText.Length) return;
        if (char.IsWhiteSpace(activeStoryMessageText[characterIndex])) return;
        if (Time.unscaledTime < nextStoryTypeSfxTime) return;

        nextStoryTypeSfxTime = Time.unscaledTime + Mathf.Max(0.005f, storyMessageTypeSfxMinInterval);
        float volume = Mathf.Clamp01(storyMessageTypeSfxVolume);

        if (storyMessageAudioSource != null)
        {
            storyMessageAudioSource.PlayOneShot(storyMessageTypeSfx, volume);
            return;
        }

        if (player != null)
        {
            player.PlayCustomSfx(storyMessageTypeSfx, volume);
        }
    }

    private void ClearStoryMessage()
    {
        if (storyMessageCanvas != null)
        {
            Destroy(storyMessageCanvas);
            storyMessageCanvas = null;
        }

        storyMessageTmp = null;
        activeStoryMessageText = string.Empty;
    }

    private void CreateLetterMessage(Transform target)
    {
        if (target == null || letterMessageText != null) return;

        letterMessageCanvas = CreateWorldMessageCanvas(
            "LetterEndMessageCanvas",
            target,
            letterMessageLocalOffset,
            letterMessageCanvasSize,
            letterMessageCanvasScale,
            500,
            activeLetterMessage,
            letterMessageFontSizeRatio,
            16f,
            30f,
            out letterMessageText
        );
    }

    private GameObject CreateWorldMessageCanvas(
        string canvasName,
        Transform target,
        Vector3 localOffset,
        Vector2 canvasSize,
        float canvasScale,
        int sortingBoost,
        string message,
        float fontSizeRatio,
        float minFontSize,
        float maxFontSize,
        out TextMeshProUGUI tmp)
    {
        GameObject canvasObj = new GameObject(canvasName, typeof(RectTransform));
        canvasObj.transform.SetParent(target, false);
        canvasObj.transform.localPosition = localOffset;

        Canvas worldCanvas = canvasObj.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;

        SpriteRenderer targetSprite = target.GetComponentInChildren<SpriteRenderer>();
        if (targetSprite != null)
        {
            worldCanvas.sortingLayerID = targetSprite.sortingLayerID;
            worldCanvas.sortingOrder = targetSprite.sortingOrder + Mathf.Max(1, sortingBoost);
        }
        else
        {
            worldCanvas.sortingOrder = Mathf.Max(1, sortingBoost);
        }

        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(Mathf.Max(720f, canvasSize.x), Mathf.Max(160f, canvasSize.y));
        canvasRect.localScale = Vector3.one * Mathf.Max(0.02f, canvasScale);

        GameObject textObj = new GameObject("Message", typeof(RectTransform));
        textObj.transform.SetParent(canvasObj.transform, false);

        tmp = textObj.AddComponent<TextMeshProUGUI>();
        RectTransform textRect = tmp.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        tmp.text = message;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.maxVisibleCharacters = 0;
        ApplyTmpFont(tmp);

        float baseFontSize = hpText != null ? hpText.fontSize : 36f;
        float clampedMin = Mathf.Max(6f, Mathf.Min(minFontSize, maxFontSize));
        float clampedMax = Mathf.Max(clampedMin, Mathf.Max(minFontSize, maxFontSize));
        tmp.fontSize = Mathf.Clamp(baseFontSize * Mathf.Clamp(fontSizeRatio, 0.25f, 2f), clampedMin, clampedMax);

        if (hpText != null && hpText.font != null)
        {
            tmp.font = hpText.font;
            if (hpText.fontSharedMaterial != null)
            {
                tmp.fontSharedMaterial = hpText.fontSharedMaterial;
            }
        }

        return canvasObj;
    }

    private void StopGameplaySystems(bool pauseTime)
    {
        if (gameplayStopped)
        {
            if (pauseTime) Time.timeScale = 0f;
            return;
        }

        gameplayStopped = true;

        if (hpFlashRoutine != null)
        {
            StopCoroutine(hpFlashRoutine);
            hpFlashRoutine = null;
        }

        if (hpText != null)
        {
            hpText.color = hpNormalColor;
        }

        if (player != null)
        {
            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }
            player.enabled = false;
        }

        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemyHealth = enemies[i];
            if (enemyHealth == null) continue;

            Rigidbody2D enemyRb = enemyHealth.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                enemyRb.linearVelocity = Vector2.zero;
            }

            MonoBehaviour[] enemyBehaviours = enemyHealth.GetComponents<MonoBehaviour>();
            for (int j = 0; j < enemyBehaviours.Length; j++)
            {
                MonoBehaviour behaviour = enemyBehaviours[j];
                if (behaviour == null || behaviour == this) continue;
                behaviour.enabled = false;
            }
        }

        Chest[] chests = FindObjectsByType<Chest>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < chests.Length; i++)
        {
            if (chests[i] != null) chests[i].enabled = false;
        }

        Parallax[] parallaxSystems = FindObjectsByType<Parallax>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < parallaxSystems.Length; i++)
        {
            if (parallaxSystems[i] != null) parallaxSystems[i].enabled = false;
        }

        PlayerProjectile[] projectiles = FindObjectsByType<PlayerProjectile>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i] == null) continue;

            Rigidbody2D projectileRb = projectiles[i].GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = Vector2.zero;
            }
            projectiles[i].enabled = false;
        }

        if (pauseTime)
        {
            Time.timeScale = 0f;
        }
    }

    private void StartDeathSequence()
    {
        if (deathSequenceStarted || levelEnded) return;
        deathSequenceStarted = true;

        StopGameplaySystems(false);
        StartCoroutine(DeathScreenRoutine());
    }

    private void TriggerInstantDeathByFall()
    {
        CamaraMovement cameraMovement = FindFirstObjectByType<CamaraMovement>();
        if (cameraMovement != null)
        {
            cameraMovement.SnapToPlayerImmediate();
        }

        currentLives = 0;
        RefreshHpLabel();
        StartDeathSequence();
    }

    private IEnumerator DeathScreenRoutine()
    {
        StartCoroutine(FadeActiveAudioSources(0f, musicFadeDuration));
        CreateDeathScreenUI();
        yield return FadeOverlayRoutine(deathFadeImage, deathContentGroup);
        Time.timeScale = 0f;
    }

    private IEnumerator LetterEndingRoutine()
    {
        StartCoroutine(FadeActiveAudioSources(0.35f, musicFadeDuration));
        yield return AnimateLetterMessageReveal();

        float hold = Mathf.Max(0f, letterMessageHoldSeconds);
        if (hold > 0f)
        {
            yield return new WaitForSecondsRealtime(hold);
        }

        StartCoroutine(FadeActiveAudioSources(0f, musicFadeDuration));
        yield return DemoScreenRoutine();
    }

    private IEnumerator DemoScreenRoutine()
    {
        CreateDemoScreenUI();
        yield return FadeOverlayRoutine(demoFadeImage, demoContentGroup);
        Time.timeScale = 0f;
    }

    private IEnumerator AnimateLetterMessageReveal()
    {
        if (letterMessageText == null) yield break;

        letterMessageText.ForceMeshUpdate();
        int totalChars = Mathf.Max(0, letterMessageText.textInfo.characterCount);
        letterMessageText.maxVisibleCharacters = 0;

        RectTransform textRect = letterMessageText.rectTransform;
        Vector3 initialScale = textRect.localScale;
        textRect.localScale = initialScale * 0.75f;

        float visible = 0f;
        float cps = Mathf.Max(1f, letterRevealCharsPerSecond);
        float popTimer = 0f;
        const float popDuration = 0.28f;

        while (letterMessageText.maxVisibleCharacters < totalChars || popTimer < popDuration)
        {
            float dt = Time.unscaledDeltaTime;
            visible += cps * dt;
            letterMessageText.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(visible));

            popTimer += dt;
            float popT = PixelStep01(popTimer / popDuration);
            textRect.localScale = initialScale * Mathf.Lerp(0.75f, 1f, popT);
            yield return null;
        }

        letterMessageText.maxVisibleCharacters = totalChars;
        textRect.localScale = initialScale;
    }

    private IEnumerator FadeOverlayRoutine(Image fadeImage, CanvasGroup contentGroup)
    {
        if (fadeImage == null)
        {
            yield break;
        }

        float duration = Mathf.Max(0.05f, deathFadeDuration);
        float timer = 0f;
        Color targetColor = deathBackgroundColor;
        targetColor.a = 1f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = PixelStep01(timer / duration);
            Color c = targetColor;
            c.a = t;
            fadeImage.color = c;
            yield return null;
        }

        fadeImage.color = targetColor;

        if (contentGroup != null)
        {
            contentGroup.alpha = 1f;
            contentGroup.interactable = true;
            contentGroup.blocksRaycasts = true;
        }
    }

    private void CreateDeathScreenUI()
    {
        if (deathCanvas != null) return;

        CreateOverlayCanvas(
            "DeathScreenCanvas",
            deathTitleText,
            "Volver a empezar",
            LoadRestartScene,
            "Ir al menu",
            LoadMenuScene,
            1000,
            out deathCanvas,
            out deathFadeImage,
            out deathContentGroup
        );
    }

    private void CreateDemoScreenUI()
    {
        if (demoCanvas != null) return;

        CreateOverlayCanvas(
            "DemoEndCanvas",
            demoCompletedText,
            "Volver al menu",
            LoadMenuScene,
            null,
            null,
            1001,
            out demoCanvas,
            out demoFadeImage,
            out demoContentGroup
        );
    }

    private void CreateOverlayCanvas(
        string canvasName,
        string title,
        string primaryButtonLabel,
        UnityEngine.Events.UnityAction primaryAction,
        string secondaryButtonLabel,
        UnityEngine.Events.UnityAction secondaryAction,
        int sortingOrder,
        out Canvas canvas,
        out Image fadeImage,
        out CanvasGroup contentGroup)
    {
        GameObject canvasObj = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject fadeObj = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeObj.transform.SetParent(canvasObj.transform, false);
        RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        fadeImage = fadeObj.GetComponent<Image>();
        Color fadeColor = deathBackgroundColor;
        fadeColor.a = 0f;
        fadeImage.color = fadeColor;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        contentObj.transform.SetParent(canvasObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(32f, 32f);
        contentRect.offsetMax = new Vector2(-32f, -32f);

        contentGroup = contentObj.GetComponent<CanvasGroup>();
        contentGroup.alpha = 0f;
        contentGroup.interactable = false;
        contentGroup.blocksRaycasts = false;

        TMP_FontAsset sharedFont = hpText != null ? hpText.font : null;
        Material sharedFontMaterial = hpText != null ? hpText.fontSharedMaterial : null;

        TextMeshProUGUI titleText = CreateOverlayText("Title", contentObj.transform, new Vector2(0f, 140f), new Vector2(1300f, 220f), 82f, title);
        titleText.color = deathTitleColor;
        if (sharedFont != null) titleText.font = sharedFont;
        if (sharedFontMaterial != null) titleText.fontSharedMaterial = sharedFontMaterial;

        Button primaryButton = CreateOverlayButton(contentObj.transform, primaryButtonLabel, new Vector2(0f, -40f), new Vector2(440f, 95f));
        primaryButton.onClick.AddListener(primaryAction);
        ConfigureButtonLabel(primaryButton, primaryButtonLabel, sharedFont, sharedFontMaterial);

        if (!string.IsNullOrWhiteSpace(secondaryButtonLabel) && secondaryAction != null)
        {
            Button secondaryButton = CreateOverlayButton(contentObj.transform, secondaryButtonLabel, new Vector2(0f, -160f), new Vector2(440f, 95f));
            secondaryButton.onClick.AddListener(secondaryAction);
            ConfigureButtonLabel(secondaryButton, secondaryButtonLabel, sharedFont, sharedFontMaterial);
        }
    }

    private TextMeshProUGUI CreateOverlayText(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, string message)
    {
        GameObject textObj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text.text = message;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(16f, fontSize * 0.45f);
        text.fontSizeMax = Mathf.Max(text.fontSizeMin + 2f, fontSize);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Color.white;
        ApplyTmpFont(text);
        return text;
    }

    private Button CreateOverlayButton(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(parent, false);

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObj.GetComponent<Image>();
        image.color = deathButtonColor;

        Button button = buttonObj.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = deathButtonColor;
        colors.highlightedColor = deathButtonHoverColor;
        colors.selectedColor = deathButtonHoverColor;
        colors.pressedColor = new Color(
            Mathf.Clamp01(deathButtonColor.r - 0.05f),
            Mathf.Clamp01(deathButtonColor.g - 0.05f),
            Mathf.Clamp01(deathButtonColor.b - 0.05f),
            1f
        );
        colors.disabledColor = new Color(deathButtonColor.r, deathButtonColor.g, deathButtonColor.b, 0.5f);
        button.colors = colors;

        PixelButtonHover hover = buttonObj.AddComponent<PixelButtonHover>();
        hover.Initialize(image, deathButtonColor, deathButtonHoverColor);
        return button;
    }

    private void ConfigureButtonLabel(Button button, string labelText, TMP_FontAsset sharedFont, Material sharedFontMaterial)
    {
        if (button == null) return;

        TextMeshProUGUI label = CreateOverlayText("Label", button.transform, Vector2.zero, Vector2.zero, 34f, labelText);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.color = deathButtonTextColor;

        if (sharedFont != null) label.font = sharedFont;
        if (sharedFontMaterial != null) label.fontSharedMaterial = sharedFontMaterial;
    }

    private IEnumerator FadeActiveAudioSources(float targetMultiplier, float duration)
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (sources == null || sources.Length == 0) yield break;

        float clampedTarget = Mathf.Clamp01(targetMultiplier);
        float fadeDuration = Mathf.Max(0.01f, duration);
        float timer = 0f;

        float[] startVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null)
            {
                startVolumes[i] = sources[i].volume;
            }
        }

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = PixelStep01(timer / fadeDuration);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                {
                    sources[i].volume = Mathf.Lerp(startVolumes[i], startVolumes[i] * clampedTarget, t);
                }
            }
            yield return null;
        }

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null)
            {
                sources[i].volume = startVolumes[i] * clampedTarget;
            }
        }
    }

    private float PixelStep01(float raw)
    {
        float t = Mathf.Clamp01(raw);
        int steps = Mathf.Max(2, pixelFadeSteps);
        return Mathf.Floor(t * steps) / steps;
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Nombre de escena invalido.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private void OnDestroy()
    {
        ClearStoryMessage();

        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
