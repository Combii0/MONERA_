using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
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

    [Header("Audio Player")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField, Range(0f, 1f), HideInInspector] private float jumpSfxVolume = 1f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    private InputAction moveAction;
    private InputAction jumpAction;

    private float moveInput;
    private bool jumpHeld;
    private bool isGrounded;
    private bool isWallSliding;
    private int touchingWallDirection;
    private int extraJumpsRemaining;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float wallJumpInputLockTimer;

    private int facingDirection = 1;
    private PhysicsMaterial2D runtimePhysicsMaterial;

    private const string PLAYER_RUNTIME_PHYSICS_MATERIAL = "Player_RuntimePhysics";
    private int EffectiveGroundMask => groundLayer.value == 0 ? Physics2D.DefaultRaycastLayers : groundLayer.value;

    // Propiedades públicas para que los otros scripts lean el estado
    public float MoveInput => moveInput;
    public bool IsGrounded => isGrounded;
    public int FacingDirection => facingDirection;
    public bool IsWallSliding => isWallSliding; // <-- Aquí está la variable expuesta para la animación

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();

        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        
        EnsureFrictionlessContactMaterial();
        EnsureAudioSource();
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
        moveAction?.Enable();
        jumpAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        jumpAction?.Dispose();
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
    }

    private void Update()
    {
        ReadInput();
        UpdateGroundAndWallState();
        UpdateFacingDirection();
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
        isGrounded = CheckIfGrounded();
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
        if (moveInput > 0.01f) facingDirection = 1;
        else if (moveInput < -0.01f) facingDirection = -1;
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

    private bool CheckIfGrounded()
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
}