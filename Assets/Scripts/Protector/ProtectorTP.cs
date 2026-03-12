using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ProtectorTP : MonoBehaviour
{
    [Header("Trigger")]
    public bool TP;
    public bool IsExecutingTp => isExecutingTp;
    public bool IsMovementOverrideApplied => movementOverrideApplied;
    public float player_coords_x;
    public float player_coords_y;

    [Header("Secuencia TP")]
    [SerializeField, Min(0f)] private float teleportDelaySeconds = 0.7f;
    [SerializeField] private string teleportOutStateName = "protectorTp";
    [SerializeField] private string teleportInStateName = "protectorTpIn";
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private Vector2 returnIdlePoint = new Vector2(0.04f, 0.15f);
    [SerializeField] private float returnToCenterSpeed = 10f;
    [SerializeField, Min(0.05f)] private float fallbackOneShotDuration = 0.9f;

    [Header("Warning Circle")]
    [SerializeField] private GameObject cianBacteriaCircle;
    [SerializeField, Min(0.1f)] private float warningBlinkSpeed = 10f;
    [SerializeField] private Color warningBlinkColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Header("Referencias")]
    [SerializeField] private Transform playerTarget;

    [Header("Safety")]
    [SerializeField, Min(0.5f)] private float minimumRuntimeMoveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float moveTimeoutPaddingSeconds = 0.75f;

    private Rigidbody2D rb;
    private Animator protectorAnimator;
    private Protector protector;
    private ProtectorMoving protectorMoving;

    private bool isExecutingTp;
    private bool movementOverrideApplied;
    private Coroutine activeTpRoutine;
    private bool hasForcedWorldPosition;
    private Vector2 forcedWorldPosition;
    private float fixedZ;

    private int teleportOutStateHash;
    private int teleportOutStateFullPathHash;
    private int teleportInStateHash;
    private int teleportInStateFullPathHash;
    private int idleStateHash;
    private int idleStateFullPathHash;

    private bool hasSavedPlayerCoordsForCast;

    private SpriteRenderer[] cianCircleRenderers;
    private Color[] cianCircleStartColors;
    private Transform cianCircleOriginalParent;
    private Vector3 cianCircleOriginalLocalPosition;
    private Quaternion cianCircleOriginalLocalRotation;
    private Vector3 cianCircleOriginalLocalScale;
    private bool cianCirclePlacementCached;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        protector = GetComponent<Protector>();
        protectorMoving = GetComponent<ProtectorMoving>();
        protectorAnimator = GetComponent<Animator>();

        fixedZ = transform.position.z;

        teleportOutStateHash = Animator.StringToHash(teleportOutStateName);
        teleportOutStateFullPathHash = Animator.StringToHash($"Base Layer.{teleportOutStateName}");
        teleportInStateHash = Animator.StringToHash(teleportInStateName);
        teleportInStateFullPathHash = Animator.StringToHash($"Base Layer.{teleportInStateName}");
        idleStateHash = Animator.StringToHash(idleStateName);
        idleStateFullPathHash = Animator.StringToHash($"Base Layer.{idleStateName}");

        EnsureCianCircleReference();
        CacheCianCircleRenderers();
        SetCianCircleVisible(false);
        ResolvePlayerTarget();
    }

    private void Update()
    {
        if (!TP || isExecutingTp) return;

        if (protectorMoving != null && !protectorMoving.IsEntrySequenceFinished)
        {
            TP = false;
            return;
        }

        TP = false;
        if (ShouldAbortTp()) return;

        activeTpRoutine = StartCoroutine(PerformTeleportRoutine());
    }

    private void LateUpdate()
    {
        if (!hasForcedWorldPosition) return;
        ApplyWorldPositionImmediate(forcedWorldPosition);
    }

    private IEnumerator PerformTeleportRoutine()
    {
        isExecutingTp = true;
        ApplyMovementOverride(true);

        Vector2 castStartPosition = GetCurrentPosition();
        SetWorldPosition(castStartPosition);

        ResolvePlayerTarget();
        CachePlayerCoordinatesOnce();
        Vector2 teleportTarget = new Vector2(player_coords_x, player_coords_y);

        PlayTeleportOutState();
        PrepareAndShowWarningCircle(teleportTarget);

        float teleportDelay = Mathf.Max(0f, teleportDelaySeconds);
        float elapsed = 0f;
        while (elapsed < teleportDelay)
        {
            if (ShouldAbortTp())
            {
                FinishTp();
                yield break;
            }

            elapsed += Time.deltaTime;
            SetWorldPosition(castStartPosition);
            ApplyWarningBlink(elapsed);
            yield return null;
        }

        SetCianCircleVisible(false);

        if (ShouldAbortTp())
        {
            FinishTp();
            yield break;
        }

        SetWorldPosition(teleportTarget);

        float teleportInDuration = PlayTeleportInStateAndResolveDuration();
        yield return WaitUninterrupted(teleportInDuration);
        if (ShouldAbortTp())
        {
            FinishTp();
            yield break;
        }

        PlayIdleStateIfAvailable();

        yield return MoveTo(returnIdlePoint, returnToCenterSpeed, 9f);
        FinishTp();
    }

    private IEnumerator WaitUninterrupted(float duration)
    {
        float safeDuration = Mathf.Max(0f, duration);
        if (safeDuration <= 0.0001f) yield break;

        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            if (ShouldAbortTp()) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
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
            if (ShouldAbortTp()) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, travelDuration));
            float easedT = t * t * (3f - (2f * t));
            Vector2 next = Vector2.Lerp(startPosition, targetPosition, easedT);
            SetWorldPosition(next);

            if (t >= 1f) break;
            yield return null;
        }

        SetWorldPosition(targetPosition);
    }

    private float ResolveMovementSpeed(float configuredSpeed, float fallbackSpeed)
    {
        float safeConfigured = configuredSpeed > 0.01f ? configuredSpeed : fallbackSpeed;
        return Mathf.Max(Mathf.Max(0.5f, minimumRuntimeMoveSpeed), safeConfigured);
    }

    private void CachePlayerCoordinatesOnce()
    {
        if (hasSavedPlayerCoordsForCast) return;

        float defaultX = transform.position.x;
        float defaultY = transform.position.y;
        if (playerTarget != null)
        {
            defaultX = playerTarget.position.x;
            defaultY = playerTarget.position.y;
        }

        player_coords_x = defaultX;
        player_coords_y = defaultY;
        hasSavedPlayerCoordsForCast = true;
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null) return;

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null) playerTarget = player.transform;
    }

    private bool ShouldAbortTp()
    {
        return protector != null && protector.IsDead;
    }

    private void FinishTp()
    {
        FlushForcedPositionToTransform();
        RestoreAndHideCianCircle();
        PlayIdleStateIfAvailable();
        ApplyMovementOverride(false);
        hasForcedWorldPosition = false;
        hasSavedPlayerCoordsForCast = false;
        isExecutingTp = false;
        activeTpRoutine = null;
    }

    public void ForceReturnToIdle()
    {
        if (activeTpRoutine != null)
        {
            StopCoroutine(activeTpRoutine);
            activeTpRoutine = null;
        }

        FlushForcedPositionToTransform();
        RestoreAndHideCianCircle();
        PlayIdleStateIfAvailable();
        ApplyMovementOverride(false);
        hasForcedWorldPosition = false;
        hasSavedPlayerCoordsForCast = false;
        isExecutingTp = false;
        TP = false;
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

    private void SetWorldPosition(Vector2 worldPosition)
    {
        forcedWorldPosition = worldPosition;
        hasForcedWorldPosition = true;
        if (rb != null) rb.position = worldPosition;
    }

    private Vector2 GetCurrentPosition()
    {
        if (rb != null) return rb.position;
        return transform.position;
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

    private void PlayTeleportOutState()
    {
        if (protectorAnimator == null || string.IsNullOrWhiteSpace(teleportOutStateName)) return;

        bool hasState =
            protectorAnimator.HasState(0, teleportOutStateFullPathHash) ||
            protectorAnimator.HasState(0, teleportOutStateHash);
        if (!hasState) return;

        int stateToPlay = protectorAnimator.HasState(0, teleportOutStateFullPathHash)
            ? teleportOutStateFullPathHash
            : teleportOutStateHash;

        protectorAnimator.enabled = true;
        protectorAnimator.Play(stateToPlay, 0, 0f);
        protectorAnimator.Update(0f);
    }

    private float PlayTeleportInStateAndResolveDuration()
    {
        if (protectorAnimator == null || string.IsNullOrWhiteSpace(teleportInStateName))
        {
            return Mathf.Max(0.05f, fallbackOneShotDuration);
        }

        bool hasState =
            protectorAnimator.HasState(0, teleportInStateFullPathHash) ||
            protectorAnimator.HasState(0, teleportInStateHash);
        if (!hasState)
        {
            Debug.LogWarning($"ProtectorTP: El estado '{teleportInStateName}' no existe en el Animator.", this);
            return Mathf.Max(0.05f, fallbackOneShotDuration);
        }

        int stateToPlay = protectorAnimator.HasState(0, teleportInStateFullPathHash)
            ? teleportInStateFullPathHash
            : teleportInStateHash;

        protectorAnimator.enabled = true;
        protectorAnimator.Play(stateToPlay, 0, 0f);
        protectorAnimator.Update(0f);

        AnimatorStateInfo stateInfo = protectorAnimator.GetCurrentAnimatorStateInfo(0);
        bool isCurrentState =
            stateInfo.shortNameHash == teleportInStateHash ||
            stateInfo.fullPathHash == teleportInStateFullPathHash;
        if (isCurrentState && stateInfo.length > 0f)
        {
            return Mathf.Max(0.05f, stateInfo.length);
        }

        return Mathf.Max(0.05f, fallbackOneShotDuration);
    }

    private void PlayIdleStateIfAvailable()
    {
        if (protectorAnimator == null || string.IsNullOrWhiteSpace(idleStateName)) return;

        bool hasIdleState =
            protectorAnimator.HasState(0, idleStateFullPathHash) ||
            protectorAnimator.HasState(0, idleStateHash);
        if (!hasIdleState) return;

        int stateToPlay = protectorAnimator.HasState(0, idleStateFullPathHash)
            ? idleStateFullPathHash
            : idleStateHash;

        protectorAnimator.enabled = true;
        protectorAnimator.Play(stateToPlay, 0, 0f);
        protectorAnimator.Update(0f);
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
        if (cianCircleRenderers == null || cianCircleRenderers.Length == 0)
        {
            cianCircleRenderers = cianBacteriaCircle.GetComponents<SpriteRenderer>();
        }

        cianCircleStartColors = new Color[cianCircleRenderers.Length];
        for (int i = 0; i < cianCircleRenderers.Length; i++)
        {
            cianCircleStartColors[i] = cianCircleRenderers[i] != null ? cianCircleRenderers[i].color : Color.white;
        }
    }

    private void PrepareAndShowWarningCircle(Vector2 worldPosition)
    {
        if (cianBacteriaCircle == null) return;

        CacheCianCirclePlacementIfNeeded();
        Transform circleTransform = cianBacteriaCircle.transform;
        circleTransform.SetParent(null, true);
        circleTransform.position = new Vector3(worldPosition.x, worldPosition.y, circleTransform.position.z);

        SetWarningCircleColor(1f);
        SetCianCircleVisible(true);
    }

    private void ApplyWarningBlink(float elapsed)
    {
        if (cianBacteriaCircle == null || !cianBacteriaCircle.activeSelf) return;

        float blink = 0.5f + (0.5f * Mathf.Sin(elapsed * Mathf.Max(0.1f, warningBlinkSpeed) * Mathf.PI * 2f));
        float intensity = Mathf.Lerp(0.28f, 1f, blink);
        SetWarningCircleColor(intensity);
    }

    private void SetWarningCircleColor(float intensity)
    {
        if (cianCircleRenderers == null || cianCircleStartColors == null) return;

        float clampedIntensity = Mathf.Clamp01(intensity);
        for (int i = 0; i < cianCircleRenderers.Length; i++)
        {
            SpriteRenderer renderer = cianCircleRenderers[i];
            if (renderer == null) continue;

            Color baseColor = cianCircleStartColors.Length > i ? cianCircleStartColors[i] : Color.white;
            Color warning = warningBlinkColor;
            warning.a = baseColor.a * clampedIntensity;
            renderer.color = warning;
        }
    }

    private void CacheCianCirclePlacementIfNeeded()
    {
        if (cianBacteriaCircle == null || cianCirclePlacementCached) return;

        Transform circleTransform = cianBacteriaCircle.transform;
        cianCircleOriginalParent = circleTransform.parent;
        cianCircleOriginalLocalPosition = circleTransform.localPosition;
        cianCircleOriginalLocalRotation = circleTransform.localRotation;
        cianCircleOriginalLocalScale = circleTransform.localScale;
        cianCirclePlacementCached = true;
    }

    private void RestoreAndHideCianCircle()
    {
        if (cianBacteriaCircle == null) return;

        if (cianCirclePlacementCached)
        {
            Transform circleTransform = cianBacteriaCircle.transform;
            circleTransform.SetParent(cianCircleOriginalParent, false);
            circleTransform.localPosition = cianCircleOriginalLocalPosition;
            circleTransform.localRotation = cianCircleOriginalLocalRotation;
            circleTransform.localScale = cianCircleOriginalLocalScale;
        }

        if (cianCircleRenderers != null && cianCircleStartColors != null)
        {
            for (int i = 0; i < cianCircleRenderers.Length; i++)
            {
                if (cianCircleRenderers[i] == null) continue;
                cianCircleRenderers[i].color = cianCircleStartColors.Length > i ? cianCircleStartColors[i] : Color.white;
            }
        }

        SetCianCircleVisible(false);
    }

    private void SetCianCircleVisible(bool visible)
    {
        if (cianBacteriaCircle == null) return;
        if (cianBacteriaCircle.activeSelf == visible) return;
        cianBacteriaCircle.SetActive(visible);
    }

    private void OnDisable()
    {
        ForceReturnToIdle();
    }
}
