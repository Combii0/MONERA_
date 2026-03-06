using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class changeScene : MonoBehaviour
{
    [Header("Escena")]
    [SerializeField] private string targetSceneName = "The Cave";

    [Header("Transicion")]
    [SerializeField] private float screenFadeDuration = 0.8f;
    [SerializeField] private float musicFadeDuration = 0.8f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private int fadeSortingOrder = 5000;

    private bool isTransitioning;

    public void CAmbiar()
    {
        if (isTransitioning) return;
        StartCoroutine(TransitionToSceneRoutine());
    }

    private IEnumerator TransitionToSceneRoutine()
    {
        isTransitioning = true;

        Canvas transitionCanvas = CreateTransitionCanvas(out Image fadeImage);
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float[] startVolumes = CaptureVolumes(sources);

        float maxDuration = Mathf.Max(0.01f, Mathf.Max(screenFadeDuration, musicFadeDuration));
        float timer = 0f;

        while (timer < maxDuration)
        {
            timer += Time.unscaledDeltaTime;

            float screenT = Mathf.Clamp01(timer / Mathf.Max(0.01f, screenFadeDuration));
            SetFadeAlpha(fadeImage, screenT);

            float musicT = Mathf.Clamp01(timer / Mathf.Max(0.01f, musicFadeDuration));
            ApplyVolumeMultiplier(sources, startVolumes, 1f - musicT);
            yield return null;
        }

        SetFadeAlpha(fadeImage, 1f);
        ApplyVolumeMultiplier(sources, startVolumes, 0f);

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("No se puede cambiar de escena: targetSceneName esta vacio.", this);
            if (transitionCanvas != null)
            {
                Destroy(transitionCanvas.gameObject);
            }

            isTransitioning = false;
            yield break;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private Canvas CreateTransitionCanvas(out Image fadeImage)
    {
        GameObject canvasObj = new GameObject("SceneTransitionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = fadeSortingOrder;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject fadeObj = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeObj.transform.SetParent(canvasObj.transform, false);
        RectTransform fadeRect = fadeObj.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        fadeImage = fadeObj.GetComponent<Image>();
        SetFadeAlpha(fadeImage, 0f);
        return canvas;
    }

    private float[] CaptureVolumes(AudioSource[] sources)
    {
        float[] volumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            volumes[i] = source != null ? source.volume : 0f;
        }

        return volumes;
    }

    private void ApplyVolumeMultiplier(AudioSource[] sources, float[] startVolumes, float multiplier)
    {
        float clampedMultiplier = Mathf.Clamp01(multiplier);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null) continue;
            source.volume = startVolumes[i] * clampedMultiplier;
        }
    }

    private void SetFadeAlpha(Image fadeImage, float alpha01)
    {
        if (fadeImage == null) return;
        Color color = fadeColor;
        color.a = Mathf.Clamp01(alpha01);
        fadeImage.color = color;
    }
}
