using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PixelButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressedScale = 0.96f;

    private Transform cachedTransform;
    private Image targetImage;
    private SpriteRenderer targetSpriteRenderer;
    private Color normalColor = Color.white;
    private Color hoverColor = Color.white;
    private Vector3 baseScale = Vector3.one;
    private bool isHovered;

    public void Initialize(Image image, Color normal, Color hover)
    {
        targetImage = image;
        normalColor = normal;
        hoverColor = hover;

        if (targetImage != null)
        {
            targetImage.color = normalColor;
        }
    }

    public void Initialize(SpriteRenderer spriteRenderer, Color normal, Color hover)
    {
        targetSpriteRenderer = spriteRenderer;
        normalColor = normal;
        hoverColor = hover;

        if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.color = normalColor;
        }
    }

    public void SetScaleProfile(float hover, float pressed)
    {
        hoverScale = Mathf.Max(1f, hover);
        pressedScale = Mathf.Clamp(pressed, 0.5f, hoverScale);
    }

    private void Awake()
    {
        cachedTransform = transform;
        baseScale = cachedTransform.localScale;

        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetSpriteRenderer == null)
        {
            targetSpriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        ApplyVisual(baseScale * hoverScale, hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        ApplyVisual(baseScale, normalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ApplyVisual(baseScale * pressedScale, hoverColor);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ApplyVisual(isHovered ? baseScale * hoverScale : baseScale, isHovered ? hoverColor : normalColor);
    }

    private void ApplyVisual(Vector3 scale, Color color)
    {
        if (cachedTransform != null)
        {
            cachedTransform.localScale = new Vector3(
                Mathf.Round(scale.x * 100f) / 100f,
                Mathf.Round(scale.y * 100f) / 100f,
                Mathf.Round(scale.z * 100f) / 100f
            );
        }

        if (targetImage != null)
        {
            targetImage.color = color;
        }
        else if (targetSpriteRenderer != null)
        {
            targetSpriteRenderer.color = color;
        }
    }
}
