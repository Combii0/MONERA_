using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class PixelButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressedScale = 0.96f;

    private RectTransform rectTransform;
    private Image targetImage;
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

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        baseScale = rectTransform.localScale;
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
        if (rectTransform != null)
        {
            rectTransform.localScale = new Vector3(
                Mathf.Round(scale.x * 100f) / 100f,
                Mathf.Round(scale.y * 100f) / 100f,
                1f
            );
        }

        if (targetImage != null)
        {
            targetImage.color = color;
        }
    }
}
