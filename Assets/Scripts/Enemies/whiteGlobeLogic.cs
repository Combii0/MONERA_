using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class whiteGlobeLogic : MonoBehaviour
{
    [Header("White Globe Type")]
    [Tooltip("If true, the white globe behaves as the shooter variant (similar to Amoeba).")]
    public bool isSmall = true;

    [Header("Detection")]
    [SerializeField] private float activationRadius = 8f;
    [SerializeField] private bool invulnerableWhileIdle = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Visuals")]
    [SerializeField] private GameObject cianBacteriaCircle;
    [SerializeField] private float preShootWarningDuration = 0.45f;
    [SerializeField] private float preShootBlinkSpeed = 10f;
    [SerializeField] private Color preShootWarningColor = new Color(1f, 0.15f, 0.15f, 1f);

    [Header("Shooting Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float burstInterval = 3f;
    [SerializeField] private int bulletsPerBurst = 3;
    [SerializeField] private float timeBetweenShots = 0.2f;
    [SerializeField] private float shootSpawnOffset = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip chargeSfx;
    [SerializeField, Range(0f, 1f)] private float chargeSfxVolume = 1f;

    [Header("X Follow (Delay)")]
    [SerializeField] private bool followPlayerX = true;
    [SerializeField, Min(0f)] private float followXDelaySeconds = 1f;
    [SerializeField, Min(0.01f)] private float xFollowSmoothTime = 0.12f;
    [SerializeField, Min(0.1f)] private float xFollowMaxSpeed = 10f;

    [Header("Floating")]
    [SerializeField] private bool enableFloating = true;
    [SerializeField] private float floatingAmplitude = 0.12f;
    [SerializeField] private float floatingSpeed = 1.8f;

    private struct TimedXSample
    {
        public float time;
        public float x;

        public TimedXSample(float sampleTime, float sampleX)
        {
            time = sampleTime;
            x = sampleX;
        }
    }

    private Rigidbody2D rb;
    private EnemyHealth health;
    private Transform playerTarget;
    private AudioSource sfxSource;

    private float burstTimer;
    private bool isActive;
    private bool isShootingBurst;

    private Coroutine shootingBurstRoutine;
    private Coroutine cianCircleWarningRoutine;

    private SpriteRenderer[] cianCircleRenderers;
    private Color[] cianCircleStartColors;

    private Vector2 floatingOrigin;
    private float floatingPhaseOffset;

    private readonly Queue<TimedXSample> xHistory = new Queue<TimedXSample>();
    private float delayedTargetX;
    private float xFollowVelocity;

    private static Transform projectilesContainer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        sfxSource = GetComponent<AudioSource>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.angularVelocity = 0f;
        }

        EnsureCianCircleReference();
        CacheCianCircleRenderers();

        floatingOrigin = rb != null ? rb.position : (Vector2)transform.position;
        delayedTargetX = floatingOrigin.x;
        floatingPhaseOffset = Random.Range(0f, Mathf.PI * 2f);

        SetCianCircleVisible(isSmall && !isActive);
    }

    private void Start()
    {
        ResolvePlayerReference();
        EnsureContainerExists();

        if (health != null)
        {
            health.OnDamaged += WakeUpInstantly;
        }
    }

    private void OnDisable()
    {
        if (shootingBurstRoutine != null)
        {
            StopCoroutine(shootingBurstRoutine);
            shootingBurstRoutine = null;
        }

        if (cianCircleWarningRoutine != null)
        {
            StopCoroutine(cianCircleWarningRoutine);
            cianCircleWarningRoutine = null;
        }

        isShootingBurst = false;
        xHistory.Clear();
        xFollowVelocity = 0f;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= WakeUpInstantly;
        }
    }

    private void EnsureContainerExists()
    {
        if (projectilesContainer != null) return;

        GameObject containerObj = GameObject.Find("EnemyProjectiles");
        if (containerObj == null)
        {
            containerObj = new GameObject("EnemyProjectiles");
        }

        projectilesContainer = containerObj.transform;
    }

    private void ResolvePlayerReference()
    {
        if (playerTarget != null) return;

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            try
            {
                GameObject playerObj = GameObject.FindWithTag(playerTag);
                if (playerObj != null)
                {
                    playerTarget = playerObj.transform;
                    return;
                }
            }
            catch (UnityException)
            {
                // Tag not defined. Fallback below.
            }
        }

        PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null) playerTarget = playerMovement.transform;
    }

    private void Update()
    {
        if (health == null || health.isDead) return;

        if (!isSmall)
        {
            health.isInvulnerable = false;
            return;
        }

        if (playerTarget == null) ResolvePlayerReference();
        if (playerTarget == null)
        {
            health.isInvulnerable = !isActive && invulnerableWhileIdle;
            return;
        }

        float distance = Vector2.Distance(transform.position, playerTarget.position);
        if (!isActive)
        {
            if (distance <= activationRadius)
            {
                ActivateWhiteGlobe();
            }
        }
        else if (distance > activationRadius)
        {
            DeactivateWhiteGlobe();
        }

        health.isInvulnerable = !isActive && invulnerableWhileIdle;

        if (isActive && !isShootingBurst)
        {
            HandleBurstTimer();
        }
    }

    private void FixedUpdate()
    {
        if (health == null || health.isDead || rb == null) return;

        ApplyHorizontalFollowAndFloating();
    }

    private void ActivateWhiteGlobe()
    {
        if (isActive) return;

        isActive = true;
        SetCianCircleVisible(false);
        PlayChargeSfx();

        xHistory.Clear();
        delayedTargetX = rb != null ? rb.position.x : transform.position.x;
        xFollowVelocity = 0f;
    }

    private void PlayChargeSfx()
    {
        if (chargeSfx == null) return;

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        sfxSource.PlayOneShot(chargeSfx, Mathf.Clamp01(chargeSfxVolume));
    }

    private void DeactivateWhiteGlobe()
    {
        if (!isActive) return;

        isActive = false;
        burstTimer = 0f;

        if (shootingBurstRoutine != null)
        {
            StopCoroutine(shootingBurstRoutine);
            shootingBurstRoutine = null;
        }

        isShootingBurst = false;
        xHistory.Clear();
        xFollowVelocity = 0f;

        RestoreAndShowCianCircle();
    }

    private void WakeUpInstantly()
    {
        if (!isSmall || health == null || health.isDead) return;
        ActivateWhiteGlobe();
    }

    private void HandleBurstTimer()
    {
        burstTimer += Time.deltaTime;

        if (burstTimer >= Mathf.Max(0.05f, burstInterval))
        {
            shootingBurstRoutine = StartCoroutine(ShootBurstRoutine());
            burstTimer = 0f;
        }
    }

    private void ApplyHorizontalFollowAndFloating()
    {
        float targetX = rb.position.x;

        if (isActive && followPlayerX && playerTarget != null)
        {
            UpdateDelayedXTarget();
            targetX = delayedTargetX;
        }
        else if (!isActive)
        {
            delayedTargetX = floatingOrigin.x;
        }

        float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        float smoothX = Mathf.SmoothDamp(
            rb.position.x,
            targetX,
            ref xFollowVelocity,
            Mathf.Max(0.01f, xFollowSmoothTime),
            Mathf.Max(0.1f, xFollowMaxSpeed),
            dt);

        float targetY = rb.position.y;
        if (isSmall && enableFloating)
        {
            float amplitude = Mathf.Max(0f, floatingAmplitude);
            float speed = Mathf.Max(0f, floatingSpeed);
            float bob = Mathf.Sin((Time.time * speed) + floatingPhaseOffset) * amplitude;
            targetY = floatingOrigin.y + bob;
        }

        Vector2 nextPosition = new Vector2(smoothX, targetY);
        Vector2 computedVelocity = (nextPosition - rb.position) / dt;
        rb.linearVelocity = computedVelocity;
        rb.MovePosition(nextPosition);
    }

    private void UpdateDelayedXTarget()
    {
        if (playerTarget == null) return;

        float delay = Mathf.Max(0f, followXDelaySeconds);
        float now = Time.time;

        if (delay <= 0f)
        {
            delayedTargetX = playerTarget.position.x;
            xHistory.Clear();
            return;
        }

        xHistory.Enqueue(new TimedXSample(now, playerTarget.position.x));

        bool updated = false;
        while (xHistory.Count > 0)
        {
            TimedXSample sample = xHistory.Peek();
            if (now - sample.time < delay) break;

            delayedTargetX = sample.x;
            updated = true;
            xHistory.Dequeue();
        }

        if (!updated && xHistory.Count == 0)
        {
            delayedTargetX = playerTarget.position.x;
        }
    }

    private IEnumerator ShootBurstRoutine()
    {
        isShootingBurst = true;

        yield return PlayPreShootWarningRoutine();

        if (health == null || health.isDead || playerTarget == null || !isActive)
        {
            isShootingBurst = false;
            shootingBurstRoutine = null;
            yield break;
        }

        SetCianCircleVisible(false);

        int safeBulletsPerBurst = Mathf.Max(1, bulletsPerBurst);
        for (int i = 0; i < safeBulletsPerBurst; i++)
        {
            if (health == null || health.isDead || playerTarget == null || !isActive)
            {
                isShootingBurst = false;
                shootingBurstRoutine = null;
                yield break;
            }

            ShootSingleBulletAtPlayer();
            yield return new WaitForSeconds(Mathf.Max(0.01f, timeBetweenShots));
        }

        isShootingBurst = false;
        shootingBurstRoutine = null;
    }

    private void ShootSingleBulletAtPlayer()
    {
        if (bulletPrefab == null || playerTarget == null) return;
        if (projectilesContainer == null) EnsureContainerExists();

        Vector2 bulletDir = ((Vector2)playerTarget.position - (Vector2)transform.position).normalized;
        Vector3 spawnPos = transform.position + ((Vector3)bulletDir * shootSpawnOffset);

        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectilesContainer);
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = bulletDir * Mathf.Max(0.01f, bulletSpeed);
        }

        float rotAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.AngleAxis(rotAngle, Vector3.forward);
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

    private void RestoreAndShowCianCircle()
    {
        if (cianBacteriaCircle == null) return;

        if (cianCircleWarningRoutine != null)
        {
            StopCoroutine(cianCircleWarningRoutine);
            cianCircleWarningRoutine = null;
        }

        RestoreCianCircleColors();
        cianBacteriaCircle.SetActive(true);
    }

    private IEnumerator PlayPreShootWarningRoutine()
    {
        if (cianBacteriaCircle == null) yield break;

        if (cianCircleRenderers == null || cianCircleStartColors == null)
        {
            CacheCianCircleRenderers();
        }

        if (cianCircleRenderers == null || cianCircleStartColors == null) yield break;

        if (cianCircleWarningRoutine != null)
        {
            StopCoroutine(cianCircleWarningRoutine);
            cianCircleWarningRoutine = null;
        }

        cianCircleWarningRoutine = StartCoroutine(WarningBlinkRoutine());
        yield return cianCircleWarningRoutine;
    }

    private IEnumerator WarningBlinkRoutine()
    {
        if (cianBacteriaCircle == null)
        {
            cianCircleWarningRoutine = null;
            yield break;
        }

        if (cianCircleRenderers == null || cianCircleStartColors == null)
        {
            cianCircleWarningRoutine = null;
            yield break;
        }

        RestoreCianCircleColors();
        cianBacteriaCircle.SetActive(true);

        float timer = 0f;
        float duration = Mathf.Max(0.05f, preShootWarningDuration);
        float blinkSpeed = Mathf.Max(0.1f, preShootBlinkSpeed);
        float toggleInterval = 0.5f / blinkSpeed;
        float toggleTimer = 0f;
        bool showRed = true;

        while (timer < duration)
        {
            if (health == null || health.isDead || playerTarget == null || !isActive)
            {
                RestoreCianCircleColors();
                cianCircleWarningRoutine = null;
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
                ApplyWarningColor();
            }
            else
            {
                SetCianCircleVisible(false);
            }

            yield return null;
        }

        SetCianCircleVisible(false);
        cianCircleWarningRoutine = null;
    }

    private void ApplyWarningColor()
    {
        if (cianCircleRenderers == null || cianCircleStartColors == null) return;

        for (int i = 0; i < cianCircleRenderers.Length; i++)
        {
            if (cianCircleRenderers[i] == null) continue;

            float alpha = cianCircleStartColors[i].a;
            cianCircleRenderers[i].color = new Color(
                preShootWarningColor.r,
                preShootWarningColor.g,
                preShootWarningColor.b,
                alpha);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}
