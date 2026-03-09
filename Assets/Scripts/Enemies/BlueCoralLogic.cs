using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class BlueCoralLogic : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Si es falso, el coral se movera desde el inicio.")]
    [SerializeField] private bool requireDetection = true;
    [SerializeField] private float activationRadius = 8f;
    [SerializeField] private bool invulnerableWhileIdle = true;

    [Tooltip("Puedes ver si el enemigo ya esta activo aqui.")]
    [SerializeField] private bool isActive;

    [Header("Orbit Settings")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float circularFollowDelay = 1.2f;
    [SerializeField] private float circularOrbitSpeed = 1f;
    [SerializeField, Min(0.1f)] private float orbitTurnsMultiplier = 1.45f;
    [SerializeField, Min(0.01f)] private float orbitRadius = 3f;
    [SerializeField, Range(0f, 1f)] private float innerRadiusFactor = 0.25f;
    [SerializeField, Min(0.01f)] private float inOutCycleSpeed = 1f;
    [SerializeField] private AnimationCurve inOutRadiusCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float centerFollowSmoothness = 2.2f;
    [SerializeField, Min(0.01f)] private float orbitPositionSmoothTime = 0.09f;
    [SerializeField, Min(0.1f)] private float orbitMaxSpeed = 30f;
    [SerializeField, Min(0.01f)] private float activationBlendDuration = 0.35f;

    [Header("Visuals")]
    [SerializeField] private GameObject cianBacteriaCircle;
    [SerializeField] private float cianCircleFadeDuration = 0.35f;
    [SerializeField] private bool forceRenderOnTop = true;
    [SerializeField] private int bodySortingOrder = 30;
    [SerializeField] private int cianCircleSortingOrder = 29;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private SpriteRenderer bodySpriteRenderer;
    private readonly Queue<Vector2> targetHistory = new Queue<Vector2>();
    private float currentOrbitAngle;
    private float inOutPhase;
    private float activationBlendTimer;
    private Vector2 currentBlueCoralCenter;
    private Vector2 orbitPositionVelocity;
    private bool warnedMissingPlayer;
    private Coroutine cianCircleFadeRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        bodySpriteRenderer = GetComponent<SpriteRenderer>();
        if (health == null)
        {
            health = gameObject.AddComponent<EnemyHealth>();
        }

        currentBlueCoralCenter = rb.position;
        ResolvePlayerTarget();

        if (!requireDetection)
        {
            isActive = true;
        }

        EnsureCianCircleReference();
        ApplyRenderPriority();
        SetCianCircleVisible(!isActive);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnDamaged += WakeUpInstantly;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDamaged -= WakeUpInstantly;
        }
    }

    private void Update()
    {
        if (health.isDead) return;
        if (playerTarget == null) ResolvePlayerTarget();
        if (playerTarget == null) return;

        if (requireDetection && !isActive)
        {
            float distance = Vector2.Distance(transform.position, playerTarget.position);
            if (distance <= activationRadius)
            {
                ActivateCoral();
            }
        }
    }

    private void FixedUpdate()
    {
        if (health.isDead) return;

        bool idle = requireDetection && !isActive;
        health.isInvulnerable = idle && invulnerableWhileIdle;

        if (playerTarget == null) ResolvePlayerTarget();
        if (playerTarget == null || !isActive) return;

        activationBlendTimer = Mathf.Min(
            Mathf.Max(0.01f, activationBlendDuration),
            activationBlendTimer + Time.fixedDeltaTime);
        float activationT = Mathf.Clamp01(activationBlendTimer / Mathf.Max(0.01f, activationBlendDuration));
        activationT = activationT * activationT * (3f - (2f * activationT));

        targetHistory.Enqueue(playerTarget.position);

        int framesToDelay = Mathf.RoundToInt(circularFollowDelay / Time.fixedDeltaTime);
        Vector2 targetCenter = playerTarget.position;

        if (targetHistory.Count > framesToDelay)
        {
            targetCenter = targetHistory.Dequeue();
        }
        else if (targetHistory.Count > 0)
        {
            targetCenter = targetHistory.Peek();
        }

        currentBlueCoralCenter = Vector2.Lerp(
            currentBlueCoralCenter,
            targetCenter,
            Mathf.Max(0.01f, centerFollowSmoothness) * Time.fixedDeltaTime);

        float effectiveOrbitSpeed = Mathf.Max(0.05f, circularOrbitSpeed) * Mathf.Max(0.1f, orbitTurnsMultiplier);
        currentOrbitAngle += effectiveOrbitSpeed * Mathf.Max(0.15f, activationT) * Time.fixedDeltaTime;
        if (currentOrbitAngle >= Mathf.PI * 2f) currentOrbitAngle -= Mathf.PI * 2f;

        inOutPhase += Mathf.Max(0.01f, inOutCycleSpeed) * Time.fixedDeltaTime;
        if (inOutPhase >= 1f) inOutPhase -= Mathf.Floor(inOutPhase);

        float outerRadius = Mathf.Max(0.01f, orbitRadius);
        float innerRadius = Mathf.Clamp(outerRadius * Mathf.Clamp01(innerRadiusFactor), 0f, outerRadius);
        float pingPong = 1f - Mathf.Abs((inOutPhase * 2f) - 1f);
        float curvedInward = Mathf.Clamp01(inOutRadiusCurve.Evaluate(pingPong));
        float dynamicRadius = Mathf.Lerp(outerRadius, innerRadius, curvedInward);

        Vector2 desiredPosition = currentBlueCoralCenter + new Vector2(
            Mathf.Cos(currentOrbitAngle) * dynamicRadius,
            Mathf.Sin(currentOrbitAngle) * dynamicRadius);
        desiredPosition = Vector2.Lerp(rb.position, desiredPosition, activationT);

        Vector2 smoothPosition = Vector2.SmoothDamp(
            rb.position,
            desiredPosition,
            ref orbitPositionVelocity,
            Mathf.Max(0.01f, orbitPositionSmoothTime),
            Mathf.Max(0.1f, orbitMaxSpeed) * Mathf.Lerp(0.2f, 1f, activationT),
            Time.fixedDeltaTime);

        rb.MovePosition(smoothPosition);
    }

    private void ActivateCoral()
    {
        if (isActive) return;
        isActive = true;
        activationBlendTimer = 0f;
        orbitPositionVelocity = Vector2.zero;
        targetHistory.Clear();
        currentBlueCoralCenter = rb != null ? rb.position : (Vector2)transform.position;

        Vector2 toCurrent = ((Vector2)(rb != null ? rb.position : transform.position)) - currentBlueCoralCenter;
        if (toCurrent.sqrMagnitude > 0.0001f)
        {
            currentOrbitAngle = Mathf.Atan2(toCurrent.y, toCurrent.x);
        }

        StartCianCircleFadeAndDestroy();
    }

    private void WakeUpInstantly()
    {
        ActivateCoral();
    }

    private void ResolvePlayerTarget()
    {
        if (playerTarget != null) return;

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            try
            {
                GameObject taggedPlayer = GameObject.FindWithTag(playerTag);
                if (taggedPlayer != null)
                {
                    playerTarget = taggedPlayer.transform;
                    warnedMissingPlayer = false;
                    return;
                }
            }
            catch (UnityException)
            {
                // Ignore missing tag definition and fallback below.
            }
        }

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
        {
            playerTarget = playerMovement.transform;
            warnedMissingPlayer = false;
            return;
        }

        if (!warnedMissingPlayer)
        {
            warnedMissingPlayer = true;
            Debug.LogWarning("BlueCoralLogic: No se encontro Player. Asigna playerTarget o revisa el tag del jugador.", this);
        }
    }

    private void EnsureCianCircleReference()
    {
        if (cianBacteriaCircle != null) return;
        Transform child = transform.Find("cianBacteriaCircle");
        if (child != null) cianBacteriaCircle = child.gameObject;
    }

    private void ApplyRenderPriority()
    {
        if (!forceRenderOnTop) return;

        if (bodySpriteRenderer != null)
        {
            bodySpriteRenderer.sortingOrder = Mathf.Max(bodySpriteRenderer.sortingOrder, bodySortingOrder);
        }

        if (cianBacteriaCircle == null) return;
        SpriteRenderer[] circleRenderers = cianBacteriaCircle.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < circleRenderers.Length; i++)
        {
            if (circleRenderers[i] == null) continue;
            circleRenderers[i].sortingOrder = Mathf.Max(circleRenderers[i].sortingOrder, cianCircleSortingOrder);
        }
    }

    private void SetCianCircleVisible(bool visible)
    {
        if (cianBacteriaCircle != null) cianBacteriaCircle.SetActive(visible);
    }

    private void StartCianCircleFadeAndDestroy()
    {
        if (cianBacteriaCircle == null || cianCircleFadeRoutine != null) return;

        if (!cianBacteriaCircle.activeInHierarchy)
        {
            Destroy(cianBacteriaCircle);
            return;
        }

        cianCircleFadeRoutine = StartCoroutine(FadeAndDestroyCianCircleRoutine());
    }

    private IEnumerator FadeAndDestroyCianCircleRoutine()
    {
        SpriteRenderer[] renderers = cianBacteriaCircle.GetComponentsInChildren<SpriteRenderer>(true);
        if (forceRenderOnTop)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sortingOrder = Mathf.Max(renderers[i].sortingOrder, cianCircleSortingOrder);
            }
        }
        Color[] startColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) startColors[i] = renderers[i].color;
        }

        float timer = 0f;
        while (timer < cianCircleFadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / Mathf.Max(0.01f, cianCircleFadeDuration);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Color c = startColors[i];
                c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                renderers[i].color = c;
            }
            yield return null;
        }

        Destroy(cianBacteriaCircle);
    }

    private void OnDrawGizmosSelected()
    {
        if (!requireDetection) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}
