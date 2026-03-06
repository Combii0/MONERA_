using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private bool allowLivesAboveMax = true;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string hpPrefix = "HPS: ";
    [SerializeField] private Color hpNormalColor = Color.white;
    [SerializeField] private Color hpDamageColor = Color.red;
    [SerializeField] private Color hpHealColor = new Color(0.2f, 1f, 0.35f, 1f);
    [SerializeField] private float hpFlashDuration = 0.5f;
    [SerializeField] private float hpHealFlashDuration = 0.5f;
    [SerializeField] private float hitCooldown = 0.6f;
    [SerializeField] private int hpScaleStartLives = 3;
    [SerializeField] private float hpScalePerExtraLife = 0.08f;
    [SerializeField] private float hpScaleMaxMultiplier = 1.45f;

    [Header("Referencias")]
    [SerializeField] private PlayerMovement player;

    [Header("Final de Nivel")]
    [SerializeField, TextArea(2, 5)] private string letterEndMessage = "We are still hungry... There will be more... We need a bigger host and go to the surface.";
    [SerializeField] private Vector3 letterMessageLocalOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private Vector2 letterMessageCanvasSize = new Vector2(780f, 210f);
    [SerializeField, Range(0.002f, 0.05f)] private float letterMessageCanvasScale = 0.02f;
    [SerializeField, Range(0.25f, 2f)] private float letterMessageFontSizeRatio = 1.2f;
    [SerializeField] private float letterRevealCharsPerSecond = 38f;
    [SerializeField] private float letterMessageHoldSeconds = 10f;
    [SerializeField, Range(2, 20)] private int pixelFadeSteps = 12;
    [SerializeField] private string demoCompletedText = "That was Monera DEMO!";

    [Header("Mensaje Sobre Player")]
    [SerializeField, TextArea(1, 3)] private string storyMessageText = "The Archeotype makes a bacteria the king of the world.";
    [SerializeField] private float storyMessageDuration = 6f;
    [SerializeField] private Vector3 storyMessageLocalOffset = new Vector3(0f, 1.88f, 0f);
    [SerializeField] private Vector2 storyMessageCanvasSize = new Vector2(780f, 210f);
    [SerializeField, Range(0.002f, 0.05f)] private float storyMessageCanvasScale = 0.02f;
    [SerializeField, Range(0.25f, 2f)] private float storyMessageFontSizeRatio = 0.65f;
    [SerializeField] private int storyMessageSortingOrder = 500;
    [SerializeField] private float storyMessageRevealCharsPerSecond = 38f;
    [SerializeField] private float storyMessagePopDuration = 0.22f;

    [Header("Audio Mensaje")]
    [SerializeField] private AudioSource storyMessageAudioSource;
    [SerializeField] private AudioClip storyMessageTypeSfx;
    [SerializeField, Range(0f, 1f)] private float storyMessageTypeSfxVolume = 0.65f;
    [SerializeField] private float storyMessageTypeSfxMinInterval = 0.02f;

    [Header("Pantalla Muerte")]
    [SerializeField] private string restartSceneName = "The Cave";
    [SerializeField] private string menuSceneName = "Monera";
    [SerializeField] private string deathTitleText = "YOU DIED";
    [SerializeField] private float deathFadeDuration = 0.8f;
    [SerializeField] private Color deathBackgroundColor = Color.black;
    [SerializeField] private Color deathTitleColor = Color.white;
    [SerializeField] private Color deathButtonColor = new Color(0.14f, 0.14f, 0.14f, 0.95f);
    [SerializeField] private Color deathButtonTextColor = Color.white;
    [SerializeField] private Color deathButtonHoverColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    [SerializeField] private float musicFadeDuration = 1f;

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
    private GameObject letterMessageCanvas;
    private TextMeshProUGUI letterMessageText;
    private Canvas deathCanvas;
    private Image deathFadeImage;
    private CanvasGroup deathContentGroup;
    private Canvas demoCanvas;
    private Image demoFadeImage;
    private CanvasGroup demoContentGroup;
    private GameObject storyMessageCanvas;
    private TextMeshProUGUI storyMessageTmp;
    private Coroutine storyMessageRoutine;
    private float nextStoryTypeSfxTime;
    private string activeStoryMessageText = string.Empty;

    public static GameManager Instance { get; private set; }
    public float InstantDeathY => instantDeathY;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        NormalizeStoryMessageSettings();
        if (instantDeathY > -30f)
        {
            instantDeathY = -30f;
        }
        currentLives = Mathf.Max(1, maxLives);
        ResolveReferences();
        RefreshHpLabel();
    }

    private void Update()
    {
        if (levelEnded || deathSequenceStarted || demoSequenceStarted) return;
        if (player == null)
        {
            ResolveReferences();
            if (player == null) return;
        }

        if (player.transform.position.y < instantDeathY)
        {
            TriggerInstantDeathByFall();
        }
    }

    public bool TryDamagePlayer(Vector2 damageSourcePosition, float knockbackForce, float knockbackUpward, float flashDuration)
    {
        if (levelEnded || deathSequenceStarted) return false;

        if (player == null)
        {
            ResolveReferences();
            if (player == null) return false;
        }

        if (currentLives <= 0) return false;
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
        if (levelEnded || deathSequenceStarted) return false;
        if (amount <= 0) return false;

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
        if (storyMessageRoutine != null || storyMessageCanvas != null) return false;

        ResolveReferences();
        if (messageTarget == null && player != null)
        {
            messageTarget = player.transform;
        }

        string messageToShow = string.IsNullOrWhiteSpace(customMessage) ? storyMessageText : customMessage;
        ShowStoryMessage(messageTarget, messageToShow);
        return true;
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
                string lowerName = candidate.name.ToLowerInvariant();
                if (!lowerName.Contains("hp")) continue;

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

    private void RefreshHpLabel()
    {
        if (hpText == null) return;
        hpText.text = hpPrefix + currentLives;
        UpdateHpScale();
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

    private void ShowStoryMessage(Transform target, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        ResolveReferences();
        if (target == null && player != null)
        {
            target = player.transform;
        }
        if (target == null) return;

        if (storyMessageRoutine != null)
        {
            StopCoroutine(storyMessageRoutine);
            storyMessageRoutine = null;
        }

        ClearStoryMessage();
        activeStoryMessageText = message;
        CreateStoryMessageVisual(target, activeStoryMessageText);
        storyMessageRoutine = StartCoroutine(StoryMessageLifetimeRoutine());
    }

    private void NormalizeStoryMessageSettings()
    {
        storyMessageDuration = Mathf.Max(6f, storyMessageDuration);
        storyMessageFontSizeRatio = Mathf.Clamp(storyMessageFontSizeRatio, 0.25f, 1f);
        storyMessageRevealCharsPerSecond = Mathf.Max(1f, storyMessageRevealCharsPerSecond);
        storyMessagePopDuration = Mathf.Max(0.05f, storyMessagePopDuration);
    }

    private void CreateStoryMessageVisual(Transform target, string message)
    {
        if (target == null || string.IsNullOrWhiteSpace(message)) return;

        storyMessageCanvas = new GameObject("StoryMessageCanvas", typeof(RectTransform));
        storyMessageCanvas.transform.SetParent(target, false);
        storyMessageCanvas.transform.localPosition = storyMessageLocalOffset;

        Canvas worldCanvas = storyMessageCanvas.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = storyMessageSortingOrder;

        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(Mathf.Max(720f, storyMessageCanvasSize.x), Mathf.Max(160f, storyMessageCanvasSize.y));
        canvasRect.localScale = Vector3.one * Mathf.Max(0.02f, storyMessageCanvasScale);

        SpriteRenderer playerSprite = target.GetComponentInChildren<SpriteRenderer>();
        if (playerSprite != null)
        {
            worldCanvas.sortingLayerID = playerSprite.sortingLayerID;
            worldCanvas.sortingOrder = playerSprite.sortingOrder + Mathf.Max(1, storyMessageSortingOrder);
        }

        GameObject textObj = new GameObject("Message", typeof(RectTransform));
        textObj.transform.SetParent(storyMessageCanvas.transform, false);

        storyMessageTmp = textObj.AddComponent<TextMeshProUGUI>();
        RectTransform textRect = storyMessageTmp.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        storyMessageTmp.text = message;
        storyMessageTmp.color = Color.white;
        storyMessageTmp.alignment = TextAlignmentOptions.Center;
        storyMessageTmp.textWrappingMode = TextWrappingModes.Normal;
        storyMessageTmp.overflowMode = TextOverflowModes.Overflow;
        storyMessageTmp.maxVisibleCharacters = 0;

        float baseFontSize = hpText != null ? hpText.fontSize : 36f;
        float scaledSize = baseFontSize * Mathf.Clamp(storyMessageFontSizeRatio, 0.25f, 1f);
        storyMessageTmp.fontSize = Mathf.Clamp(scaledSize, 10f, 20f);

        if (hpText != null && hpText.font != null)
        {
            storyMessageTmp.font = hpText.font;
            if (hpText.fontSharedMaterial != null)
            {
                storyMessageTmp.fontSharedMaterial = hpText.fontSharedMaterial;
            }
        }
    }

    private IEnumerator StoryMessageLifetimeRoutine()
    {
        if (storyMessageTmp == null)
        {
            storyMessageRoutine = null;
            yield break;
        }

        float sequenceStartTime = Time.unscaledTime;

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
            float scale = Mathf.Lerp(0.82f, 1f, popT);
            textRect.localScale = initialScale * scale;
            yield return null;
        }

        if (storyMessageTmp != null)
        {
            storyMessageTmp.maxVisibleCharacters = totalChars;
            textRect.localScale = initialScale;
        }

        float elapsed = Time.unscaledTime - sequenceStartTime;
        // Keep total on-screen time close to storyMessageDuration (including reveal).
        float remaining = Mathf.Max(0f, storyMessageDuration - elapsed);
        if (remaining > 0f)
        {
            yield return new WaitForSecondsRealtime(remaining);
        }
        ClearStoryMessage();
        storyMessageRoutine = null;
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

    private void CreateLetterMessage(Transform target)
    {
        if (target == null || letterMessageText != null) return;

        letterMessageCanvas = new GameObject("LetterEndMessageCanvas", typeof(RectTransform));
        letterMessageCanvas.transform.SetParent(target, false);
        letterMessageCanvas.transform.localPosition = letterMessageLocalOffset;

        Canvas worldCanvas = letterMessageCanvas.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = 500;

        RectTransform canvasRect = worldCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(Mathf.Max(720f, letterMessageCanvasSize.x), Mathf.Max(180f, letterMessageCanvasSize.y));
        canvasRect.localScale = Vector3.one * Mathf.Max(0.02f, letterMessageCanvasScale);

        SpriteRenderer playerSprite = target.GetComponentInChildren<SpriteRenderer>();
        if (playerSprite != null)
        {
            worldCanvas.sortingLayerID = playerSprite.sortingLayerID;
            worldCanvas.sortingOrder = playerSprite.sortingOrder + 100;
        }

        GameObject textObj = new GameObject("Message", typeof(RectTransform));
        textObj.transform.SetParent(letterMessageCanvas.transform, false);

        letterMessageText = textObj.AddComponent<TextMeshProUGUI>();
        RectTransform textRect = letterMessageText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        letterMessageText.text = letterEndMessage;
        letterMessageText.color = Color.white;
        letterMessageText.alignment = TextAlignmentOptions.Center;
        letterMessageText.textWrappingMode = TextWrappingModes.Normal;
        letterMessageText.overflowMode = TextOverflowModes.Overflow;

        float baseFontSize = hpText != null ? hpText.fontSize : 36f;
        letterMessageText.fontSize = Mathf.Max(28f, baseFontSize * Mathf.Max(1.25f, letterMessageFontSizeRatio));

        if (hpText != null && hpText.font != null)
        {
            letterMessageText.font = hpText.font;
            if (hpText.fontSharedMaterial != null)
            {
                letterMessageText.fontSharedMaterial = hpText.fontSharedMaterial;
            }
        }
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

        EnemyMovement[] enemies = FindObjectsByType<EnemyMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyMovement enemy = enemies[i];
            if (enemy == null) continue;

            Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                enemyRb.linearVelocity = Vector2.zero;
            }

            enemy.enabled = false;
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
            PlayerProjectile projectile = projectiles[i];
            if (projectile == null) continue;

            Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                projectileRb.linearVelocity = Vector2.zero;
            }

            projectile.enabled = false;
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

        Debug.Log("Jugador sin vida. Mostrando pantalla de muerte.", this);
        StopGameplaySystems(false);
        StartCoroutine(DeathScreenRoutine());
    }

    private void TriggerInstantDeathByFall()
    {
        if (player != null)
        {
            Debug.Log($"Muerte por caida: y={player.transform.position.y:F2}, umbral={instantDeathY:F2}", this);
        }

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
        if (deathFadeImage == null)
        {
            Time.timeScale = 0f;
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
            deathFadeImage.color = c;
            yield return null;
        }

        deathFadeImage.color = targetColor;
        if (deathContentGroup != null)
        {
            deathContentGroup.alpha = 1f;
            deathContentGroup.interactable = true;
            deathContentGroup.blocksRaycasts = true;
        }

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
            float scale = Mathf.Lerp(0.75f, 1f, popT);
            textRect.localScale = initialScale * scale;

            yield return null;
        }

        letterMessageText.maxVisibleCharacters = totalChars;
        textRect.localScale = initialScale;
    }

    private IEnumerator DemoScreenRoutine()
    {
        CreateDemoScreenUI();
        if (demoFadeImage == null)
        {
            Time.timeScale = 0f;
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
            demoFadeImage.color = c;
            yield return null;
        }

        demoFadeImage.color = targetColor;
        if (demoContentGroup != null)
        {
            demoContentGroup.alpha = 1f;
            demoContentGroup.interactable = true;
            demoContentGroup.blocksRaycasts = true;
        }

        Time.timeScale = 0f;
    }

    private void CreateDeathScreenUI()
    {
        if (deathCanvas != null) return;

        GameObject canvasObj = new GameObject("DeathScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        deathCanvas = canvasObj.GetComponent<Canvas>();
        deathCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        deathCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
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

        deathFadeImage = fadeObj.GetComponent<Image>();
        Color fadeColor = deathBackgroundColor;
        fadeColor.a = 0f;
        deathFadeImage.color = fadeColor;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        contentObj.transform.SetParent(canvasObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        deathContentGroup = contentObj.GetComponent<CanvasGroup>();
        deathContentGroup.alpha = 0f;
        deathContentGroup.interactable = false;
        deathContentGroup.blocksRaycasts = false;

        TMP_FontAsset sharedFont = hpText != null ? hpText.font : null;
        Material sharedFontMaterial = hpText != null ? hpText.fontSharedMaterial : null;

        TextMeshProUGUI titleText = CreateDeathText("Title", contentObj.transform, new Vector2(0f, 150f), new Vector2(1200f, 220f), Mathf.Max(72f, (hpText != null ? hpText.fontSize : 48f) * 2.2f), deathTitleText);
        titleText.color = deathTitleColor;
        if (sharedFont != null) titleText.font = sharedFont;
        if (sharedFontMaterial != null) titleText.fontSharedMaterial = sharedFontMaterial;

        Button restartButton = CreateDeathButton(contentObj.transform, "Volver a empezar", new Vector2(0f, -40f), new Vector2(420f, 95f));
        restartButton.onClick.AddListener(LoadRestartScene);
        ConfigureButtonLabel(restartButton, "Volver a empezar", sharedFont, sharedFontMaterial);

        Button menuButton = CreateDeathButton(contentObj.transform, "Ir al menu", new Vector2(0f, -160f), new Vector2(420f, 95f));
        menuButton.onClick.AddListener(LoadMenuScene);
        ConfigureButtonLabel(menuButton, "Ir al menu", sharedFont, sharedFontMaterial);
    }

    private void CreateDemoScreenUI()
    {
        if (demoCanvas != null) return;

        GameObject canvasObj = new GameObject("DemoEndCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        demoCanvas = canvasObj.GetComponent<Canvas>();
        demoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        demoCanvas.sortingOrder = 1001;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
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

        demoFadeImage = fadeObj.GetComponent<Image>();
        Color fadeColor = deathBackgroundColor;
        fadeColor.a = 0f;
        demoFadeImage.color = fadeColor;

        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        contentObj.transform.SetParent(canvasObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        demoContentGroup = contentObj.GetComponent<CanvasGroup>();
        demoContentGroup.alpha = 0f;
        demoContentGroup.interactable = false;
        demoContentGroup.blocksRaycasts = false;

        TMP_FontAsset sharedFont = hpText != null ? hpText.font : null;
        Material sharedFontMaterial = hpText != null ? hpText.fontSharedMaterial : null;

        TextMeshProUGUI titleText = CreateDeathText("Title", contentObj.transform, new Vector2(0f, 120f), new Vector2(1400f, 240f), Mathf.Max(62f, (hpText != null ? hpText.fontSize : 46f) * 1.9f), demoCompletedText);
        titleText.color = deathTitleColor;
        if (sharedFont != null) titleText.font = sharedFont;
        if (sharedFontMaterial != null) titleText.fontSharedMaterial = sharedFontMaterial;

        Button menuButton = CreateDeathButton(contentObj.transform, "Volver al menu", new Vector2(0f, -90f), new Vector2(480f, 105f));
        menuButton.onClick.AddListener(LoadMenuScene);
        ConfigureButtonLabel(menuButton, "Volver al menu", sharedFont, sharedFontMaterial);
    }

    private TextMeshProUGUI CreateDeathText(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, string message)
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
        text.color = Color.white;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.fontStyle = FontStyles.Bold;
        List<UnityEngine.TextCore.OTL_FeatureTag> fontFeatures = new List<UnityEngine.TextCore.OTL_FeatureTag>(text.fontFeatures);
        fontFeatures.Remove(UnityEngine.TextCore.OTL_FeatureTag.kern);
        text.fontFeatures = fontFeatures;
        return text;
    }

    private Button CreateDeathButton(Transform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
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
        Outline outline = buttonObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonObj.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = deathButtonColor;
        colors.highlightedColor = deathButtonHoverColor;
        colors.pressedColor = new Color(Mathf.Clamp01(deathButtonColor.r - 0.05f), Mathf.Clamp01(deathButtonColor.g - 0.05f), Mathf.Clamp01(deathButtonColor.b - 0.05f), 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(deathButtonColor.r, deathButtonColor.g, deathButtonColor.b, 0.5f);
        button.colors = colors;

        PixelButtonHover hover = buttonObj.AddComponent<PixelButtonHover>();
        hover.Initialize(image, deathButtonColor, deathButtonHoverColor);
        return button;
    }

    private void ConfigureButtonLabel(Button button, string labelText, TMP_FontAsset sharedFont, Material sharedFontMaterial)
    {
        if (button == null) return;
        TextMeshProUGUI label = CreateDeathText("Label", button.transform, Vector2.zero, Vector2.zero, Mathf.Max(32f, (hpText != null ? hpText.fontSize : 28f) * 1.05f), labelText);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label.color = deathButtonTextColor;
        label.textWrappingMode = TextWrappingModes.NoWrap;

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
            AudioSource source = sources[i];
            if (source == null) continue;
            startVolumes[i] = source.volume;
        }

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = PixelStep01(timer / fadeDuration);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null) continue;
                source.volume = Mathf.Lerp(startVolumes[i], startVolumes[i] * clampedTarget, t);
            }

            yield return null;
        }

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null) continue;
            source.volume = startVolumes[i] * clampedTarget;
        }
    }

    private float PixelStep01(float raw)
    {
        float t = Mathf.Clamp01(raw);
        int steps = Mathf.Max(2, pixelFadeSteps);
        return Mathf.Floor(t * steps) / steps;
    }

    private void LoadRestartScene()
    {
        LoadSceneSafe(restartSceneName);
    }

    private void LoadMenuScene()
    {
        LoadSceneSafe(menuSceneName);
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Nombre de escena invalido para pantalla de muerte.", this);
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
