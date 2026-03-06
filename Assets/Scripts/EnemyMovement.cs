using UnityEngine;
using System.Collections;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Deteccion")]
    [SerializeField] private bool requireDetectionToChase = true;
    [SerializeField] private bool detectionOnlyForBlueAndGreenBacteria = true;
    public float detectionRadiusInTiles = 6f;
    [SerializeField] private float tileSizeWorldUnits = 1f;
    [SerializeField] private LayerMask detectionRaycastMask = ~0;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool invulnerableWhileIdle = true;
    [SerializeField] private float idleSpinSpeedDegrees = 35f;
    [SerializeField] private bool idleSpinClockwise = true;
    [SerializeField] private GameObject cianBacteriaCircle;
    [SerializeField] private float cianCircleFadeDuration = 0.35f;

    [Header("Seguimiento")]
    [SerializeField] private Transform target;
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float stoppingDistance = 0.3f;
    [SerializeField] private float chaseAcceleration = 20f;
    [SerializeField] private float chaseDeceleration = 28f;
    [SerializeField] private float stopVelocityEpsilon = 0.08f;

    [Header("Coral")]
    public bool isCoral = false;
    [SerializeField] private float coralTilesToMove = 3f;
    [SerializeField] private float coralTileSize = 1f;
    [SerializeField] private float coralMoveSpeed = 0.8f;
    [SerializeField] private float coralSmoothTime = 0.2f;
    [SerializeField] private float coralArrivalDistance = 0.02f;
    [SerializeField] private float coralPauseAtEnds = 0.08f;
    public float delayStartMovingCoralTime = 0f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private string movingStateName = "MOVING";
    [SerializeField] private string movingClipKey = "GreenBacteriaMoving";
    [SerializeField] private AnimationClip movingClip;
    [SerializeField] private string deathStateName = "OVER";
    [SerializeField] private string deathClipKey = "GreenBacteriaOver";
    [SerializeField] private AnimationClip deathClip;
    [SerializeField] private float deathPopScale = 1.1f;
    [SerializeField] private float deathFadeOutDuration = 0.24f;

    [Header("Vida")]
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private float hitCooldown = 0.08f;
    [SerializeField] private float destroyDelayAfterDeath = 0f;
    [SerializeField] private string[] damageTags = { "PlayerAttack", "Projectile" };
    [SerializeField] private bool destroyDamageSourceOnHit = false;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.16f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f)] private float damageFlashStrength = 0.9f;
    [SerializeField] private float damageFlashDuration = 0.12f;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip hitSfx;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.9f;

    [Header("Barra Vida")]
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private float healthBarYOffset = 0.24f;
    [SerializeField] private float healthBarWorldWidth = 0.7f;
    [SerializeField] private float healthBarWorldHeight = 0.08f;
    [SerializeField] private Color healthBarBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color healthBarFillColor = new Color(0.26f, 1f, 0.33f, 1f);
    [SerializeField] private int healthBarSortingOffset = 8;

    private Rigidbody2D rb;
    private AnimatorOverrideController overrideController;
    private int currentHealth;
    private bool isDead;
    private float nextAllowedHitTime;
    private string resolvedMovingStateName;
    private float coralBaseY;
    private bool coralMovingUp = true;
    private Transform healthBarRoot;
    private Transform healthBarFill;
    private Color baseSpriteColor = Color.white;
    private float damageFlashTimer;
    private float coralVelocityY;
    private float coralPauseTimer;
    private float coralStartDelayTimer;
    private Vector3 baseScale = Vector3.one;
    private Coroutine deathVisualRoutine;
    private Coroutine cianCircleFadeRoutine;
    private bool hasDetectedPlayer;
    private Collider2D[] cachedEnemyColliders;
    private readonly RaycastHit2D[] detectionHitsBuffer = new RaycastHit2D[16];

    private static Sprite healthBarSprite;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        currentHealth = Mathf.Max(1, maxHealth);
        coralBaseY = rb.position.y;
        coralStartDelayTimer = Mathf.Max(0f, delayStartMovingCoralTime);
        baseScale = transform.localScale;
        CacheEnemyColliders();

        EnsureVisualComponents();
        EnsureAudioSource();
        EnsureCianCircleReference();
        if (spriteRenderer != null)
        {
            baseSpriteColor = spriteRenderer.color;
        }

        SetupAnimator();

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        SetCianCircleVisible(IsIdleDetectionState());

        if (isCoral)
        {
            IgnoreAllTileCollisionsForCoral();
        }
    }

    private void Start()
    {
        resolvedMovingStateName = ResolvePlayableStateName(movingStateName);
        SetupHealthBar();
        PlayMoveAnimation();

        if (isCoral)
        {
            IgnoreAllTileCollisionsForCoral();
        }
    }

    private void OnValidate()
    {
        EnsureVisualComponents();
        EnsureAudioSource();
        detectionRadiusInTiles = Mathf.Max(0f, detectionRadiusInTiles);
        tileSizeWorldUnits = Mathf.Max(0.01f, tileSizeWorldUnits);
        cianCircleFadeDuration = Mathf.Max(0.01f, cianCircleFadeDuration);
        idleSpinSpeedDegrees = Mathf.Max(0f, idleSpinSpeedDegrees);
        delayStartMovingCoralTime = Mathf.Max(0f, delayStartMovingCoralTime);
        EnsureCianCircleReference();
        if (Application.isPlaying) return;
    }

    private void Update()
    {
        UpdateDamageFlash();
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isCoral)
        {
            UpdateCoralMovement();
            return;
        }

        bool useDetectionChase = ShouldUseDetectionChaseForThisEnemy();
        bool wasIdleState = useDetectionChase && !hasDetectedPlayer;
        if (useDetectionChase)
        {
            TryDetectPlayerIfNeeded();
        }
        bool isIdleState = useDetectionChase && !hasDetectedPlayer;

        if (wasIdleState && !isIdleState)
        {
            StartCianCircleFadeAndDestroy();
        }

        if (target == null)
        {
            SetCianCircleVisible(isIdleState);
            if (isIdleState)
            {
                ApplyIdleSpin();
            }
            ApplyChaseVelocity(Vector2.zero);
            return;
        }

        if (isIdleState)
        {
            SetCianCircleVisible(true);
            ApplyIdleSpin();
            ApplyChaseVelocity(Vector2.zero);
            return;
        }

        SetCianCircleVisible(false);

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - currentPos;
        float distance = toTarget.magnitude;

        Vector2 targetVelocity = Vector2.zero;
        if (distance > stoppingDistance)
        {
            Vector2 direction = toTarget.normalized;
            targetVelocity = direction * Mathf.Max(0.01f, speed);
        }
        ApplyChaseVelocity(targetVelocity);

        if (spriteRenderer != null)
        {
            float lookX = rb.linearVelocity.x;
            if (lookX > 0.01f) spriteRenderer.flipX = true;
            else if (lookX < -0.01f) spriteRenderer.flipX = false;
        }
    }

    private void LateUpdate()
    {
        UpdateHealthBarTransform();
    }

    private void UpdateCoralMovement()
    {
        if (coralStartDelayTimer > 0f)
        {
            coralStartDelayTimer = Mathf.Max(0f, coralStartDelayTimer - Time.fixedDeltaTime);
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (coralPauseTimer > 0f)
        {
            coralPauseTimer = Mathf.Max(0f, coralPauseTimer - Time.fixedDeltaTime);
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float travelDistance = Mathf.Max(0f, coralTilesToMove) * Mathf.Max(0.01f, coralTileSize);
        if (travelDistance <= 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float targetY = coralMovingUp ? (coralBaseY + travelDistance) : coralBaseY;
        float nextY = Mathf.SmoothDamp(
            rb.position.y,
            targetY,
            ref coralVelocityY,
            Mathf.Max(0.02f, coralSmoothTime),
            Mathf.Max(0.05f, coralMoveSpeed),
            Time.fixedDeltaTime
        );
        float deltaY = nextY - rb.position.y;
        rb.MovePosition(new Vector2(rb.position.x, nextY));
        rb.linearVelocity = new Vector2(0f, deltaY / Mathf.Max(0.0001f, Time.fixedDeltaTime));

        if (Mathf.Abs(nextY - targetY) <= Mathf.Max(0.001f, coralArrivalDistance))
        {
            rb.MovePosition(new Vector2(rb.position.x, targetY));
            rb.linearVelocity = Vector2.zero;
            coralVelocityY = 0f;
            coralMovingUp = !coralMovingUp;
            coralPauseTimer = Mathf.Max(0f, coralPauseAtEnds);
        }
    }

    public void TakeDamage(int amount = 1)
    {
        if (isDead || amount <= 0) return;
        if (IsIdleInvulnerableState()) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        TriggerDamageFeedback();
        UpdateHealthBarVisual();
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReceiveTagDamage(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryIgnoreTileCollision(collision.collider)) return;
        TryReceiveTagDamage(collision.gameObject);
    }

    private void TryReceiveTagDamage(GameObject damageSource)
    {
        if (isDead || damageSource == null || damageTags == null) return;
        if (Time.time < nextAllowedHitTime) return;
        if (IsIdleInvulnerableState()) return;

        for (int i = 0; i < damageTags.Length; i++)
        {
            string tagName = damageTags[i];
            if (string.IsNullOrEmpty(tagName)) continue;
            if (damageSource.tag != tagName) continue;

            TakeDamage(1);
            nextAllowedHitTime = Time.time + Mathf.Max(0f, hitCooldown);

            if (destroyDamageSourceOnHit)
            {
                Destroy(damageSource);
            }
            return;
        }
    }

    private void SetupAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrideController;

        if (movingClip != null)
        {
            TryOverrideMovingClip(movingClip);
        }
    }

    private void EnsureVisualComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = gameObject.AddComponent<Animator>();
        }

        if (animator != null && animatorController == null)
        {
            animatorController = animator.runtimeAnimatorController;
        }

        if (animator != null && animatorController != null && animator.runtimeAnimatorController != animatorController)
        {
            animator.runtimeAnimatorController = animatorController;
        }

        if (spriteRenderer != null)
        {
            baseSpriteColor = spriteRenderer.color;
        }
    }

    private void PlayMoveAnimation()
    {
        if (animator == null) return;

        if (string.IsNullOrEmpty(resolvedMovingStateName))
        {
            resolvedMovingStateName = ResolvePlayableStateName(movingStateName);
        }

        if (string.IsNullOrEmpty(resolvedMovingStateName)) return;
        animator.Play(resolvedMovingStateName, 0, 0f);
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        coralVelocityY = 0f;
        coralPauseTimer = 0f;

        if (healthBarRoot != null)
        {
            healthBarRoot.gameObject.SetActive(false);
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        float deathDuration = PlayDeathAnimation();

        StartCoroutine(DestroyAfterDeath(deathDuration + Mathf.Max(0f, destroyDelayAfterDeath)));
    }

    private bool TryOverrideClip(string stateName, AnimationClip clip)
    {
        if (overrideController == null || clip == null || string.IsNullOrEmpty(stateName)) return false;

        try
        {
            overrideController[stateName] = clip;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private IEnumerator DestroyAfterDeath(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (deathVisualRoutine != null)
        {
            StopCoroutine(deathVisualRoutine);
            deathVisualRoutine = null;
        }

        if (cianCircleFadeRoutine != null)
        {
            StopCoroutine(cianCircleFadeRoutine);
            cianCircleFadeRoutine = null;
        }

        if (healthBarRoot != null)
        {
            Destroy(healthBarRoot.gameObject);
        }
    }

    private bool TryOverrideMovingClip(AnimationClip clip)
    {
        if (clip == null) return false;

        if (!string.IsNullOrEmpty(movingClipKey) && TryOverrideClip(movingClipKey, clip))
        {
            return true;
        }

        if (TryOverrideClip(movingStateName, clip))
        {
            return true;
        }

        return false;
    }

    private bool TryOverrideDeathClip(AnimationClip clip)
    {
        if (clip == null) return false;

        if (!string.IsNullOrEmpty(deathClipKey) && TryOverrideClip(deathClipKey, clip))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(deathStateName) && TryOverrideClip(deathStateName, clip))
        {
            return true;
        }

        return TryOverrideMovingClip(clip);
    }

    private string ResolvePlayableStateName(string preferredStateName, string fallbackStateName = "MOVING")
    {
        if (animator == null || animator.runtimeAnimatorController == null) return string.Empty;

        if (CanPlayState(preferredStateName)) return preferredStateName;
        string preferredBaseLayer = "Base Layer." + preferredStateName;
        if (CanPlayState(preferredBaseLayer)) return preferredBaseLayer;

        if (CanPlayState(fallbackStateName)) return fallbackStateName;
        string fallbackBaseLayer = "Base Layer." + fallbackStateName;
        if (CanPlayState(fallbackBaseLayer)) return fallbackBaseLayer;

        Debug.LogWarning($"No se encontro un estado valido para EnemyMovement en '{name}'. Revisa estados del Animator.", this);
        return string.Empty;
    }

    private bool CanPlayState(string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;
        return animator.HasState(0, Animator.StringToHash(stateName));
    }

    private void SetupHealthBar()
    {
        if (!showHealthBar || healthBarRoot != null) return;
        if (spriteRenderer == null) return;

        Sprite barSprite = GetHealthBarSprite();
        if (barSprite == null) return;

        GameObject rootObj = new GameObject(name + "_HealthBar");
        healthBarRoot = rootObj.transform;

        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(healthBarRoot, false);
        SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        bgRenderer.sprite = barSprite;
        bgRenderer.color = healthBarBackgroundColor;
        ApplyHealthBarSorting(bgRenderer);
        bgObj.transform.localScale = new Vector3(Mathf.Max(0.2f, healthBarWorldWidth), Mathf.Max(0.02f, healthBarWorldHeight), 1f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(healthBarRoot, false);
        SpriteRenderer fillRenderer = fillObj.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = barSprite;
        fillRenderer.color = healthBarFillColor;
        ApplyHealthBarSorting(fillRenderer);
        healthBarFill = fillObj.transform;

        UpdateHealthBarTransform();
        UpdateHealthBarVisual();
    }

    private void UpdateHealthBarTransform()
    {
        if (healthBarRoot == null || !showHealthBar) return;
        if (isDead)
        {
            healthBarRoot.gameObject.SetActive(false);
            return;
        }

        float yOffset = healthBarYOffset;
        if (spriteRenderer != null)
        {
            yOffset += spriteRenderer.bounds.extents.y;
        }

        Vector3 position = transform.position;
        healthBarRoot.position = new Vector3(position.x, position.y + yOffset, position.z);
    }

    private void UpdateHealthBarVisual()
    {
        if (healthBarFill == null) return;

        float total = Mathf.Max(1f, maxHealth);
        float ratio = Mathf.Clamp01(currentHealth / total);
        float barWidth = Mathf.Max(0.2f, healthBarWorldWidth);
        float barHeight = Mathf.Max(0.02f, healthBarWorldHeight);

        healthBarFill.localScale = new Vector3(barWidth * ratio, barHeight, 1f);
        healthBarFill.localPosition = new Vector3(-(barWidth * (1f - ratio) * 0.5f), 0f, 0f);
        healthBarFill.gameObject.SetActive(ratio > 0f);
    }

    private void ApplyHealthBarSorting(SpriteRenderer renderer)
    {
        if (renderer == null) return;
        if (spriteRenderer != null)
        {
            renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            renderer.sortingOrder = spriteRenderer.sortingOrder + healthBarSortingOffset;
        }
    }

    private static Sprite GetHealthBarSprite()
    {
        if (healthBarSprite != null) return healthBarSprite;

        Texture2D tex = Texture2D.whiteTexture;
        if (tex == null) return null;
        healthBarSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            tex.width
        );
        return healthBarSprite;
    }

    private void ApplyChaseVelocity(Vector2 targetVelocity)
    {
        float accel = targetVelocity.sqrMagnitude > 0.0001f
            ? Mathf.Max(0.01f, chaseAcceleration)
            : Mathf.Max(0.01f, chaseDeceleration);

        Vector2 newVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, accel * Time.fixedDeltaTime);
        float epsilon = Mathf.Max(0.001f, stopVelocityEpsilon);
        if (targetVelocity.sqrMagnitude <= 0.0001f && newVelocity.sqrMagnitude <= (epsilon * epsilon))
        {
            newVelocity = Vector2.zero;
        }

        rb.linearVelocity = newVelocity;
    }

    private float PlayDeathAnimation()
    {
        float duration = 0.35f;

        if (deathClip != null)
        {
            TryOverrideDeathClip(deathClip);
            duration = Mathf.Max(duration, deathClip.length);
        }

        if (animator != null)
        {
            string fallbackState = string.IsNullOrEmpty(resolvedMovingStateName) ? movingStateName : resolvedMovingStateName;
            string deathState = ResolvePlayableStateName(deathStateName, fallbackState);
            if (!string.IsNullOrEmpty(deathState))
            {
                animator.Play(deathState, 0, 0f);
            }
            else if (!string.IsNullOrEmpty(resolvedMovingStateName))
            {
                animator.Play(resolvedMovingStateName, 0, 0f);
            }
        }

        if (deathVisualRoutine != null)
        {
            StopCoroutine(deathVisualRoutine);
        }
        deathVisualRoutine = StartCoroutine(DeathVisualRoutine(duration));

        return duration;
    }

    private IEnumerator DeathVisualRoutine(float totalDuration)
    {
        if (spriteRenderer == null) yield break;

        float duration = Mathf.Max(0.05f, totalDuration);
        float timer = 0f;
        float popScale = Mathf.Max(1f, deathPopScale);
        float popDuration = Mathf.Min(0.12f, duration * 0.35f);
        float fadeDuration = Mathf.Clamp(deathFadeOutDuration, 0.01f, duration);
        float fadeStart = Mathf.Max(0f, duration - fadeDuration);

        Vector3 initialScale = baseScale;
        Color currentColor = spriteRenderer.color;
        float initialAlpha = currentColor.a;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float scaleT = popDuration <= 0.0001f ? 1f : Mathf.Clamp01(timer / popDuration);
            float scaleMul = Mathf.Lerp(1f, popScale, scaleT);
            if (timer > popDuration)
            {
                float settleT = Mathf.Clamp01((timer - popDuration) / Mathf.Max(0.01f, duration - popDuration));
                scaleMul = Mathf.Lerp(popScale, 0.88f, settleT);
            }
            transform.localScale = initialScale * scaleMul;

            if (timer >= fadeStart)
            {
                float alphaT = Mathf.Clamp01((timer - fadeStart) / fadeDuration);
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(initialAlpha, 0f, alphaT);
                spriteRenderer.color = c;
            }

            yield return null;
        }
    }

    private void TriggerDamageFeedback()
    {
        damageFlashTimer = Mathf.Max(damageFlashTimer, Mathf.Max(0.01f, damageFlashDuration));
        PlaySfx(hitSfx, hitSfxVolume);
    }

    private void UpdateDamageFlash()
    {
        if (spriteRenderer == null) return;

        if (damageFlashTimer > 0f)
        {
            damageFlashTimer = Mathf.Max(0f, damageFlashTimer - Time.deltaTime);
            Color flashColor = damageFlashColor;
            flashColor.a = baseSpriteColor.a;
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, flashColor, Mathf.Clamp01(damageFlashStrength));
            return;
        }

        spriteRenderer.color = Color.Lerp(spriteRenderer.color, baseSpriteColor, Mathf.Clamp01(Time.deltaTime * 18f));
    }

    private void EnsureAudioSource()
    {
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void TryDetectPlayerIfNeeded()
    {
        if (!ShouldUseDetectionChaseForThisEnemy() || hasDetectedPlayer || isDead) return;

        if (target == null)
        {
            ResolveTargetReference();
        }
        if (target == null) return;
        if (!IsPlayerTarget(target)) return;

        if (CanDetectPlayerByRaycast(target))
        {
            hasDetectedPlayer = true;
        }
    }

    private bool CanDetectPlayerByRaycast(Transform playerTransform)
    {
        if (playerTransform == null) return false;

        float detectionRadius = Mathf.Max(0f, detectionRadiusInTiles) * Mathf.Max(0.01f, tileSizeWorldUnits);
        if (detectionRadius <= 0.0001f) return false;

        Vector2 rayOrigin = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 toPlayer = (Vector2)playerTransform.position - rayOrigin;
        float distanceToPlayer = toPlayer.magnitude;
        if (distanceToPlayer > detectionRadius) return false;
        if (distanceToPlayer <= 0.0001f) return true;

        Vector2 direction = toPlayer / distanceToPlayer;
        int hitCount = Physics2D.RaycastNonAlloc(rayOrigin, direction, detectionHitsBuffer, distanceToPlayer, detectionRaycastMask);
        if (hitCount <= 0) return false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = detectionHitsBuffer[i];
            Collider2D hitCollider = hit.collider;
            if (hitCollider == null) continue;
            if (hitCollider.transform.IsChildOf(transform)) continue;
            if (hitCollider.GetComponentInParent<PlayerMovement>() != null) return true;
        }

        return false;
    }

    private void ResolveTargetReference()
    {
        if (target != null) return;

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }

        if (target == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    private bool ShouldUseDetectionChaseForThisEnemy()
    {
        if (!requireDetectionToChase) return false;
        if (!detectionOnlyForBlueAndGreenBacteria) return true;

        return IsBlueOrGreenBacteria();
    }

    private bool IsBlueOrGreenBacteria()
    {
        if (isCoral) return false;

        return ContainsBlueOrGreenToken(gameObject.name)
            || ContainsBlueOrGreenToken(movingStateName)
            || ContainsBlueOrGreenToken(movingClipKey);
    }

    private static bool ContainsBlueOrGreenToken(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        string normalized = text.ToLowerInvariant();
        return normalized.Contains("blue") || normalized.Contains("green");
    }

    private bool IsPlayerTarget(Transform candidate)
    {
        if (candidate == null) return false;
        if (candidate.GetComponentInParent<PlayerMovement>() != null) return true;

        return !string.IsNullOrEmpty(playerTag) && candidate.CompareTag(playerTag);
    }

    public bool ShouldDamagePlayerFromCollider(Collider2D enemyCollider)
    {
        if (enemyCollider == null) return false;

        if (IsBlueOrGreenBacteria())
        {
            return enemyCollider is BoxCollider2D;
        }

        return true;
    }

    private bool IsIdleDetectionState()
    {
        return ShouldUseDetectionChaseForThisEnemy() && !hasDetectedPlayer && !isDead;
    }

    private bool IsIdleInvulnerableState()
    {
        return invulnerableWhileIdle && IsIdleDetectionState();
    }

    private void ApplyIdleSpin()
    {
        if (rb == null || idleSpinSpeedDegrees <= 0f) return;

        float direction = idleSpinClockwise ? -1f : 1f;
        float nextRotation = rb.rotation + (direction * idleSpinSpeedDegrees * Time.fixedDeltaTime);
        rb.MoveRotation(nextRotation);
    }

    private void EnsureCianCircleReference()
    {
        if (cianBacteriaCircle != null) return;

        Transform directChild = transform.Find("cianBacteriaCircle");
        if (directChild != null)
        {
            cianBacteriaCircle = directChild.gameObject;
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform) continue;
            if (!string.Equals(child.name, "cianBacteriaCircle", System.StringComparison.OrdinalIgnoreCase)) continue;

            cianBacteriaCircle = child.gameObject;
            return;
        }
    }

    private void SetCianCircleVisible(bool visible)
    {
        if (cianBacteriaCircle == null) return;

        if (!visible)
        {
            if (cianCircleFadeRoutine == null && cianBacteriaCircle.activeSelf)
            {
                cianBacteriaCircle.SetActive(false);
            }
            return;
        }

        if (cianCircleFadeRoutine != null) return;
        if (!cianBacteriaCircle.activeSelf)
        {
            cianBacteriaCircle.SetActive(true);
        }
    }

    private void StartCianCircleFadeAndDestroy()
    {
        if (cianBacteriaCircle == null) return;
        if (cianCircleFadeRoutine != null) return;
        if (!cianBacteriaCircle.activeInHierarchy)
        {
            Destroy(cianBacteriaCircle);
            cianBacteriaCircle = null;
            return;
        }

        cianCircleFadeRoutine = StartCoroutine(FadeAndDestroyCianCircleRoutine());
    }

    private IEnumerator FadeAndDestroyCianCircleRoutine()
    {
        if (cianBacteriaCircle == null)
        {
            cianCircleFadeRoutine = null;
            yield break;
        }

        SpriteRenderer[] renderers = cianBacteriaCircle.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            startColors[i] = renderers[i].color;
        }

        float duration = Mathf.Max(0.01f, cianCircleFadeDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;

                Color c = startColors[i];
                c.a = Mathf.Lerp(startColors[i].a, 0f, t);
                sr.color = c;
            }

            yield return null;
        }

        if (cianBacteriaCircle != null)
        {
            Destroy(cianBacteriaCircle);
            cianBacteriaCircle = null;
        }

        cianCircleFadeRoutine = null;
    }

    private void CacheEnemyColliders()
    {
        cachedEnemyColliders = GetComponents<Collider2D>();
    }

    private void IgnoreAllTileCollisionsForCoral()
    {
        if (!isCoral) return;

        if (cachedEnemyColliders == null || cachedEnemyColliders.Length == 0)
        {
            CacheEnemyColliders();
        }
        if (cachedEnemyColliders == null || cachedEnemyColliders.Length == 0) return;

        TilemapCollider2D[] tilemapColliders = FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < tilemapColliders.Length; i++)
        {
            TilemapCollider2D tileCollider = tilemapColliders[i];
            if (tileCollider == null) continue;

            for (int j = 0; j < cachedEnemyColliders.Length; j++)
            {
                Collider2D enemyCollider = cachedEnemyColliders[j];
                if (enemyCollider == null) continue;
                Physics2D.IgnoreCollision(enemyCollider, tileCollider, true);
            }

            CompositeCollider2D composite = tileCollider.GetComponent<CompositeCollider2D>();
            if (composite == null) continue;

            for (int j = 0; j < cachedEnemyColliders.Length; j++)
            {
                Collider2D enemyCollider = cachedEnemyColliders[j];
                if (enemyCollider == null) continue;
                Physics2D.IgnoreCollision(enemyCollider, composite, true);
            }
        }
    }

    private bool TryIgnoreTileCollision(Collider2D otherCollider)
    {
        if (!isCoral || otherCollider == null) return false;
        if (!IsTileCollider(otherCollider)) return false;

        if (cachedEnemyColliders == null || cachedEnemyColliders.Length == 0)
        {
            CacheEnemyColliders();
        }
        if (cachedEnemyColliders == null || cachedEnemyColliders.Length == 0) return false;

        for (int i = 0; i < cachedEnemyColliders.Length; i++)
        {
            Collider2D enemyCollider = cachedEnemyColliders[i];
            if (enemyCollider == null) continue;
            Physics2D.IgnoreCollision(enemyCollider, otherCollider, true);
        }

        return true;
    }

    private static bool IsTileCollider(Collider2D collider)
    {
        if (collider == null) return false;
        if (collider.GetComponent<TilemapCollider2D>() != null) return true;
        if (collider.GetComponentInParent<TilemapCollider2D>() != null) return true;
        return collider.GetComponentInParent<Tilemap>() != null;
    }
}
