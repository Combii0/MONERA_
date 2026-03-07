using UnityEngine;
using TMPro;
using System.Collections;
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
    [SerializeField] private ChangeScene sceneChanger;

    [Header("Final de Nivel")]
    [SerializeField, TextArea(2, 5)] private string letterEndMessage = "We are still hungry... There will be more... We need a bigger host and go to the surface.";
    [SerializeField] private float letterRevealCharsPerSecond = 38f;
    [SerializeField] private float letterMessageHoldSeconds = 10f;
    [SerializeField, Range(2, 20)] private int pixelFadeSteps = 12;

    [Header("Pantalla Muerte & Fades")]
    [SerializeField] private float deathFadeDuration = 0.8f;
    [SerializeField] private Color deathBackgroundColor = Color.black;
    [SerializeField] private float musicFadeDuration = 1f;

    [Header("Muerte por Caida")]
    [SerializeField] private float instantDeathY = -12f;

    [Header("Visor de Pantalla (Hierarchical UI)")]
    [SerializeField] private Canvas deathCanvas;
    [SerializeField] private Image deathFadeImage;
    [SerializeField] private CanvasGroup deathContentGroup;
    
    [SerializeField] private Canvas demoCanvas;
    [SerializeField] private Image demoFadeImage;
    [SerializeField] private CanvasGroup demoContentGroup;
    
    [SerializeField] private Canvas transitionCanvas;
    [SerializeField] private Image transitionFadeImage;
    
    [SerializeField] private Canvas letterMessageCanvas;
    [SerializeField] private TextMeshProUGUI letterMessageText;

    private int currentLives;
    private float nextAllowedHitTime;
    private Coroutine hpFlashRoutine;
    private Vector3 hpBaseScale = Vector3.one;
    private bool hasHpBaseScale;
    private bool levelEnded;
    private bool deathSequenceStarted;
    private bool gameplayStopped;
    private bool demoSequenceStarted;
    private bool transitionStarted;

    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

    public bool TriggerLetterEnding(Transform messageTarget = null)
    {
        if (levelEnded || deathSequenceStarted || demoSequenceStarted) return false;

        StopGameplaySystems(true);
        levelEnded = true;
        demoSequenceStarted = true;
        StartCoroutine(LetterEndingRoutine());
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
        if (Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }

        if (Instance == this)
        {
            Instance = null;
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
        currentLives = 0;
        RefreshHpLabel();
        StartDeathSequence();
    }

    private IEnumerator PixelFadeRoutine(Image fadeImage, CanvasGroup contentGroup, Canvas targetCanvas, int sortingOrder)
    {
        if (targetCanvas == null || fadeImage == null) yield break;

        targetCanvas.gameObject.SetActive(true); 
        targetCanvas.sortingOrder = sortingOrder; 

        fadeImage.gameObject.SetActive(true); 
        Color initialFadeColor = deathBackgroundColor;
        initialFadeColor.a = 0f;
        fadeImage.color = initialFadeColor;

        if (contentGroup != null)
        {
            contentGroup.gameObject.SetActive(true); 
            contentGroup.alpha = 0f;
            contentGroup.interactable = false;
            contentGroup.blocksRaycasts = false;
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

    private IEnumerator DeathScreenRoutine()
    {
        StartCoroutine(FadeActiveAudioSources(0f, musicFadeDuration));
        
        if (deathCanvas == null)
        {
            Debug.LogWarning("Death Screen not assigned!");
            Time.timeScale = 0f;
            yield break;
        }

        yield return PixelFadeRoutine(deathFadeImage, deathContentGroup, deathCanvas, 1000);
        
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
        if (letterMessageText == null || letterMessageCanvas == null) yield break;

        letterMessageCanvas.gameObject.SetActive(true);
        letterMessageText.gameObject.SetActive(true);

        letterMessageText.text = letterEndMessage;

        letterMessageText.ForceMeshUpdate();
        int totalChars = letterMessageText.textInfo.characterCount;
        letterMessageText.maxVisibleCharacters = 0; 

        float visible = 0f;
        float cps = Mathf.Max(1f, letterRevealCharsPerSecond);

        while (letterMessageText.maxVisibleCharacters < totalChars)
        {
            visible += cps * Time.unscaledDeltaTime;
            letterMessageText.maxVisibleCharacters = Mathf.Min(totalChars, Mathf.FloorToInt(visible));
            yield return null;
        }

        letterMessageText.maxVisibleCharacters = totalChars;
    }

    private IEnumerator DemoScreenRoutine()
    {
        if (demoCanvas == null)
        {
            Time.timeScale = 0f;
            yield break;
        }

        yield return PixelFadeRoutine(demoFadeImage, demoContentGroup, demoCanvas, 1001);

        Time.timeScale = 0f;
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
            if (sources[i] != null) startVolumes[i] = sources[i].volume;
        }

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = PixelStep01(timer / fadeDuration);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null) sources[i].volume = Mathf.Lerp(startVolumes[i], startVolumes[i] * clampedTarget, t);
            }
            yield return null;
        }

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null) sources[i].volume = startVolumes[i] * clampedTarget;
        }
    }

    private float PixelStep01(float raw)
    {
        float t = Mathf.Clamp01(raw);
        int steps = Mathf.Max(2, pixelFadeSteps);
        return Mathf.Floor(t * steps) / steps;
    }

    public void LoadRestartScene()
    {
        if (sceneChanger != null) sceneChanger.Restart();
    }

    public void LoadMenuScene()
    {
        if (sceneChanger != null) sceneChanger.Menu();
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
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneIndex);
            yield break;
        }

        yield return PixelFadeRoutine(transitionFadeImage, null, transitionCanvas, 2000);
        
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneIndex);
    }
}