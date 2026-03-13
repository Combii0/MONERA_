using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; 
using System.Collections;
using System.Collections.Generic;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[System.Serializable]
public struct LoreSlide
{
    public Sprite slideImage;
    [TextArea(3, 5)]
    public string slideText;
}

public class LoreController : MonoBehaviour
{
    private const string DefaultContinuePromptMessage = "PRESS E TO CONTINUE";

    [Header("UI References")]
    public Image displayImage;
    public TextMeshProUGUI displayText; 
    public Image fadeImage; 
    public TextMeshProUGUI continuePromptText;

    [Header("Lore Content")]
    public LoreSlide[] slides;

    [Header("Settings")]
    public float revealCharsPerSecond = 38f;
    public float fadeDuration = 0.5f;
    public float finalFadeOutDuration = 2f;
    public float nextSceneIntroFadeDuration = 3f;
    [Range(2, 64)] public int nextSceneIntroFadeSteps = 24;
    public int nextSceneIndex;

    [Header("Continue Prompt")]
    [Min(8f)] public float continuePromptFontSize = 22f;
    public Color continuePromptColor = new Color(0.7f, 0.7f, 0.7f, 0.92f);
    public Vector2 continuePromptScreenOffset = new Vector2(-40f, 28f);

    [Header("Audio Settings")]
    [Tooltip("The sound effect to play when a letter appears.")]
    public AudioClip typingSound;
    [Range(0f, 1f)] public float typingVolume = 0.6f;
    [Tooltip("Minimum time between blips to prevent audio distortion.")]
    public float minTimeBetweenSounds = 0.02f;

    private AudioSource audioSource;
    private float nextSfxTime;
    private bool transitionTriggered;

    private readonly List<GraphicState> loreGraphicStates = new List<GraphicState>(8);
    private readonly List<AudioState> loreAudioStates = new List<AudioState>(4);

    private struct GraphicState
    {
        public Graphic graphic;
        public Color color;
    }

    private struct AudioState
    {
        public AudioSource source;
        public float volume;
    }

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        EnsureContinuePrompt();
        SetContinuePromptVisible(false);
    }

    void Start()
    {
        if (slides.Length > 0)
        {
            StartCoroutine(LoreSequence());
        }
        else
        {
            Debug.LogWarning("You haven't added any slides to the Lore Controller!");
        }
    }

    private IEnumerator LoreSequence()
    {
        SetFadeAlpha(1f);
        RestoreLoreGraphicAlpha();

        for (int i = 0; i < slides.Length; i++)
        {
            LoreSlide currentSlide = slides[i];
            bool isLastSlide = i == slides.Length - 1;

            if (currentSlide.slideImage != null)
            {
                displayImage.sprite = currentSlide.slideImage;
            }

            displayText.text = currentSlide.slideText;
            displayText.ForceMeshUpdate(); 
            
            int totalChars = displayText.textInfo.characterCount;
            displayText.maxVisibleCharacters = 0; 
            float visible = 0f;

            SetContinuePromptVisible(false);
            yield return StartCoroutine(FadeScreen(1f, 0f, fadeDuration));
            SetContinuePromptVisible(totalChars > 0);

            yield return null; 

            while (displayText.maxVisibleCharacters < totalChars)
            {
                if (WasInteractPressedThisFrame())
                {
                    displayText.maxVisibleCharacters = totalChars;
                    break; 
                }

                int previousVisible = displayText.maxVisibleCharacters;
                visible += revealCharsPerSecond * Time.unscaledDeltaTime;
                int newVisible = Mathf.Min(totalChars, Mathf.FloorToInt(visible));

                if (newVisible > previousVisible)
                {
                    displayText.maxVisibleCharacters = newVisible;
                    PlayTypingSound();
                }
                
                yield return null;
            }

            yield return null; 

            while (true)
            {
                if (WasInteractPressedThisFrame())
                {
                    break; 
                }
                yield return null;
            }

            if (isLastSlide)
            {
                yield return StartCoroutine(FinalizeLoreTransition());
                yield break;
            }

            SetContinuePromptVisible(false);
            yield return StartCoroutine(FadeScreen(0f, 1f, fadeDuration));
        }
    }

    private void PlayTypingSound()
    {
        if (typingSound == null) return;
        if (Time.unscaledTime < nextSfxTime) return;

        nextSfxTime = Time.unscaledTime + minTimeBetweenSounds;
        audioSource.PlayOneShot(typingSound, typingVolume);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage != null)
        {
            Color c = fadeImage.color;
            c.a = alpha;
            fadeImage.color = c;
        }
    }

    private IEnumerator FadeScreen(float startAlpha, float targetAlpha, float duration)
    {
        if (fadeImage == null) yield break;

        float elapsedTime = 0f;
        Color c = fadeImage.color;
        float safeDuration = Mathf.Max(0.0001f, duration);

        while (elapsedTime < safeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / safeDuration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = targetAlpha;
        fadeImage.color = c;
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

    private IEnumerator FinalizeLoreTransition()
    {
        if (transitionTriggered) yield break;
        transitionTriggered = true;

        GameManager.ConfigureNextSceneIntroFade(nextSceneIntroFadeDuration, nextSceneIntroFadeSteps);
        GameManager.ConfigureNextSceneIntroAudioFade(nextSceneIntroFadeDuration);

        CacheLoreGraphicStates();
        CacheAudioStates();

        float elapsedTime = 0f;
        float safeDuration = Mathf.Max(0.0001f, finalFadeOutDuration);
        float startFadeAlpha = fadeImage != null ? fadeImage.color.a : 0f;

        if (fadeImage != null) fadeImage.gameObject.SetActive(true);

        while (elapsedTime < safeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsedTime / safeDuration);

            ApplyLoreGraphicFade(t);
            ApplyAudioFade(t);
            SetFadeAlpha(Mathf.Lerp(startFadeAlpha, 1f, t));

            yield return null;
        }

        ApplyLoreGraphicFade(1f);
        ApplyAudioFade(1f);
        SetFadeAlpha(1f);

        LoadNextSceneDirect();
    }

    private void EnsureContinuePrompt()
    {
        if (continuePromptText == null)
        {
            RectTransform promptParent = ResolvePromptParent();
            if (promptParent == null) return;

            GameObject promptObject = new GameObject("ContinuePrompt", typeof(RectTransform));
            promptObject.transform.SetParent(promptParent, false);
            continuePromptText = promptObject.AddComponent<TextMeshProUGUI>();
        }

        if (displayText != null)
        {
            continuePromptText.font = displayText.font;
            continuePromptText.fontSharedMaterial = displayText.fontSharedMaterial;
            continuePromptText.fontStyle = displayText.fontStyle;
        }

        RectTransform promptRect = continuePromptText.rectTransform;
        promptRect.anchorMin = new Vector2(1f, 0f);
        promptRect.anchorMax = new Vector2(1f, 0f);
        promptRect.pivot = new Vector2(1f, 0f);
        promptRect.anchoredPosition = continuePromptScreenOffset;
        promptRect.sizeDelta = new Vector2(420f, Mathf.Max(36f, continuePromptFontSize * 1.75f));

        continuePromptText.text = DefaultContinuePromptMessage;
        continuePromptText.fontSize = continuePromptFontSize;
        continuePromptText.enableAutoSizing = true;
        continuePromptText.fontSizeMax = continuePromptFontSize;
        continuePromptText.fontSizeMin = Mathf.Max(8f, continuePromptFontSize * 0.7f);
        continuePromptText.alignment = TextAlignmentOptions.BottomRight;
        continuePromptText.enableWordWrapping = false;
        continuePromptText.overflowMode = TextOverflowModes.Overflow;
        continuePromptText.color = continuePromptColor;
        continuePromptText.raycastTarget = false;
    }

    private RectTransform ResolvePromptParent()
    {
        if (displayText != null)
        {
            if (displayText.canvas != null)
            {
                RectTransform canvasRect = displayText.canvas.transform as RectTransform;
                if (canvasRect != null) return canvasRect;
            }

            RectTransform textParent = displayText.transform.parent as RectTransform;
            if (textParent != null) return textParent;
        }

        if (displayImage != null)
        {
            if (displayImage.canvas != null)
            {
                RectTransform canvasRect = displayImage.canvas.transform as RectTransform;
                if (canvasRect != null) return canvasRect;
            }

            RectTransform imageParent = displayImage.transform.parent as RectTransform;
            if (imageParent != null) return imageParent;
        }

        return null;
    }

    private void SetContinuePromptVisible(bool visible)
    {
        if (continuePromptText == null) return;

        continuePromptText.rectTransform.anchoredPosition = continuePromptScreenOffset;
        continuePromptText.rectTransform.sizeDelta = new Vector2(420f, Mathf.Max(36f, continuePromptFontSize * 1.75f));
        continuePromptText.fontSize = continuePromptFontSize;
        continuePromptText.fontSizeMax = continuePromptFontSize;
        continuePromptText.fontSizeMin = Mathf.Max(8f, continuePromptFontSize * 0.7f);
        continuePromptText.color = continuePromptColor;
        continuePromptText.gameObject.SetActive(visible);
    }

    private void CacheLoreGraphicStates()
    {
        loreGraphicStates.Clear();

        if (displayText != null)
        {
            CacheGraphicsFromRoot(displayText.transform.parent);
        }

        if (displayImage != null)
        {
            CacheGraphicsFromRoot(displayImage.transform.parent);
        }

        CacheSingleGraphic(displayImage);
        CacheSingleGraphic(displayText);
        CacheSingleGraphic(continuePromptText);
    }

    private void CacheGraphicsFromRoot(Transform root)
    {
        if (root == null) return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            CacheSingleGraphic(graphics[i]);
        }
    }

    private void CacheSingleGraphic(Graphic graphic)
    {
        if (graphic == null || graphic == fadeImage) return;
        for (int i = 0; i < loreGraphicStates.Count; i++)
        {
            if (loreGraphicStates[i].graphic == graphic) return;
        }

        loreGraphicStates.Add(new GraphicState
        {
            graphic = graphic,
            color = graphic.color
        });
    }

    private void RestoreLoreGraphicAlpha()
    {
        CacheLoreGraphicStates();
        ApplyLoreGraphicFade(0f);
    }

    private void ApplyLoreGraphicFade(float progress01)
    {
        float t = Mathf.Clamp01(progress01);

        for (int i = 0; i < loreGraphicStates.Count; i++)
        {
            GraphicState state = loreGraphicStates[i];
            if (state.graphic == null) continue;

            Color color = state.color;
            color.a = Mathf.Lerp(state.color.a, 0f, t);
            state.graphic.color = color;
        }
    }

    private void CacheAudioStates()
    {
        loreAudioStates.Clear();

        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || source.volume <= 0.0001f) continue;

            loreAudioStates.Add(new AudioState
            {
                source = source,
                volume = source.volume
            });
        }
    }

    private void ApplyAudioFade(float progress01)
    {
        float t = Mathf.Clamp01(progress01);

        for (int i = 0; i < loreAudioStates.Count; i++)
        {
            AudioState state = loreAudioStates[i];
            if (state.source == null) continue;
            state.source.volume = Mathf.Lerp(state.volume, 0f, t);
        }
    }

    private void LoadNextSceneDirect()
    {
        if (nextSceneIndex >= 0 && nextSceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
            return;
        }

        Debug.LogWarning($"LoreController: scene index {nextSceneIndex} is not valid in Build Settings.");
    }
}
