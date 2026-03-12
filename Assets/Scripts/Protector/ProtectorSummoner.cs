using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ProtectorSummoner : MonoBehaviour
{
    [Header("Trigger")]
    public bool IsSummoning;
    public bool IsExecutingSummon => isExecutingSummon;
    public bool IsMovementOverrideApplied => movementOverrideApplied;

    [Header("Secuencia")]
    [SerializeField] private Vector2 summonCenterPoint = new Vector2(0.04f, 0.15f);
    [SerializeField] private float moveToCenterSpeed = 10f;
    [SerializeField, Min(0f)] private float summonSpawnDelayAfterAnimationStart = 0.2f;
    [SerializeField, Min(0.1f)] private float returnToMovingAfterAnimationStart = 1.2f;
    [SerializeField] private string summonerStateName = "SUMMONER";
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField, Min(0.05f)] private float fallbackOneShotDuration = 1f;

    [Header("Invocacion")]
    [SerializeField] private GameObject whiteGlobePrefab;
    [SerializeField] private Transform summonedEnemiesContainer;
    [SerializeField] private Vector2 rightSpawnPoint = new Vector2(3.5f, 1.5f);
    [SerializeField] private Vector2 leftSpawnPoint = new Vector2(-3.5f, 1.5f);
    [SerializeField] private float summonSpawnZ = 0f;
    [SerializeField, Min(0.01f)] private float summonFadeInDuration = 0.35f;

    [Header("Safety")]
    [SerializeField, Min(0.5f)] private float minimumRuntimeMoveSpeed = 6f;
    [SerializeField, Min(0.1f)] private float moveTimeoutPaddingSeconds = 0.75f;

    private Rigidbody2D rb;
    private Animator protectorAnimator;
    private Protector protector;
    private ProtectorMoving protectorMoving;

    private bool isExecutingSummon;
    private bool movementOverrideApplied;
    private Coroutine activeSummonRoutine;

    private bool hasForcedWorldPosition;
    private Vector2 forcedWorldPosition;
    private float fixedZ;

    private int summonerStateHash;
    private int summonerStateFullPathHash;
    private int idleStateHash;
    private int idleStateFullPathHash;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        protector = GetComponent<Protector>();
        protectorMoving = GetComponent<ProtectorMoving>();
        protectorAnimator = GetComponent<Animator>();

        fixedZ = transform.position.z;

        summonerStateHash = Animator.StringToHash(summonerStateName);
        summonerStateFullPathHash = Animator.StringToHash($"Base Layer.{summonerStateName}");
        idleStateHash = Animator.StringToHash(idleStateName);
        idleStateFullPathHash = Animator.StringToHash($"Base Layer.{idleStateName}");
    }

    private void Update()
    {
        if (!IsSummoning || isExecutingSummon) return;

        if (protectorMoving != null && !protectorMoving.IsEntrySequenceFinished)
        {
            // Garantiza que primero termine el estado post-Entry.
            IsSummoning = false;
            return;
        }

        IsSummoning = false;
        if (ShouldAbortSummon()) return;

        activeSummonRoutine = StartCoroutine(PerformSummonRoutine());
    }

    private void LateUpdate()
    {
        if (hasForcedWorldPosition)
        {
            ApplyWorldPositionImmediate(forcedWorldPosition);
        }
    }

    private IEnumerator PerformSummonRoutine()
    {
        isExecutingSummon = true;
        ApplyMovementOverride(true);

        // 1) Se desplaza suave al punto central antes de invocar.
        yield return MoveTo(summonCenterPoint, moveToCenterSpeed, 10f);
        if (ShouldAbortSummon())
        {
            FinishSummon();
            yield break;
        }

        // Mantiene la posicion fija durante la animacion de invocacion.
        SetWorldPosition(summonCenterPoint);

        // 2) Reproduce la animacion de invocacion y fuerza una sola repeticion.
        float oneShotDuration = PlaySummonerAnimationAndResolveDuration();
        float totalDuration = Mathf.Max(0.1f, returnToMovingAfterAnimationStart);
        float spawnAt = Mathf.Clamp(summonSpawnDelayAfterAnimationStart, 0f, totalDuration);

        bool hasSpawned = false;
        bool restoredIdle = false;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            if (ShouldAbortSummon())
            {
                FinishSummon();
                yield break;
            }

            elapsed += Time.deltaTime;
            SetWorldPosition(summonCenterPoint);

            if (!hasSpawned && elapsed >= spawnAt)
            {
                SpawnWhiteGlobesWithFadeIn();
                hasSpawned = true;
            }

            if (!restoredIdle && elapsed >= oneShotDuration)
            {
                PlayIdleStateIfAvailable();
                restoredIdle = true;
            }

            yield return null;
        }

        if (!hasSpawned)
        {
            SpawnWhiteGlobesWithFadeIn();
        }

        if (!restoredIdle)
        {
            PlayIdleStateIfAvailable();
        }

        FinishSummon();
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
            if (ShouldAbortSummon()) yield break;

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

    private float PlaySummonerAnimationAndResolveDuration()
    {
        if (protectorAnimator == null || string.IsNullOrWhiteSpace(summonerStateName))
        {
            return Mathf.Max(0.05f, fallbackOneShotDuration);
        }

        protectorAnimator.enabled = true;

        bool hasSummonerState =
            protectorAnimator.HasState(0, summonerStateFullPathHash) ||
            protectorAnimator.HasState(0, summonerStateHash);
        if (!hasSummonerState)
        {
            Debug.LogWarning($"ProtectorSummoner: El estado '{summonerStateName}' no existe en el Animator.", this);
            return Mathf.Max(0.05f, fallbackOneShotDuration);
        }

        int stateToPlay = protectorAnimator.HasState(0, summonerStateFullPathHash)
            ? summonerStateFullPathHash
            : summonerStateHash;

        protectorAnimator.Play(stateToPlay, 0, 0f);
        protectorAnimator.Update(0f);

        AnimatorStateInfo stateInfo = protectorAnimator.GetCurrentAnimatorStateInfo(0);
        bool isSummonerState =
            stateInfo.shortNameHash == summonerStateHash ||
            stateInfo.fullPathHash == summonerStateFullPathHash;

        if (isSummonerState && stateInfo.length > 0f)
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

    private void SpawnWhiteGlobesWithFadeIn()
    {
        ResolveWhiteGlobePrefabFallback();
        if (whiteGlobePrefab == null)
        {
            Debug.LogWarning("ProtectorSummoner: No hay whiteGlobePrefab asignado para invocar.", this);
            return;
        }

        Transform parent = ResolveSummonedEnemiesContainer();

        SpawnSingleWhiteGlobe(rightSpawnPoint, parent);
        SpawnSingleWhiteGlobe(leftSpawnPoint, parent);
    }

    private void SpawnSingleWhiteGlobe(Vector2 spawnPoint, Transform parent)
    {
        Vector3 worldPosition = new Vector3(spawnPoint.x, spawnPoint.y, summonSpawnZ);
        GameObject spawned = Instantiate(whiteGlobePrefab, worldPosition, Quaternion.identity, parent);
        if (spawned == null) return;

        StartCoroutine(FadeInSpawnedObject(spawned));
    }

    private IEnumerator FadeInSpawnedObject(GameObject spawnedObject)
    {
        if (spawnedObject == null) yield break;

        SpriteRenderer[] renderers = spawnedObject.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0) yield break;

        Color[] targetColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            targetColors[i] = renderers[i].color;
            Color hidden = targetColors[i];
            hidden.a = 0f;
            renderers[i].color = hidden;
        }

        float duration = Mathf.Max(0.01f, summonFadeInDuration);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Color target = targetColors[i];
                Color c = target;
                c.a = Mathf.Lerp(0f, target.a, t);
                renderers[i].color = c;
            }

            yield return null;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].color = targetColors[i];
        }
    }

    private Transform ResolveSummonedEnemiesContainer()
    {
        if (summonedEnemiesContainer != null) return summonedEnemiesContainer;

        GameObject containerObj = GameObject.Find("SummonedWhiteGlobes");
        if (containerObj == null)
        {
            containerObj = new GameObject("SummonedWhiteGlobes");
        }

        summonedEnemiesContainer = containerObj.transform;
        return summonedEnemiesContainer;
    }

    private void ResolveWhiteGlobePrefabFallback()
    {
        if (whiteGlobePrefab != null) return;

        whiteGlobeLogic globeInScene = FindFirstObjectByType<whiteGlobeLogic>();
        if (globeInScene != null)
        {
            whiteGlobePrefab = globeInScene.gameObject;
        }
    }

    private bool ShouldAbortSummon()
    {
        return protector != null && protector.IsDead;
    }

    private void FinishSummon()
    {
        FlushForcedPositionToTransform();
        ApplyMovementOverride(false);
        hasForcedWorldPosition = false;
        isExecutingSummon = false;
        activeSummonRoutine = null;
    }

    public void ForceReturnToIdle()
    {
        if (activeSummonRoutine != null)
        {
            StopCoroutine(activeSummonRoutine);
            activeSummonRoutine = null;
        }

        FlushForcedPositionToTransform();
        PlayIdleStateIfAvailable();
        ApplyMovementOverride(false);
        hasForcedWorldPosition = false;
        isExecutingSummon = false;
        IsSummoning = false;
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

    private void OnDisable()
    {
        ForceReturnToIdle();
    }
}
