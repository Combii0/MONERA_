using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ProtectorAttack : MonoBehaviour
{
    [Header("Trigger")]
    public bool IsAttacking;
    public bool IsExecutingAttack => isExecutingAttack;
    public bool IsMovementOverrideApplied => movementOverrideApplied;

    [Header("Ruta Fija de Embestida")]
    [SerializeField] private float attackRightX = 5.18f;
    [SerializeField] private float attackLeftX = -5.33f;
    [SerializeField] private float attackLaneY = -2.04f;
    [SerializeField] private Vector2 returnIdlePoint = new Vector2(0.04f, 0.15f);
    [SerializeField] private float moveToEdgeSpeed = 10f;
    [SerializeField] private float sweepSpeed = 22f;
    [SerializeField] private float sweepBrakeSpeed = 8.5f;
    [SerializeField] private float sweepBrakeDistance = 1.2f;
    [SerializeField] private float returnToCenterSpeed = 11f;
    [SerializeField, Min(0f)] private float preAttackDelaySeconds = 1.5f;
    [SerializeField, Min(0.1f)] private float preAttackBlinkSpeed = 10f;
    [SerializeField] private Color preAttackWarningColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Header("Safety")]
    [SerializeField, Min(0.5f)] private float minimumRuntimeMoveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float moveTimeoutPaddingSeconds = 0.75f;

    [Header("Sprite de Barrido")]
    public Sprite sweepAttackSprite;
    [SerializeField] private SpriteRenderer attackSpriteRenderer;

    [Header("Referencias")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private GameObject cianBacteriaCircle;

    private Rigidbody2D rb;
    private Protector protector;
    private ProtectorMoving protectorMoving;

    private bool isExecutingAttack;
    private bool movementOverrideApplied;
    private bool forceSweepSprite;
    private Sprite cachedSpriteBeforeSweep;
    private Coroutine activeAttackRoutine;
    private bool hasForcedWorldPosition;
    private Vector2 forcedWorldPosition;
    private float fixedZ;
    private SpriteRenderer[] cianCircleRenderers;
    private Color[] cianCircleStartColors;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        protector = GetComponent<Protector>();
        protectorMoving = GetComponent<ProtectorMoving>();
        fixedZ = transform.position.z;
        if (attackSpriteRenderer == null) attackSpriteRenderer = GetComponent<SpriteRenderer>();
        EnsureCianCircleReference();
        CacheCianCircleRenderers();
        SetCianCircleVisible(false);
        ResolvePlayerTarget();
    }

    private void Update()
    {
        if (!IsAttacking || isExecutingAttack) return;

        if (protectorMoving != null && !protectorMoving.IsEntrySequenceFinished)
        {
            // Garantiza que siempre aparezca primero el comportamiento IDLE post-Entry.
            IsAttacking = false;
            return;
        }

        IsAttacking = false;
        if (protector != null && protector.IsDead) return;

        activeAttackRoutine = StartCoroutine(PerformAttackRoutine());
    }

    private void LateUpdate()
    {
        if (hasForcedWorldPosition)
        {
            ApplyWorldPositionImmediate(forcedWorldPosition);
        }

        if (!forceSweepSprite) return;
        if (attackSpriteRenderer == null || sweepAttackSprite == null) return;

        // Se fuerza al final del frame para ganar a cualquier keyframe de animacion del Sprite.
        attackSpriteRenderer.sprite = sweepAttackSprite;
    }

    private IEnumerator PerformAttackRoutine()
    {
        isExecutingAttack = true;
        ApplyMovementOverride(true);

        Vector2 currentPos = GetCurrentPosition();
        ResolvePlayerTarget();
        float playerX = playerTarget != null ? playerTarget.position.x : 0f;
        bool protectorIsAtRightOfPlayer = currentPos.x >= playerX;

        float firstEdgeX = protectorIsAtRightOfPlayer ? attackRightX : attackLeftX;
        float sweepTargetX = protectorIsAtRightOfPlayer ? attackLeftX : attackRightX;
        Vector2 firstEdgePoint = new Vector2(firstEdgeX, attackLaneY);
        Vector2 sweepTargetPoint = new Vector2(sweepTargetX, attackLaneY);

        // 1) Ajuste suave al borde correspondiente y a la altura fija de ataque.
        yield return MoveTo(firstEdgePoint, moveToEdgeSpeed, 10f);
        if (ShouldAbortAttack())
        {
            FinishAttack();
            yield break;
        }

        // Espera previa al impacto: se aplica cuando ya llego a la esquina.
        float warmupDelay = Mathf.Max(0f, preAttackDelaySeconds);
        if (warmupDelay > 0f)
        {
            yield return PlayPreAttackWarningRoutine(warmupDelay);
            if (ShouldAbortAttack())
            {
                FinishAttack();
                yield break;
            }
        }

        // 2) Embestida rápida hacia el lado contrario con sprite de ataque y frenado suave.
        BeginSweepSpriteOverride();
        yield return SweepToWithSoftBrake(sweepTargetPoint);
        EndSweepSpriteOverride();
        if (ShouldAbortAttack())
        {
            FinishAttack();
            yield break;
        }

        // 3) Regresa suave al punto de reposo antes de retomar el movimiento normal.
        yield return MoveTo(returnIdlePoint, returnToCenterSpeed, moveToEdgeSpeed);
        FinishAttack();
    }

    private IEnumerator MoveTo(Vector2 targetPosition, float configuredSpeed, float fallbackSpeed)
    {
        float finalSpeed = ResolveMovementSpeed(configuredSpeed, fallbackSpeed);
        const float arriveThreshold = 0.0004f;
        Vector2 startPosition = GetCurrentPosition();
        float distance = Vector2.Distance(startPosition, targetPosition);
        if (distance <= arriveThreshold)
        {
            SetWorldPosition(targetPosition);
            yield break;
        }

        float travelDuration = distance / Mathf.Max(0.01f, finalSpeed);
        float elapsed = 0f;
        float timeout = Mathf.Max(0.2f, travelDuration + Mathf.Max(0.1f, moveTimeoutPaddingSeconds));

        while (elapsed < timeout)
        {
            if (ShouldAbortAttack()) yield break;
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, travelDuration));
            float easedT = t * t * (3f - (2f * t)); // SmoothStep
            Vector2 next = Vector2.Lerp(startPosition, targetPosition, easedT);
            SetWorldPosition(next);

            if (t >= 1f) break;
            yield return null;
        }

        SetWorldPosition(targetPosition);
    }

    private IEnumerator SweepToWithSoftBrake(Vector2 targetPosition)
    {
        float brakeDistance = Mathf.Max(0.1f, sweepBrakeDistance);
        float chargeSpeed = ResolveMovementSpeed(sweepSpeed, 20f);
        float elapsed = 0f;
        float distance = Vector2.Distance(GetCurrentPosition(), targetPosition);
        float timeout = Mathf.Max(0.2f, distance / Mathf.Max(0.01f, chargeSpeed) + Mathf.Max(0.1f, moveTimeoutPaddingSeconds) + 0.75f);

        while ((targetPosition - GetCurrentPosition()).sqrMagnitude > brakeDistance * brakeDistance)
        {
            if (ShouldAbortAttack()) yield break;
            if (elapsed >= timeout) break;

            Vector2 next = Vector2.MoveTowards(
                GetCurrentPosition(),
                targetPosition,
                chargeSpeed * Time.deltaTime
            );
            SetWorldPosition(next);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Tramo final con desaceleracion para que no termine en seco.
        yield return MoveTo(targetPosition, sweepBrakeSpeed, moveToEdgeSpeed);
    }

    private float ResolveMovementSpeed(float configuredSpeed, float fallbackSpeed)
    {
        float safeConfigured = configuredSpeed > 0.01f ? configuredSpeed : fallbackSpeed;
        return Mathf.Max(Mathf.Max(0.5f, minimumRuntimeMoveSpeed), safeConfigured);
    }

    private bool ShouldAbortAttack()
    {
        return protector != null && protector.IsDead;
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null) return;

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) playerTarget = player.transform;
    }

    private void FinishAttack()
    {
        FlushForcedPositionToTransform();
        RestoreAndHideCianCircle();
        EndSweepSpriteOverride();
        ApplyMovementOverride(false);
        hasForcedWorldPosition = false;
        isExecutingAttack = false;
        activeAttackRoutine = null;
    }

    public void ForceReturnToIdle()
    {
        if (activeAttackRoutine != null)
        {
            StopCoroutine(activeAttackRoutine);
            activeAttackRoutine = null;
        }

        FlushForcedPositionToTransform();
        RestoreAndHideCianCircle();
        EndSweepSpriteOverride();
        ApplyMovementOverride(false);
        hasForcedWorldPosition = false;
        isExecutingAttack = false;
        IsAttacking = false;
    }

    private void ApplyMovementOverride(bool enabled)
    {
        if (protectorMoving != null)
        {
            protectorMoving.SetExternalMovementOverride(enabled);
            if (!enabled)
            {
                protectorMoving.SyncInternalStateFromCurrentTransform(true);
            }
        }

        movementOverrideApplied = enabled;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    private void BeginSweepSpriteOverride()
    {
        if (attackSpriteRenderer == null || sweepAttackSprite == null) return;

        cachedSpriteBeforeSweep = attackSpriteRenderer.sprite;
        forceSweepSprite = true;
    }

    private void EndSweepSpriteOverride()
    {
        if (!forceSweepSprite) return;

        forceSweepSprite = false;
        if (attackSpriteRenderer != null && cachedSpriteBeforeSweep != null)
        {
            attackSpriteRenderer.sprite = cachedSpriteBeforeSweep;
        }
    }

    private Vector2 GetCurrentPosition()
    {
        if (rb != null) return rb.position;
        return transform.position;
    }

    private void SetWorldPosition(Vector2 worldPosition)
    {
        forcedWorldPosition = worldPosition;
        hasForcedWorldPosition = true;
        if (rb != null) rb.position = worldPosition;
    }

    private void FlushForcedPositionToTransform()
    {
        if (!hasForcedWorldPosition) return;
        ApplyWorldPositionImmediate(forcedWorldPosition);
    }

    private void ApplyWorldPositionImmediate(Vector2 worldPosition)
    {
        transform.position = new Vector3(worldPosition.x, worldPosition.y, fixedZ);
        if (rb != null) rb.position = worldPosition;
    }

    private void EnsureCianCircleReference()
    {
        if (cianBacteriaCircle != null) return;

        Transform child = transform.Find("cianBacteriaCircle");
        if (child != null) cianBacteriaCircle = child.gameObject;
    }

    private void CacheCianCircleRenderers()
    {
        if (cianBacteriaCircle == null)
        {
            cianCircleRenderers = null;
            cianCircleStartColors = null;
            return;
        }

        cianCircleRenderers = cianBacteriaCircle.GetComponentsInChildren<SpriteRenderer>(true);
        cianCircleStartColors = new Color[cianCircleRenderers.Length];
        for (int i = 0; i < cianCircleRenderers.Length; i++)
        {
            if (cianCircleRenderers[i] != null)
            {
                cianCircleStartColors[i] = cianCircleRenderers[i].color;
            }
        }
    }

    private void SetCianCircleVisible(bool visible)
    {
        if (cianBacteriaCircle != null) cianBacteriaCircle.SetActive(visible);
    }

    private void ApplyCianCircleWarningColor()
    {
        if (cianCircleRenderers == null || cianCircleStartColors == null) return;

        for (int i = 0; i < cianCircleRenderers.Length; i++)
        {
            if (cianCircleRenderers[i] == null) continue;

            float alpha = cianCircleStartColors[i].a;
            cianCircleRenderers[i].color = new Color(
                preAttackWarningColor.r,
                preAttackWarningColor.g,
                preAttackWarningColor.b,
                alpha
            );
        }
    }

    private void RestoreCianCircleColors()
    {
        if (cianCircleRenderers == null || cianCircleStartColors == null)
        {
            CacheCianCircleRenderers();
        }

        if (cianCircleRenderers == null || cianCircleStartColors == null) return;

        for (int i = 0; i < cianCircleRenderers.Length; i++)
        {
            if (cianCircleRenderers[i] != null)
            {
                cianCircleRenderers[i].color = cianCircleStartColors[i];
            }
        }
    }

    private void RestoreAndHideCianCircle()
    {
        RestoreCianCircleColors();
        SetCianCircleVisible(false);
    }

    private IEnumerator PlayPreAttackWarningRoutine(float delayDuration)
    {
        float duration = Mathf.Max(0f, delayDuration);
        if (duration <= 0f) yield break;

        EnsureCianCircleReference();
        CacheCianCircleRenderers();

        // Si no existe el circulo, mantenemos el delay sin feedback visual.
        if (cianBacteriaCircle == null || cianCircleRenderers == null || cianCircleStartColors == null)
        {
            float plainElapsed = 0f;
            while (plainElapsed < duration)
            {
                if (ShouldAbortAttack()) yield break;
                plainElapsed += Time.deltaTime;
                yield return null;
            }

            yield break;
        }

        RestoreCianCircleColors();
        SetCianCircleVisible(true);

        float blinkSpeed = Mathf.Max(0.1f, preAttackBlinkSpeed);
        float toggleInterval = 0.5f / blinkSpeed;
        float timer = 0f;
        float toggleTimer = 0f;
        bool showRed = true;

        while (timer < duration)
        {
            if (ShouldAbortAttack())
            {
                RestoreAndHideCianCircle();
                yield break;
            }

            timer += Time.deltaTime;
            toggleTimer += Time.deltaTime;
            if (toggleTimer >= toggleInterval)
            {
                toggleTimer -= toggleInterval;
                showRed = !showRed;
            }

            if (showRed)
            {
                SetCianCircleVisible(true);
                ApplyCianCircleWarningColor();
            }
            else
            {
                SetCianCircleVisible(false);
            }

            yield return null;
        }

        RestoreAndHideCianCircle();
    }

    private void OnDisable()
    {
        ForceReturnToIdle();
    }
}
