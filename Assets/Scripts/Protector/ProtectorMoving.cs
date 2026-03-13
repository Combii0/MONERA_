using System;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class ProtectorMoving : MonoBehaviour
{
    public event Action OnEntrySequenceFinished;
    public bool IsEntrySequenceFinished => canMove && animatorReleased;
    public bool IsEntryStateActive =>
        protectorAnimator != null &&
        protectorAnimator.isActiveAndEnabled &&
        IsEntryState(protectorAnimator.GetCurrentAnimatorStateInfo(0));

    [Header("Entry / Idle")]
    [SerializeField] private Animator protectorAnimator;
    [SerializeField] private string entryStateName = "Entry";
    [SerializeField] private float entryStateTimeoutSeconds = 8f;
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private bool playIdleAnimationWhileMoving = true;
    [SerializeField] private bool disableAnimatorAfterEntry = true;
    [SerializeField] private bool forceIdlePoseBeforeDisabling = true;

    [Header("Player Lock During Entry")]
    [SerializeField] private bool lockPlayerScriptsUntilEntryEnds = true;
    [SerializeField] private bool lockPlayerMovementScript = true;
    [SerializeField] private bool lockPlayerShootingScript = true;

    [Header("Movimiento Pendulo (IDLE)")]
    [SerializeField] private float pendulumLength = 2.25f;
    [SerializeField, Range(5f, 80f)] private float pendulumMaxAngleDegrees = 38f;
    [SerializeField] private float pendulumSpeed = 1.25f;
    [SerializeField] private float horizontalCenterFollowSpeed = 2.4f;

    [Header("Variacion Vertical (independiente del jugador)")]
    [SerializeField] private float yWanderRange = 0.35f;
    [SerializeField] private float yWanderSpeed = 0.25f;

    [Header("Suavizado de Movimiento")]
    [SerializeField] private float positionSmoothTime = 0.12f;
    [SerializeField] private float maxMoveSpeed = 18f;

    [Header("Física")]
    [SerializeField] private bool forceKinematicBody = true;
    [SerializeField] private bool freezeRotation = true;

    [Header("Target")]
    [SerializeField] private Transform playerTarget;

    [Header("Mirada al Jugador")]
    [SerializeField] private SpriteRenderer facingSpriteRenderer;
    [SerializeField] private bool facesRightByDefault = true;
    [SerializeField, Min(0f)] private float facingDeadZoneX = 0.04f;

    private Rigidbody2D rb;
    private Protector protector;
    private Vector2 moveVelocity;
    private Vector2 currentWorldPosition;
    private bool canMove;
    private float randomPhase;
    private float yNoiseSeed;
    private float centerX;
    private float baseY;
    private float fixedZ;
    private bool warnedMissingPlayer;
    private bool animatorReleased;
    private bool externalMovementOverride;
    private bool hasFacingDirection;
    private bool isFacingRight;
    private int entryStateShortHash;
    private int entryStateFullPathHash;
    private int idleStateShortHash;
    private int idleStateFullPathHash;

    private PlayerMovement lockedPlayerMovement;
    private PlayerShooting lockedPlayerShooting;
    private bool cachedPlayerMovementEnabled;
    private bool cachedPlayerShootingEnabled;
    private bool cachedPlayerScriptStates;
    private bool playerScriptsLockedByEntry;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        protector = GetComponent<Protector>();
        if (protectorAnimator == null) protectorAnimator = GetComponent<Animator>();
        if (protectorAnimator == null) protectorAnimator = GetComponentInChildren<Animator>();
        if (facingSpriteRenderer == null) facingSpriteRenderer = GetComponent<SpriteRenderer>();
        entryStateShortHash = Animator.StringToHash(entryStateName);
        entryStateFullPathHash = Animator.StringToHash($"Base Layer.{entryStateName}");
        idleStateShortHash = Animator.StringToHash(idleStateName);
        idleStateFullPathHash = Animator.StringToHash($"Base Layer.{idleStateName}");
        ConfigureRigidbody();
        ResolvePlayerTarget();
        randomPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        yNoiseSeed = UnityEngine.Random.Range(0f, 999f);
        currentWorldPosition = rb != null ? rb.position : (Vector2)transform.position;
        centerX = currentWorldPosition.x;
        baseY = currentWorldPosition.y;
        fixedZ = transform.position.z;
        CacheCurrentFacing();
    }

    private void Start()
    {
        StartCoroutine(StartMovingAfterEntryDelay());
    }

    private IEnumerator StartMovingAfterEntryDelay()
    {
        canMove = false;
        moveVelocity = Vector2.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        TryPlayEntryState();
        LockPlayerScriptsForEntry();
        yield return WaitForEntryToFinish();

        ReleaseFromEntryAnimation();
        currentWorldPosition = rb != null ? rb.position : (Vector2)transform.position;
        centerX = currentWorldPosition.x;
        baseY = currentWorldPosition.y;
        moveVelocity = Vector2.zero;
        canMove = true;
        UnlockPlayerScriptsAfterEntry();
        OnEntrySequenceFinished?.Invoke();
    }

    private void LateUpdate()
    {
        if (!canMove) return;
        if (protector != null && protector.IsDead) return;
        if (externalMovementOverride) return;

        if (playerTarget == null) ResolvePlayerTarget();
        UpdateFacingToPlayer();

        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector2 desiredPosition = CalculateDesiredPendulumPosition(dt, Time.time + randomPhase);
        currentWorldPosition = Vector2.SmoothDamp(
            currentWorldPosition,
            desiredPosition,
            ref moveVelocity,
            Mathf.Max(0.01f, positionSmoothTime),
            Mathf.Max(0.1f, maxMoveSpeed),
            dt
        );

        ApplyWorldPosition(currentWorldPosition);
    }

    public void SetExternalMovementOverride(bool enabled)
    {
        externalMovementOverride = enabled;
        moveVelocity = Vector2.zero;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        if (!enabled)
        {
            SyncInternalStateFromCurrentTransform(false);
        }
    }

    public void SyncInternalStateFromCurrentTransform(bool updateBaseY)
    {
        Vector3 current = transform.position;
        currentWorldPosition = new Vector2(current.x, current.y);
        centerX = currentWorldPosition.x;
        if (updateBaseY) baseY = currentWorldPosition.y;
        moveVelocity = Vector2.zero;
    }

    private Vector2 CalculateDesiredPendulumPosition(float dt, float time)
    {
        if (playerTarget != null)
        {
            float followT = 1f - Mathf.Exp(-Mathf.Max(0.01f, horizontalCenterFollowSpeed) * dt);
            centerX = Mathf.Lerp(centerX, playerTarget.position.x, followT);
        }

        float maxAngleRad = Mathf.Deg2Rad * Mathf.Clamp(pendulumMaxAngleDegrees, 0f, 89f);
        float theta = Mathf.Sin(time * Mathf.Max(0.05f, pendulumSpeed)) * maxAngleRad;

        float length = Mathf.Max(0.01f, pendulumLength);
        float pendulumX = Mathf.Sin(theta) * length;
        float pendulumY = -(1f - Mathf.Cos(theta)) * length * 0.55f;

        float noise = (Mathf.PerlinNoise(yNoiseSeed, Time.time * Mathf.Max(0.01f, yWanderSpeed)) - 0.5f) * 2f;
        float randomYOffset = noise * Mathf.Max(0f, yWanderRange);

        return new Vector2(
            centerX + pendulumX,
            baseY + randomYOffset + pendulumY
        );
    }

    private void ApplyWorldPosition(Vector2 worldPosition)
    {
        // Se aplica al final del frame para ganar a cualquier keyframe de animacion en Transform.
        transform.position = new Vector3(worldPosition.x, worldPosition.y, fixedZ);

        if (rb == null) return;
        rb.position = worldPosition;
    }

    private void ConfigureRigidbody()
    {
        if (rb == null) return;

        rb.gravityScale = 0f;
        rb.angularVelocity = 0f;

        if (forceKinematicBody)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (freezeRotation)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        else
        {
            rb.constraints = RigidbodyConstraints2D.None;
        }
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null) return;

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            playerTarget = player.transform;
            warnedMissingPlayer = false;
            return;
        }

        if (!warnedMissingPlayer)
        {
            warnedMissingPlayer = true;
            Debug.LogWarning("ProtectorMoving: no se encontró PlayerMovement en la escena.", this);
        }
    }

    private IEnumerator WaitForEntryToFinish()
    {
        if (protectorAnimator == null || string.IsNullOrWhiteSpace(entryStateName))
        {
            yield break;
        }

        bool hasEntryState =
            protectorAnimator.HasState(0, entryStateFullPathHash) ||
            protectorAnimator.HasState(0, entryStateShortHash);
        if (!hasEntryState)
        {
            yield break;
        }

        float timeout = Mathf.Max(0.25f, entryStateTimeoutSeconds);
        float elapsed = 0f;
        bool sawEntryState = false;
        const float NoEntryGraceTime = 0.25f;

        while (elapsed < timeout)
        {
            if (protectorAnimator == null || !protectorAnimator.isActiveAndEnabled) break;

            AnimatorStateInfo stateInfo = protectorAnimator.GetCurrentAnimatorStateInfo(0);
            bool isEntryState = IsEntryState(stateInfo);

            if (isEntryState) sawEntryState = true;

            if (sawEntryState && !isEntryState && !protectorAnimator.IsInTransition(0))
            {
                yield break;
            }

            if (sawEntryState && isEntryState && stateInfo.normalizedTime >= 1f && !protectorAnimator.IsInTransition(0))
            {
                yield break;
            }

            if (!sawEntryState && elapsed >= NoEntryGraceTime && !protectorAnimator.IsInTransition(0))
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Si nunca vimos Entry en runtime, arrancamos movimiento de inmediato.
        // Evita que el Protector se quede "congelado" al inicio de la batalla.
        if (!sawEntryState) yield break;
    }

    private void TryPlayEntryState()
    {
        if (protectorAnimator == null || string.IsNullOrWhiteSpace(entryStateName)) return;

        bool hasEntryState =
            protectorAnimator.HasState(0, entryStateFullPathHash) ||
            protectorAnimator.HasState(0, entryStateShortHash);
        if (!hasEntryState) return;

        int entryHashToPlay = protectorAnimator.HasState(0, entryStateFullPathHash) ? entryStateFullPathHash : entryStateShortHash;
        protectorAnimator.enabled = true;
        protectorAnimator.Play(entryHashToPlay, 0, 0f);
        protectorAnimator.Update(0f);
    }

    private bool IsEntryState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.shortNameHash == entryStateShortHash || stateInfo.fullPathHash == entryStateFullPathHash;
    }

    private void UpdateFacingToPlayer()
    {
        if (playerTarget == null) return;

        float deltaX = playerTarget.position.x - transform.position.x;
        if (hasFacingDirection && Mathf.Abs(deltaX) <= Mathf.Max(0f, facingDeadZoneX)) return;

        bool playerOnRight = deltaX >= 0f;
        bool shouldFaceRight = facesRightByDefault ? playerOnRight : !playerOnRight;

        if (hasFacingDirection && shouldFaceRight == isFacingRight) return;

        ApplyFacing(shouldFaceRight);
        isFacingRight = shouldFaceRight;
        hasFacingDirection = true;
    }

    private void CacheCurrentFacing()
    {
        if (facingSpriteRenderer != null)
        {
            isFacingRight = facesRightByDefault ? facingSpriteRenderer.flipX : !facingSpriteRenderer.flipX;
            hasFacingDirection = true;
            return;
        }

        isFacingRight = transform.localScale.x >= 0f;
        hasFacingDirection = true;
    }

    private void ApplyFacing(bool shouldFaceRight)
    {
        bool flipX = facesRightByDefault ? shouldFaceRight : !shouldFaceRight;

        if (facingSpriteRenderer != null)
        {
            facingSpriteRenderer.flipX = flipX;
            return;
        }

        Vector3 scale = transform.localScale;
        float absX = Mathf.Abs(scale.x) > 0.001f ? Mathf.Abs(scale.x) : 1f;
        scale.x = (shouldFaceRight != facesRightByDefault) ? absX : -absX;
        transform.localScale = scale;
    }

    private void ReleaseFromEntryAnimation()
    {
        if (animatorReleased) return;
        animatorReleased = true;

        if (protectorAnimator == null) return;

        bool hasIdleState = false;
        int idleHashToPlay = 0;
        if (!string.IsNullOrWhiteSpace(idleStateName))
        {
            hasIdleState =
                protectorAnimator.HasState(0, idleStateFullPathHash) ||
                protectorAnimator.HasState(0, idleStateShortHash);
            if (hasIdleState)
            {
                idleHashToPlay = protectorAnimator.HasState(0, idleStateFullPathHash) ? idleStateFullPathHash : idleStateShortHash;
            }
        }

        // Prioridad: si queremos animacion de IDLE mientras se mueve, dejamos Animator activo.
        if (playIdleAnimationWhileMoving && hasIdleState)
        {
            protectorAnimator.enabled = true;
            protectorAnimator.Play(idleHashToPlay, 0, 0f);
            protectorAnimator.Update(0f);
            return;
        }

        // Modo pose fija: opcionalmente dejamos una pose de IDLE y luego apagamos Animator.
        if (disableAnimatorAfterEntry)
        {
            if (forceIdlePoseBeforeDisabling && hasIdleState)
            {
                protectorAnimator.enabled = true;
                protectorAnimator.Play(idleHashToPlay, 0, 0f);
                protectorAnimator.Update(0f);
            }

            protectorAnimator.enabled = false;
            return;
        }

        // Si Animator queda activo pero no forzamos IDLE, dejamos el estado actual del controller.
        protectorAnimator.enabled = true;
        if (hasIdleState)
        {
            protectorAnimator.Play(idleHashToPlay, 0, 0f);
            protectorAnimator.Update(0f);
        }
    }

    private void LockPlayerScriptsForEntry()
    {
        if (!lockPlayerScriptsUntilEntryEnds) return;
        ResolvePlayerScriptsToLock();
        if (!cachedPlayerScriptStates) CachePlayerScriptState();

        if (lockPlayerMovementScript && lockedPlayerMovement != null)
        {
            Rigidbody2D playerRb = lockedPlayerMovement.GetComponent<Rigidbody2D>();
            if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
            lockedPlayerMovement.enabled = false;
            playerScriptsLockedByEntry = true;
        }

        if (lockPlayerShootingScript && lockedPlayerShooting != null)
        {
            lockedPlayerShooting.enabled = false;
            playerScriptsLockedByEntry = true;
        }
    }

    private void UnlockPlayerScriptsAfterEntry()
    {
        if (!playerScriptsLockedByEntry) return;
        if (!lockPlayerScriptsUntilEntryEnds) return;
        ResolvePlayerScriptsToLock();

        if (lockPlayerMovementScript && lockedPlayerMovement != null)
        {
            lockedPlayerMovement.enabled = cachedPlayerMovementEnabled;
        }

        if (lockPlayerShootingScript && lockedPlayerShooting != null)
        {
            lockedPlayerShooting.enabled = cachedPlayerShootingEnabled;
        }

        playerScriptsLockedByEntry = false;
    }

    private void ResolvePlayerScriptsToLock()
    {
        if (lockedPlayerMovement == null)
        {
            lockedPlayerMovement = FindFirstObjectByType<PlayerMovement>();
        }

        if (lockedPlayerShooting == null)
        {
            if (lockedPlayerMovement != null)
            {
                lockedPlayerShooting = lockedPlayerMovement.GetComponent<PlayerShooting>();
            }
            if (lockedPlayerShooting == null)
            {
                lockedPlayerShooting = FindFirstObjectByType<PlayerShooting>();
            }
        }
    }

    private void CachePlayerScriptState()
    {
        ResolvePlayerScriptsToLock();
        cachedPlayerMovementEnabled = lockedPlayerMovement == null || lockedPlayerMovement.enabled;
        cachedPlayerShootingEnabled = lockedPlayerShooting == null || lockedPlayerShooting.enabled;
        cachedPlayerScriptStates = true;
    }

    private void OnDisable()
    {
        UnlockPlayerScriptsAfterEntry();
    }
}
