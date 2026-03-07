using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimiento")]
    [SerializeField] public float moveSpeed = 7f;
    [SerializeField] public float groundAcceleration = 60f;
    [SerializeField] public float groundDeceleration = 70f;
    [SerializeField] public float airAcceleration = 35f;
    [SerializeField] public float airDeceleration = 25f;
    [SerializeField, HideInInspector] private bool forceFrictionlessContact = true;

    [Header("Salto")]
    [SerializeField] public float jumpForce = 12f;
    [SerializeField] private bool enableDoubleJump = true;
    [SerializeField] public float doubleJumpForce = 9.5f;
    [SerializeField] public float coyoteTime = 0.12f;
    [SerializeField] public float jumpBufferTime = 0.12f;
    [SerializeField] public float fallGravityMultiplier = 2.2f;
    [SerializeField] public float lowJumpGravityMultiplier = 1.8f;
    [SerializeField] public float maxFallSpeed = 22f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, HideInInspector] private float groundCheckExtra = 0.1f;

    [Header("Pared")]
    [SerializeField] private bool enableWallJump = true;
    [SerializeField] public Vector2 wallJumpForce = new Vector2(8.5f, 10.5f);
    [SerializeField] public float wallJumpInputLockTime = 0.12f;
    [SerializeField] public float wallSlideSpeed = 2.6f;
    [SerializeField, HideInInspector] private float wallCheckDistance = 0.08f;
    [SerializeField, HideInInspector] private float wallCheckTopRatio = 0.7f;
    [SerializeField, HideInInspector] private float wallCheckMidRatio = 0.45f;

    [Header("Color Adaptativo")]
    [SerializeField, HideInInspector] private bool enableAdaptiveTint = true;
    [SerializeField, HideInInspector] private float tintSampleRadius = 2.25f;
    [SerializeField, Range(0f, 1f), HideInInspector] private float tintInfluence = 0.35f;
    [SerializeField, Range(0f, 1f), HideInInspector] private float pastelAmount = 0.45f;
    [SerializeField, Range(0f, 1f), HideInInspector] private float ambientInfluence = 0.25f;
    [SerializeField, HideInInspector] private LayerMask tintSampleLayers = ~0;
    [SerializeField, HideInInspector] private float tintUpdateInterval = 0.08f;
    [SerializeField, HideInInspector] private float tintSmoothing = 7f;
    [SerializeField, HideInInspector] private int tintMaxSamples = 20;
    [SerializeField, HideInInspector] private Color tintFallbackColor = Color.white;

    [Header("Daño")]
    [SerializeField, HideInInspector] private bool detectEnemyContacts = true;
    [SerializeField, HideInInspector] private float contactKnockbackForce = 8.5f;
    [SerializeField, HideInInspector] private float contactKnockbackUpward = 2.4f;
    [SerializeField, HideInInspector] private float contactFlashDuration = 0.5f;
    [SerializeField, HideInInspector] private float contactInvulnerabilityDuration = 2f;
    [SerializeField, HideInInspector] private float invulnerabilityRefreshInterval = 0.12f;
    [SerializeField, HideInInspector] private bool showInvulnerabilityBlink = true;
    [SerializeField, HideInInspector] private float invulnerabilityBlinkSpeed = 12f;
    [SerializeField, Range(0f, 1f), HideInInspector] private float invulnerabilityBlinkStrength = 0.55f;
    [SerializeField, HideInInspector] private Color invulnerabilityBlinkColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField, HideInInspector] private Color damageFlashColor = new Color(1f, 0.16f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f), HideInInspector] private float damageFlashStrength = 0.92f;

    [Header("Disparo")]
    [SerializeField] private bool enableShooting = true;
    [SerializeField] private Sprite projectileSprite;
    [SerializeField, HideInInspector] private float projectileSpeed = 14f;
    [SerializeField, HideInInspector] private int projectileDamage = 1;
    [SerializeField, HideInInspector] private float projectileLifetime = 2.5f;
    [SerializeField] private float shootCooldown = 0.2f;
    [SerializeField, HideInInspector] private Vector2 shootSpawnOffset = new Vector2(0.55f, 0.05f);
    [SerializeField, HideInInspector] private float projectileColliderRadius = 0.12f;
    [SerializeField, HideInInspector] private float projectileScale = 1f;
    [SerializeField, HideInInspector] private int projectileSortingOrderOffset = 2;
    [SerializeField, HideInInspector] private Color projectileColor = Color.white;

    [Header("Audio Player")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField] private AudioClip hurtSfx;
    [FormerlySerializedAs("shootSfx")]
    [SerializeField] private AudioClip projectileShootSfx;
    [SerializeField, Range(0f, 1f), HideInInspector] private float jumpSfxVolume = 1f;
    [SerializeField, Range(0f, 1f), HideInInspector] private float hurtSfxVolume = 1f;
    [FormerlySerializedAs("shootSfxVolume")]
    [SerializeField, Range(0f, 1f), HideInInspector] private float projectileShootSfxVolume = 0.9f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction shootAction;

    private float moveInput;
    private bool jumpHeld;
    private bool isGrounded;
    private bool isWallSliding;
    private int touchingWallDirection;
    private int extraJumpsRemaining;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float wallJumpInputLockTimer;
    private string currentAnimation;

    private float tintTimer;
    private Color baseSpriteColor;
    private Color targetSpriteColor;
    private Collider2D[] tintSampleBuffer;
    private ContactFilter2D tintContactFilter;

    private float damageFlashTimer;
    private float invulnerabilityTimer;
    private float invulnerabilityRefreshTimer;
    private float nextShootTime;
    private int facingDirection = 1;

    private Collider2D[] playerColliders;
    private readonly List<Collider2D> ignoredEnemyColliders = new List<Collider2D>(24);
    private GameManager gameManager;
    private PhysicsMaterial2D runtimePhysicsMaterial;

    private const string ANIM_IDLE = "Normal";
    private const string ANIM_WALK = "Walk";
    private const string ANIM_JUMP = "Jump";
    private const string PLAYER_RUNTIME_PHYSICS_MATERIAL = "Player_RuntimePhysics";
    private int EffectiveGroundMask => groundLayer.value == 0 ? Physics2D.DefaultRaycastLayers : groundLayer.value;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        EnsureFrictionlessContactMaterial();

        playerColliders = GetComponents<Collider2D>();
        baseSpriteColor = spriteRenderer.color;
        targetSpriteColor = baseSpriteColor;

        EnsureAudioSource();
        SetupAdaptiveTint();
        BuildInputActions();
    }

    private void EnsureFrictionlessContactMaterial()
    {
        if (!forceFrictionlessContact) return;

        if (runtimePhysicsMaterial == null)
        {
            runtimePhysicsMaterial = new PhysicsMaterial2D(PLAYER_RUNTIME_PHYSICS_MATERIAL)
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        if (boxCollider != null && boxCollider.sharedMaterial != runtimePhysicsMaterial)
        {
            boxCollider.sharedMaterial = runtimePhysicsMaterial;
        }

        if (rb != null && rb.sharedMaterial != runtimePhysicsMaterial)
        {
            rb.sharedMaterial = runtimePhysicsMaterial;
        }
    }

    private void OnEnable()
    {
        CacheGameManagerReference();
        moveAction?.Enable();
        jumpAction?.Enable();
        shootAction?.Enable();
    }

    private void OnDisable()
    {
        EndInvulnerabilityPhase();
        moveAction?.Disable();
        jumpAction?.Disable();
        shootAction?.Disable();
    }

    private void OnDestroy()
    {
        EndInvulnerabilityPhase();
        moveAction?.Dispose();
        jumpAction?.Dispose();
        shootAction?.Dispose();
    }

    private void BuildInputActions()
    {
        moveAction = new InputAction(name: "Move", type: InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.AddBinding("<Gamepad>/dpad");

        jumpAction = new InputAction(name: "Jump", type: InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        shootAction = new InputAction(name: "Shoot", type: InputActionType.Button);
        shootAction.AddBinding("<Mouse>/leftButton");
        shootAction.AddBinding("<Keyboard>/j");
        shootAction.AddBinding("<Keyboard>/k");
        shootAction.AddBinding("<Gamepad>/buttonWest");
        shootAction.AddBinding("<Gamepad>/rightTrigger");
    }

    private void Update()
    {
        ReadInput();
        UpdateGroundAndWallState();
        UpdateFacingDirection();
        UpdateAnimationState();
        UpdateAdaptiveTint();
        UpdateDamageFlash();
        UpdateInvulnerability();
        ApplyInvulnerabilityBlink();
        TryShoot();
    }

    private void FixedUpdate()
    {
        wallJumpInputLockTimer = Mathf.Max(0f, wallJumpInputLockTimer - Time.fixedDeltaTime);
        ApplyHorizontalMovement();
        TryPerformJump();
        ApplyWallSlide();
        ApplyBetterGravity();
    }

    private void ReadInput()
    {
        moveInput = Mathf.Clamp(moveAction.ReadValue<Vector2>().x, -1f, 1f);

        if (jumpAction.WasPressedThisFrame())
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - Time.deltaTime);
        }

        jumpHeld = jumpAction.IsPressed();
    }

    private void UpdateGroundAndWallState()
    {
        isGrounded = IsGrounded();
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            extraJumpsRemaining = enableDoubleJump ? 1 : 0;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.deltaTime);
        }

        touchingWallDirection = GetTouchingWallDirection();
        isWallSliding = enableWallJump
            && !isGrounded
            && touchingWallDirection != 0
            && Mathf.Abs(moveInput) > 0.05f
            && Mathf.Sign(moveInput) == touchingWallDirection
            && rb.linearVelocity.y < 0f;
    }

    private void UpdateFacingDirection()
    {
        if (moveInput > 0.01f) spriteRenderer.flipX = false;
        else if (moveInput < -0.01f) spriteRenderer.flipX = true;

        facingDirection = spriteRenderer.flipX ? -1 : 1;
    }

    private void ApplyHorizontalMovement()
    {
        if (wallJumpInputLockTimer > 0f) return;

        float targetX = moveInput * moveSpeed;
        float accel = Mathf.Abs(moveInput) > 0.01f
            ? (isGrounded ? groundAcceleration : airAcceleration)
            : (isGrounded ? groundDeceleration : airDeceleration);
        float newVelX = Mathf.MoveTowards(rb.linearVelocity.x, targetX, accel * Time.fixedDeltaTime);

        if (Mathf.Abs(moveInput) > 0.01f && IsTouchingWall(moveInput))
        {
            newVelX = 0f;
        }

        rb.linearVelocity = new Vector2(newVelX, rb.linearVelocity.y);
    }

    private void TryPerformJump()
    {
        if (jumpBufferTimer <= 0f) return;

        if (CanWallJump())
        {
            float jumpDirectionX = -touchingWallDirection;
            rb.linearVelocity = new Vector2(jumpDirectionX * wallJumpForce.x, wallJumpForce.y);
            PlaySfx(jumpSfx, jumpSfxVolume);
            wallJumpInputLockTimer = wallJumpInputLockTime;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            isGrounded = false;
            isWallSliding = false;
            return;
        }

        if (coyoteTimer > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            PlaySfx(jumpSfx, jumpSfxVolume);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            isGrounded = false;
            return;
        }

        if (!isGrounded && enableDoubleJump && extraJumpsRemaining > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
            PlaySfx(jumpSfx, jumpSfxVolume);
            extraJumpsRemaining--;
            jumpBufferTimer = 0f;
            isWallSliding = false;
        }
    }

    private bool CanWallJump()
    {
        return enableWallJump && !isGrounded && touchingWallDirection != 0 && rb.linearVelocity.y <= 0.2f;
    }

    private void ApplyWallSlide()
    {
        if (!isWallSliding) return;

        if (rb.linearVelocity.y < -wallSlideSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -wallSlideSpeed);
        }
    }

    private void ApplyBetterGravity()
    {
        Vector2 velocity = rb.linearVelocity;
        float gravity = Physics2D.gravity.y * rb.gravityScale;

        if (isGrounded && velocity.y <= 0f)
        {
            velocity.y = 0f;
            rb.linearVelocity = velocity;
            return;
        }

        if (velocity.y < 0f)
        {
            velocity.y += gravity * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            velocity.y += gravity * (lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime;
        }

        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        rb.linearVelocity = velocity;
    }

    private bool IsGrounded()
    {
        Bounds b = boxCollider.bounds;
        Vector2 checkSize = new Vector2(b.size.x * 0.95f, groundCheckExtra);
        Vector2 checkPos = new Vector2(b.center.x, b.min.y - (groundCheckExtra * 0.5f));
        return Physics2D.OverlapBox(checkPos, checkSize, 0f, EffectiveGroundMask);
    }

    private bool IsTouchingWall(float direction)
    {
        Bounds b = boxCollider.bounds;
        float sign = Mathf.Sign(direction);

        Vector2 sideOriginTop = new Vector2(b.center.x + (b.extents.x * sign), b.min.y + (b.size.y * wallCheckTopRatio));
        Vector2 sideOriginMid = new Vector2(b.center.x + (b.extents.x * sign), b.min.y + (b.size.y * wallCheckMidRatio));
        Vector2 rayDirection = new Vector2(sign, 0f);

        bool hitTop = Physics2D.Raycast(sideOriginTop, rayDirection, wallCheckDistance, EffectiveGroundMask);
        bool hitMid = Physics2D.Raycast(sideOriginMid, rayDirection, wallCheckDistance, EffectiveGroundMask);
        return hitTop || hitMid;
    }

    private int GetTouchingWallDirection()
    {
        if (IsTouchingWall(1f)) return 1;
        if (IsTouchingWall(-1f)) return -1;
        return 0;
    }

    private void UpdateAnimationState()
    {
        if (animator == null || !animator.enabled) return;

        string targetAnimation = (!isGrounded && Mathf.Abs(rb.linearVelocity.y) > 0.05f)
            ? ANIM_JUMP
            : Mathf.Abs(rb.linearVelocity.x) > 0.05f ? ANIM_WALK : ANIM_IDLE;

        if (currentAnimation == targetAnimation) return;

        animator.CrossFade(targetAnimation, 0.04f);
        currentAnimation = targetAnimation;
    }

    private void SetupAdaptiveTint()
    {
        tintMaxSamples = Mathf.Max(4, tintMaxSamples);
        tintSampleBuffer = new Collider2D[tintMaxSamples];
        tintContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        tintContactFilter.SetLayerMask(tintSampleLayers.value == 0 ? Physics2D.DefaultRaycastLayers : tintSampleLayers.value);
    }

    private void UpdateAdaptiveTint()
    {
        if (!enableAdaptiveTint)
        {
            targetSpriteColor = baseSpriteColor;
            ApplyTintSmoothing();
            return;
        }

        tintTimer -= Time.deltaTime;
        if (tintTimer <= 0f)
        {
            targetSpriteColor = CalculateAdaptiveTintColor();
            tintTimer = Mathf.Max(0.02f, tintUpdateInterval);
        }

        ApplyTintSmoothing();
    }

    private void ApplyTintSmoothing()
    {
        float t = 1f - Mathf.Exp(-Mathf.Max(0f, tintSmoothing) * Time.deltaTime);
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, targetSpriteColor, t);
    }

    private Color CalculateAdaptiveTintColor()
    {
        if (tintSampleRadius <= 0.01f || tintInfluence <= 0f)
        {
            return baseSpriteColor;
        }

        int desiredBufferSize = Mathf.Max(4, tintMaxSamples);
        if (tintSampleBuffer == null || tintSampleBuffer.Length != desiredBufferSize)
        {
            tintSampleBuffer = new Collider2D[desiredBufferSize];
        }

        tintContactFilter.SetLayerMask(tintSampleLayers.value == 0 ? Physics2D.DefaultRaycastLayers : tintSampleLayers.value);
        int hits = Physics2D.OverlapCircle((Vector2)transform.position, tintSampleRadius, tintContactFilter, tintSampleBuffer);

        Vector3 weightedColor = Vector3.zero;
        float totalWeight = 0f;

        for (int i = 0; i < hits; i++)
        {
            Collider2D hit = tintSampleBuffer[i];
            if (hit == null || hit.attachedRigidbody == rb || hit.transform == transform) continue;

            SpriteRenderer nearbySprite = hit.GetComponent<SpriteRenderer>() ?? hit.GetComponentInParent<SpriteRenderer>();
            if (nearbySprite == null || nearbySprite == spriteRenderer || !nearbySprite.enabled) continue;

            float distance = Vector2.Distance(transform.position, hit.ClosestPoint(transform.position));
            float distanceWeight = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, tintSampleRadius));
            float alphaWeight = Mathf.Clamp01(nearbySprite.color.a);
            if (distanceWeight <= 0f || alphaWeight <= 0.01f) continue;

            float weight = distanceWeight * alphaWeight;
            Color c = nearbySprite.color;
            weightedColor += new Vector3(c.r, c.g, c.b) * weight;
            totalWeight += weight;
        }

        Color sampledColor = totalWeight > 0.0001f
            ? new Color(weightedColor.x / totalWeight, weightedColor.y / totalWeight, weightedColor.z / totalWeight, 1f)
            : tintFallbackColor;

        sampledColor = Color.Lerp(sampledColor, RenderSettings.ambientLight, Mathf.Clamp01(ambientInfluence));
        Color pastelColor = ToPastel(sampledColor, pastelAmount);

        Color finalColor = Color.Lerp(baseSpriteColor, pastelColor, Mathf.Clamp01(tintInfluence));
        finalColor.a = baseSpriteColor.a;
        return finalColor;
    }

    private static Color ToPastel(Color sourceColor, float strength)
    {
        Color.RGBToHSV(sourceColor, out float h, out float s, out float v);
        strength = Mathf.Clamp01(strength);

        s = Mathf.Lerp(s, s * 0.35f, strength);
        v = Mathf.Lerp(v, Mathf.Clamp01(v + 0.25f), strength);

        Color pastel = Color.HSVToRGB(h, Mathf.Clamp01(s), Mathf.Clamp01(v));
        float luminance = (sourceColor.r * 0.2126f) + (sourceColor.g * 0.7152f) + (sourceColor.b * 0.0722f);
        float luminanceBoost = Mathf.Lerp(0.92f, 1.08f, luminance);

        pastel.r = Mathf.Clamp01(pastel.r * luminanceBoost);
        pastel.g = Mathf.Clamp01(pastel.g * luminanceBoost);
        pastel.b = Mathf.Clamp01(pastel.b * luminanceBoost);
        return pastel;
    }

    private void OnCollisionEnter2D(Collision2D collision) => TryProcessEnemyContact(collision.collider);

    private void OnTriggerEnter2D(Collider2D other) => TryProcessEnemyContact(other);

    public void ApplyDamageFeedback(Vector2 awayDirection, float knockbackForce, float upwardForceMin, float flashDuration)
    {
        if (awayDirection.sqrMagnitude <= 0.0001f)
        {
            awayDirection = Vector2.up;
        }

        awayDirection.Normalize();
        Vector2 knockbackVelocity = awayDirection * Mathf.Max(0f, knockbackForce);
        knockbackVelocity.y = Mathf.Max(knockbackVelocity.y, Mathf.Max(0f, upwardForceMin));

        rb.linearVelocity = new Vector2(knockbackVelocity.x, Mathf.Max(rb.linearVelocity.y, knockbackVelocity.y));

        damageFlashTimer = Mathf.Max(damageFlashTimer, Mathf.Max(0f, flashDuration));
        StartInvulnerabilityPhase(Mathf.Max(0f, contactInvulnerabilityDuration));
        PlaySfx(hurtSfx, hurtSfxVolume);
    }

    private void UpdateDamageFlash()
    {
        if (damageFlashTimer <= 0f) return;

        damageFlashTimer = Mathf.Max(0f, damageFlashTimer - Time.deltaTime);
        Color flashColor = damageFlashColor;
        flashColor.a = spriteRenderer.color.a;
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, flashColor, Mathf.Clamp01(damageFlashStrength));
    }

    private void TryProcessEnemyContact(Collider2D other)
    {
        if (!detectEnemyContacts || other == null || invulnerabilityTimer > 0f) return;
        if (!IsEnemyContact(other)) return;

        CacheGameManagerReference();
        if (gameManager == null) return;

        gameManager.TryDamagePlayer(other.transform.position, contactKnockbackForce, contactKnockbackUpward, contactFlashDuration);
    }

    private bool IsEnemyContact(Collider2D other)
    {
        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null || enemy.isDead) return false;
        return enemy.ShouldDamagePlayerFromCollider(other);
    }

    private void CacheGameManagerReference()
    {
        if (gameManager != null) return;
        gameManager = FindFirstObjectByType<GameManager>();
    }

    private void TryShoot()
    {
        if (!enableShooting || shootAction == null || !shootAction.WasPressedThisFrame() || Time.time < nextShootTime) return;

        nextShootTime = Time.time + Mathf.Max(0.02f, shootCooldown);
        SpawnProjectile(GetShootDirection());
        PlaySfx(projectileShootSfx, projectileShootSfxVolume);
    }

    private Vector2 GetShootDirection()
    {
        if (TryGetPointerWorldPosition(out Vector3 pointerWorldPosition))
        {
            Vector2 toPointer = (Vector2)(pointerWorldPosition - transform.position);
            if (toPointer.sqrMagnitude > 0.0001f)
            {
                return toPointer.normalized;
            }
        }

        return new Vector2(facingDirection == 0 ? 1f : facingDirection, 0f);
    }

    private bool TryGetPointerWorldPosition(out Vector3 pointerWorldPosition)
    {
        pointerWorldPosition = default;

        if (Mouse.current == null) return false;

        Camera activeCamera = Camera.main;
        if (activeCamera == null) return false;

        Vector2 pointerScreen = Mouse.current.position.ReadValue();
        if (!activeCamera.pixelRect.Contains(pointerScreen)) return false;

        float cameraDistance = Mathf.Abs(activeCamera.transform.position.z - transform.position.z);
        pointerWorldPosition = activeCamera.ScreenToWorldPoint(new Vector3(pointerScreen.x, pointerScreen.y, cameraDistance));
        pointerWorldPosition.z = transform.position.z;
        return true;
    }

    private void SpawnProjectile(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = new Vector2(facingDirection == 0 ? 1f : facingDirection, 0f);
        }

        direction.Normalize();
        Vector3 spawnOffset = (Vector3)(direction * shootSpawnOffset.x) + new Vector3(0f, shootSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;

        GameObject projectileObj = new GameObject("PlayerProjectile");
        projectileObj.transform.position = spawnPosition;

        SpriteRenderer projectileRenderer = projectileObj.AddComponent<SpriteRenderer>();
        projectileRenderer.sprite = projectileSprite != null ? projectileSprite : spriteRenderer.sprite;
        projectileRenderer.color = projectileColor;
        projectileObj.transform.localScale = Vector3.one * Mathf.Max(0.05f, projectileScale);
        projectileRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        projectileRenderer.sortingOrder = spriteRenderer.sortingOrder + projectileSortingOrderOffset;

        CircleCollider2D projectileCollider = projectileObj.AddComponent<CircleCollider2D>();
        projectileCollider.isTrigger = true;
        projectileCollider.radius = Mathf.Max(0.01f, projectileColliderRadius);

        Rigidbody2D projectileRb = projectileObj.AddComponent<Rigidbody2D>();
        projectileRb.bodyType = RigidbodyType2D.Kinematic;
        projectileRb.gravityScale = 0f;
        projectileRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        projectileRb.linearVelocity = direction * Mathf.Max(0.1f, projectileSpeed);

        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponents<Collider2D>();
        }

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D ownCollider = playerColliders[i];
            if (ownCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, ownCollider, true);
            }
        }

        PlayerProjectile projectile = projectileObj.AddComponent<PlayerProjectile>();
        projectile.Initialize(Mathf.Max(1, projectileDamage), Mathf.Max(0.05f, projectileLifetime));
    }

    private void StartInvulnerabilityPhase(float duration)
    {
        if (duration <= 0f) return;

        bool wasNotInvulnerable = invulnerabilityTimer <= 0f;
        invulnerabilityTimer = Mathf.Max(invulnerabilityTimer, duration);
        if (!wasNotInvulnerable) return;

        invulnerabilityRefreshTimer = 0f;
        RefreshIgnoredEnemyColliders();
    }

    private void UpdateInvulnerability()
    {
        if (invulnerabilityTimer <= 0f) return;

        invulnerabilityTimer = Mathf.Max(0f, invulnerabilityTimer - Time.deltaTime);
        invulnerabilityRefreshTimer -= Time.deltaTime;

        if (invulnerabilityRefreshTimer <= 0f)
        {
            RefreshIgnoredEnemyColliders();
        }

        if (invulnerabilityTimer <= 0f)
        {
            EndInvulnerabilityPhase();
        }
    }

    private void RefreshIgnoredEnemyColliders()
    {
        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponents<Collider2D>();
        }

        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            Collider2D[] enemyColliders = enemies[i].GetComponents<Collider2D>();
            for (int j = 0; j < enemyColliders.Length; j++)
            {
                Collider2D enemyCollider = enemyColliders[j];
                if (enemyCollider == null) continue;

                for (int k = 0; k < playerColliders.Length; k++)
                {
                    Collider2D playerCollider = playerColliders[k];
                    if (playerCollider != null)
                    {
                        Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
                    }
                }

                if (!ignoredEnemyColliders.Contains(enemyCollider))
                {
                    ignoredEnemyColliders.Add(enemyCollider);
                }
            }
        }

        invulnerabilityRefreshTimer = Mathf.Max(0.02f, invulnerabilityRefreshInterval);
    }

    private void EndInvulnerabilityPhase()
    {
        if (playerColliders == null || playerColliders.Length == 0)
        {
            playerColliders = GetComponents<Collider2D>();
        }

        for (int i = 0; i < ignoredEnemyColliders.Count; i++)
        {
            Collider2D enemyCollider = ignoredEnemyColliders[i];
            if (enemyCollider == null) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider2D playerCollider = playerColliders[j];
                if (playerCollider != null)
                {
                    Physics2D.IgnoreCollision(playerCollider, enemyCollider, false);
                }
            }
        }

        ignoredEnemyColliders.Clear();
        invulnerabilityTimer = 0f;
        invulnerabilityRefreshTimer = 0f;
    }

    private void ApplyInvulnerabilityBlink()
    {
        if (!showInvulnerabilityBlink || invulnerabilityTimer <= 0f) return;

        float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, invulnerabilityBlinkSpeed)));
        float blend = pulse * Mathf.Clamp01(invulnerabilityBlinkStrength);

        Color blinkColor = invulnerabilityBlinkColor;
        blinkColor.a = spriteRenderer.color.a;
        spriteRenderer.color = Color.Lerp(spriteRenderer.color, blinkColor, blend);
    }

    public void PlayCustomSfx(AudioClip clip, float volume = 1f)
    {
        PlaySfx(clip, volume);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
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

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureAudioSource();
        if (projectileSprite == null)
        {
            projectileSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Attacks/powerOrb.png");
        }
    }
#endif
}
