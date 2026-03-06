using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Play : MonoBehaviour
{
    [SerializeField] private float hoverScale = 1.12f;
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private Camera hoverCamera;
    [SerializeField, Range(0.1f, 0.8f)] private float hoverRadiusScale = 0.4f;

    private Vector3 baseScale;
    private SpriteRenderer spriteRenderer;
    private bool isHovered;
    private bool isPressed;

    private void Awake()
    {
        baseScale = transform.localScale;
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (hoverCamera == null)
        {
            hoverCamera = Camera.main;
        }

        ApplyScale(baseScale);
    }

    private void Update()
    {
        if (spriteRenderer == null || hoverCamera == null) return;

        if (!TryGetPointerPosition(out Vector2 pointerScreenPos)) return;

        Vector3 pointerWorldPos = hoverCamera.ScreenToWorldPoint(pointerScreenPos);
        pointerWorldPos.z = spriteRenderer.bounds.center.z;
        Vector3 center = spriteRenderer.bounds.center;
        float radius = Mathf.Min(spriteRenderer.bounds.extents.x, spriteRenderer.bounds.extents.y) * hoverRadiusScale;
        bool hoveredNow = Vector2.Distance(new Vector2(pointerWorldPos.x, pointerWorldPos.y), new Vector2(center.x, center.y)) <= radius;
        bool pressedNow = hoveredNow && IsPointerPressed();

        if (hoveredNow == isHovered && pressedNow == isPressed) return;

        isHovered = hoveredNow;
        isPressed = pressedNow;
        ApplyCurrentScale();
    }

    private void ApplyCurrentScale()
    {
        float targetMultiplier = isPressed ? pressedScale : (isHovered ? hoverScale : 1f);
        ApplyScale(baseScale * Mathf.Max(0.01f, targetMultiplier));
    }

    private void ApplyScale(Vector3 scale)
    {
        transform.localScale = new Vector3(
            Mathf.Round(scale.x * 100f) / 100f,
            Mathf.Round(scale.y * 100f) / 100f,
            Mathf.Round(scale.z * 100f) / 100f
        );
    }

    private bool TryGetPointerPosition(out Vector2 pointerPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pointerPosition = Input.mousePosition;
        return true;
#else
        pointerPosition = default;
        return false;
#endif
    }

    private bool IsPointerPressed()
    {
        bool isPressedNow = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            isPressedNow = Mouse.current.leftButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        isPressedNow |= Input.GetMouseButton(0);
#endif
        return isPressedNow;
    }
}
