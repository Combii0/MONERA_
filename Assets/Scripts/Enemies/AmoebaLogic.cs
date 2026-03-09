using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class AmoebaLogic : MonoBehaviour
{
    [Header("Amoeba Type")]
    [Tooltip("If true, the amoeba stays still and shoots toward the player.")]
    public bool isSmall = true;

    [Header("Detection")]
    [SerializeField] private float activationRadius = 8f;
    [SerializeField] private bool invulnerableWhileIdle = true;
    private Transform playerTarget;
    private bool isActive = false;

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
    [SerializeField] private float timeBetweenShots = 0.2f; // Delay between sequential bullets
    [SerializeField] private float shootSpawnOffset = 0.5f;

    [Header("Floating")]
    [SerializeField] private bool enableFloating = true;
    [SerializeField] private float floatingAmplitude = 0.12f;
    [SerializeField] private float floatingSpeed = 1.8f;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private float burstTimer;
    private bool isShootingBurst = false;
    private Coroutine shootingBurstRoutine;
    private Coroutine cianCircleWarningRoutine;
    private SpriteRenderer[] cianCircleRenderers;
    private Color[] cianCircleStartColors;
    private Vector2 floatingOrigin;
    private float floatingPhaseOffset;
    
    private static Transform projectilesContainer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        EnsureCianCircleReference();
        CacheCianCircleRenderers();
        SetCianCircleVisible(isSmall && !isActive);
        floatingOrigin = rb != null ? rb.position : (Vector2)transform.position;
        floatingPhaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) playerTarget = playerObj.transform;

        EnsureContainerExists();

        if (health != null)
        {
            health.OnDamaged += WakeUpInstantly;
        }
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
        if (projectilesContainer == null)
        {
            GameObject containerObj = GameObject.Find("EnemyProjectiles");
            if (containerObj == null)
            {
                containerObj = new GameObject("EnemyProjectiles");
            }
            projectilesContainer = containerObj.transform;
        }
    }

    private void Update()
    {
        if (health.isDead) return;

        if (!isSmall)
        {
            health.isInvulnerable = false;
            return;
        }

        if (playerTarget == null)
        {
            health.isInvulnerable = !isActive && invulnerableWhileIdle;
            return;
        }

        if (!isActive)
        {
            float distance = Vector2.Distance(transform.position, playerTarget.position);
            if (distance <= activationRadius)
            {
                ActivateAmoeba();
            }
        }
        else
        {
            float distance = Vector2.Distance(transform.position, playerTarget.position);
            if (distance > activationRadius)
            {
                DeactivateAmoeba();
            }
        }

        health.isInvulnerable = !isActive && invulnerableWhileIdle;

        if (isActive && !isShootingBurst)
        {
            HandleBurstTimer();
        }
    }

    private void FixedUpdate()
    {
        ApplyFloatingMotion();
    }

    private void ActivateAmoeba()
    {
        if (isActive) return;
        isActive = true;
        SetCianCircleVisible(false);
    }

    private void DeactivateAmoeba()
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

        RestoreAndShowCianCircle();
    }

    private void WakeUpInstantly()
    {
        if (!isSmall || health == null || health.isDead) return;
        ActivateAmoeba();
    }

    private void HandleBurstTimer()
    {
        burstTimer += Time.deltaTime;

        if (burstTimer >= burstInterval)
        {
            shootingBurstRoutine = StartCoroutine(ShootBurstRoutine());
            burstTimer = 0f;
        }
    }

    private IEnumerator ShootBurstRoutine()
    {
        isShootingBurst = true;

        yield return PlayPreShootWarningRoutine();

        if (health.isDead || playerTarget == null || !isActive)
        {
            isShootingBurst = false;
            shootingBurstRoutine = null;
            yield break;
        }

        SetCianCircleVisible(false);

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            // If the enemy dies while shooting the burst, stop firing
            if (health.isDead || playerTarget == null || !isActive)
            {
                isShootingBurst = false;
                shootingBurstRoutine = null;
                yield break;
            }

            ShootSingleBulletAtPlayer();
            
            // Wait for the specific sequence delay
            yield return new WaitForSeconds(timeBetweenShots);
        }

        isShootingBurst = false;
        shootingBurstRoutine = null;
    }

    private void ShootSingleBulletAtPlayer()
    {
        if (bulletPrefab == null || playerTarget == null) return;
        if (projectilesContainer == null) EnsureContainerExists();

        Vector2 bulletDir = (playerTarget.position - transform.position).normalized;
        Vector3 spawnPos = transform.position + (Vector3)bulletDir * shootSpawnOffset;
        
        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectilesContainer);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = bulletDir * bulletSpeed;

        float rotAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.AngleAxis(rotAngle, Vector3.forward);
    }

    private void ApplyFloatingMotion()
    {
        if (!isSmall || !enableFloating || health == null || health.isDead) return;

        float amplitude = Mathf.Max(0f, floatingAmplitude);
        float speed = Mathf.Max(0f, floatingSpeed);
        float bob = Mathf.Sin((Time.time * speed) + floatingPhaseOffset) * amplitude;

        Vector2 targetPosition = new Vector2(floatingOrigin.x, floatingOrigin.y + bob);
        if (rb != null && rb.simulated)
        {
            rb.MovePosition(targetPosition);
        }
        else
        {
            transform.position = targetPosition;
        }
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
            if (health.isDead || playerTarget == null || !isActive)
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
