using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class BacteriaLogic : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 3.5f; 
    [SerializeField] private float chaseAcceleration = 20f;
    [SerializeField] private float chaseDeceleration = 28f;
    [SerializeField] private float stoppingDistance = 0.3f;

    [Header("Detection Raycast")]
    [SerializeField] private bool requireDetectionToChase = true;
    [SerializeField] private string playerTag = "Player";
    public float detectionRadiusInTiles = 6f;
    [SerializeField] private float tileSizeWorldUnits = 1f;
    [SerializeField] private LayerMask detectionRaycastMask = ~0;
    [SerializeField] private bool invulnerableWhileIdle = true;

    [Header("Aggro")]
    public bool useDetectionRadius = true;
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float aggroLoseTime = 5f;

    [Header("Idle State")]
    [SerializeField] private float idleSpinSpeedDegrees = 35f;
    [SerializeField] private bool idleSpinClockwise = true;
    public bool isIdle { get; private set; } = true;

    [Header("Visuals")]
    [SerializeField] private GameObject cianBacteriaCircle;
    [SerializeField] private float cianCircleFadeDuration = 0.35f;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private Transform playerTarget;
    
    private Vector2 initialPosition;
    private bool isAggroed = false;
    private bool hasDetectedPlayer = false;
    private float timeOutOfRange = 0f;
    private Coroutine cianCircleFadeRoutine;
    private readonly RaycastHit2D[] detectionHitsBuffer = new RaycastHit2D[16];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = gameObject.AddComponent<EnemyHealth>();
        }
        initialPosition = rb.position;
        
        if (!requireDetectionToChase)
        {
            hasDetectedPlayer = true;
            isIdle = false;
        }

        EnsureCianCircleReference();
        SetCianCircleVisible(isIdle);
    }

    private void Start()
    {
        ResolvePlayerReference();
        
        // Subscribe to damage so it wakes up instantly if you shoot it!
        if (health != null)
        {
            health.OnDamaged += WakeUpInstantly;
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.OnDamaged -= WakeUpInstantly;
    }

    private void ResolvePlayerReference()
    {
        if (!string.IsNullOrEmpty(playerTag))
        {
            try
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj != null) playerTarget = playerObj.transform;
            }
            catch (UnityException)
            {
                // Ignore invalid/undefined tag and fallback to PlayerMovement lookup below.
            }
        }

        if (playerTarget == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null) playerTarget = player.transform;
        }
    }

    private void WakeUpInstantly()
    {
        if (isIdle)
        {
            hasDetectedPlayer = true;
        }
    }

    private void FixedUpdate()
    {
        if (health.isDead) return;

        // Share invulnerability state with the health script
        health.isInvulnerable = (isIdle && invulnerableWhileIdle);

        if (playerTarget == null) ResolvePlayerReference();

        // 1. Idle & Detection Phase
        if (requireDetectionToChase && !hasDetectedPlayer)
        {
            isIdle = true;
            ApplyIdleSpin();
            TryDetectPlayer();
            ApplyChaseVelocity(Vector2.zero);
            return;
        }

        // 2. Waking Up
        if (isIdle) 
        {
            isIdle = false;
            StartCianCircleFadeAndDestroy();
        }

        if (playerTarget == null)
        {
            ApplyChaseVelocity(Vector2.zero);
            return;
        }

        Vector2 currentPos = rb.position;
        Vector2 targetPos = playerTarget.position;
        Vector2 toTarget = targetPos - currentPos;
        float distanceToPlayer = toTarget.magnitude;

        // 3. Aggro Logic
        if (useDetectionRadius)
        {
            if (distanceToPlayer <= detectionRadius)
            {
                isAggroed = true;
                timeOutOfRange = 0f;
            }
            else if (isAggroed)
            {
                timeOutOfRange += Time.fixedDeltaTime;
                if (timeOutOfRange >= aggroLoseTime)
                {
                    isAggroed = false;
                }
            }
        }
        else
        {
            isAggroed = true;
        }

        // 4. Return Home or Chase
        if (!isAggroed)
        {
            Vector2 toInitial = initialPosition - rb.position;
            if (toInitial.magnitude > 0.1f)
            {
                ApplyChaseVelocity(toInitial.normalized * speed);
            }
            else
            {
                ApplyChaseVelocity(Vector2.zero);
                rb.position = initialPosition;
            }
        }
        else
        {
            Vector2 targetVelocity = Vector2.zero;
            if (distanceToPlayer > stoppingDistance)
            {
                targetVelocity = (toTarget / distanceToPlayer) * Mathf.Max(0.01f, speed);
            }
            ApplyChaseVelocity(targetVelocity);
        }
    }

    private void ApplyChaseVelocity(Vector2 targetVelocity)
    {
        float accel = targetVelocity.sqrMagnitude > 0.0001f ? Mathf.Max(0.01f, chaseAcceleration) : Mathf.Max(0.01f, chaseDeceleration);
        Vector2 newVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, accel * Time.fixedDeltaTime);
        
        if (targetVelocity.sqrMagnitude <= 0.0001f && newVelocity.sqrMagnitude <= 0.0064f)
        {
            newVelocity = Vector2.zero;
        }
        
        rb.linearVelocity = newVelocity;
    }

    private void ApplyIdleSpin()
    {
        if (idleSpinSpeedDegrees <= 0f) return;
        float direction = idleSpinClockwise ? -1f : 1f;
        float nextRotation = rb.rotation + (direction * idleSpinSpeedDegrees * Time.fixedDeltaTime);
        rb.MoveRotation(nextRotation);
    }

    private void TryDetectPlayer()
    {
        if (playerTarget == null) return;

        float searchRadius = Mathf.Max(0f, detectionRadiusInTiles) * Mathf.Max(0.01f, tileSizeWorldUnits);
        if (searchRadius <= 0.0001f) return;

        Vector2 rayOrigin = rb.position;
        Vector2 toPlayer = (Vector2)playerTarget.position - rayOrigin;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer > searchRadius) return;
        
        if (distanceToPlayer <= 0.0001f)
        {
            hasDetectedPlayer = true;
            return;
        }

        Vector2 direction = toPlayer / distanceToPlayer;
        int hitCount = Physics2D.RaycastNonAlloc(rayOrigin, direction, detectionHitsBuffer, distanceToPlayer, detectionRaycastMask);
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = detectionHitsBuffer[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform)) continue;
            
            if (MatchesTagSafe(hitCollider.gameObject, playerTag) || hitCollider.GetComponentInParent<PlayerMovement>() != null)
            {
                hasDetectedPlayer = true;
                return;
            }
        }
    }

    private static bool MatchesTagSafe(GameObject gameObjectToCheck, string tagName)
    {
        if (gameObjectToCheck == null || string.IsNullOrWhiteSpace(tagName)) return false;

        try
        {
            return gameObjectToCheck.CompareTag(tagName);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private void EnsureCianCircleReference()
    {
        if (cianBacteriaCircle != null) return;
        Transform child = transform.Find("cianBacteriaCircle");
        if (child != null) cianBacteriaCircle = child.gameObject;
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
        Color[] startColors = new Color[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++) 
        {
            if (renderers[i] != null) startColors[i] = renderers[i].color;
        }

        float timer = 0f;
        while (timer < cianCircleFadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / cianCircleFadeDuration;
            
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    Color c = startColors[i];
                    c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                    renderers[i].color = c;
                }
            }
            yield return null;
        }
        
        Destroy(cianBacteriaCircle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        float searchRadius = Mathf.Max(0f, detectionRadiusInTiles) * Mathf.Max(0.01f, tileSizeWorldUnits);
        Gizmos.DrawWireSphere(transform.position, searchRadius);
        
        if (useDetectionRadius)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
