using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private bool allowLivesAboveMax = true;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string hpPrefix = "HPS: ";
    
    [Header("Colores Vida")]
    [SerializeField] private Color hpColorHigh = new Color(0.2f, 1f, 0.35f, 1f); // 3 o más (Verde)
    [SerializeField] private Color hpColorMedium = new Color(1f, 0.65f, 0f, 1f); // 2 (Naranja/Amarillo)
    [SerializeField] private Color hpColorLow = Color.red; // 1 (Rojo)
    [SerializeField] private Color hpDamageColor = Color.white; // Flash de daño
    [SerializeField] private Color hpHealColor = Color.cyan; // Flash de curación
    
    [Header("Tiempos Vida")]
    [SerializeField] private float hpFlashDuration = 0.5f;
    [SerializeField] private float hpHealFlashDuration = 0.5f;
    [SerializeField] private float hitCooldown = 0.6f;
    [SerializeField] private int hpScaleStartLives = 3;
    [SerializeField] private float hpScalePerExtraLife = 0.08f;
    [SerializeField] private float hpScaleMaxMultiplier = 1.45f;

    [Header("Referencias")]
    [SerializeField] private PlayerMovement player;
    [SerializeField] private ChangeScene sceneChanger;

    [Header("Pantalla Muerte & Fades")]
    [SerializeField] private float deathFadeDuration = 0.8f;
    [SerializeField] private Color deathBackgroundColor = Color.black;
    [SerializeField] private float musicFadeDuration = 1f;
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

    [Header("Visor de Pantalla (Hierarchical UI)")]
    [SerializeField] private Canvas deathCanvas;
    [SerializeField] private Image deathFadeImage;
    [SerializeField] private CanvasGroup deathContentGroup;
    
    [SerializeField] private Canvas transitionCanvas;
    [SerializeField] private Image transitionFadeImage;

    private int currentLives;
    private float nextAllowedHitTime;
    private Coroutine hpFlashRoutine;
    private Vector3 hpBaseScale = Vector3.one;
    private bool hasHpBaseScale;
    private Color currentBaseHpColor;
    
    private bool deathSequenceStarted;
    private bool gameplayStopped;
    private bool transitionStarted;

    public static GameManager Instance { get; private set; }
    public float InstantDeathY => instantDeathY;
    private float FallDeathTriggerY => instantDeathY - fallDeathExtraDepth;

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
        currentLives = Mathf.Max(0, currentLives - 1);
        
        RefreshHpLabel();
        FlashHpText(hpDamageColor, hpFlashDuration);

        Vector2 awayDirection = (Vector2)player.transform.position - damageSourcePosition;
        player.ApplyDamageFeedback(awayDirection, knockbackForce, knockbackUpward, flashDuration);

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
        
        currentLives = nextLives;
        RefreshHpLabel();
        FlashHpText(hpHealColor, hpHealFlashDuration);
        return true;
    }

    private void ResolveReferences()
    {
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();

        if (hpText != null && !hasHpBaseScale)
        {
            hpBaseScale = hpText.rectTransform.localScale;
            hasHpBaseScale = true;
        }
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

        if (currentLives >= 3) currentBaseHpColor = hpColorHigh;
        else if (currentLives == 2) currentBaseHpColor = hpColorMedium;
        else currentBaseHpColor = hpColorLow;

        if (hpFlashRoutine == null)
        {
            hpText.color = currentBaseHpColor;
        }

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
        hpText.color = flashColor;
        yield return new WaitForSeconds(duration);
        hpText.color = currentBaseHpColor;
        hpFlashRoutine = null;
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
        if (hpText != null) hpText.color = currentBaseHpColor;

        if (player != null)
        {
            if (player.TryGetComponent(out Rigidbody2D playerRb)) playerRb.linearVelocity = Vector2.zero;
            player.enabled = false;
        }

        if (pauseTime) Time.timeScale = 0f;
    }

    // --- SCREEN SPACE UI & TRANSITIONS ---

    private void StartDeathSequence()
    {
        if (deathSequenceStarted) return;
        deathSequenceStarted = true;

        StopGameplaySystems(false);
        StartCoroutine(DeathScreenRoutine());
    }

    private void TriggerInstantDeathByFall()
    {
        PlayFallDeathSfx();
        currentLives = 0;
        RefreshHpLabel();
        StartDeathSequence();
    }

    private void PlayFallDeathSfx()
    {
        if (fallDeathSfx == null) return;

        if (sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(fallDeathSfx, Mathf.Clamp01(fallDeathSfxVolume));
        }
    }

    private IEnumerator FadeInRoutine()
    {
        transitionCanvas.gameObject.SetActive(true);
        transitionCanvas.sortingOrder = 2000;
        
        transitionFadeImage.gameObject.SetActive(true);
        Color targetColor = deathBackgroundColor;
        
        float timer = 0f;
        while (timer < deathFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = 1f - PixelStep01(timer / deathFadeDuration);
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
        StartCoroutine(FadeActiveAudioSources(0f, musicFadeDuration));
        
        if (deathCanvas == null)
        {
            Debug.LogError("GameManager: You need to drag the Death Canvas into the GameManager Inspector!");
            Time.timeScale = 0f;
            yield break;
        }

        yield return PixelFadeRoutine(deathFadeImage, deathContentGroup, deathCanvas, 1000);
        Time.timeScale = 0f;
    }

    public void LoadRestartScene() => sceneChanger?.Restart();
    public void LoadMenuScene() => sceneChanger?.Menu();
    
    public void LoadFirstLevel() => TransitionToScene(firstLevelSceneIndex);

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

    private void OnDestroy()
    {
        if (Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = 1f;
        if (Instance == this) Instance = null;
    }
}
