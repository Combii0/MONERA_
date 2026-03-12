using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private PlayerMovement playerMovement;

    private string currentAnimation;

    private const string ANIM_IDLE = "Idle";  
    private const string ANIM_WALK = "Moving"; 
    private const string ANIM_JUMP = "Jump";
    private const string ANIM_SLIDE = "Sliding"; // <-- Nuestra nueva animación

    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        UpdateFacingDirection();
        UpdateAnimationState();
    }

    private void UpdateFacingDirection()
    {
        // El personaje solo mira en la dirección de su movimiento
        if (playerMovement.MoveInput > 0.01f) spriteRenderer.flipX = false;
        else if (playerMovement.MoveInput < -0.01f) spriteRenderer.flipX = true;
    }

    private void UpdateAnimationState()
    {
        if (animator == null || !animator.enabled) return;

        string targetAnimation;

        // 1. Prioridad alta: ¿Está deslizando por una pared?
        if (playerMovement.IsWallSliding)
        {
            targetAnimation = ANIM_SLIDE;
        }
        // 2. Si no está en la pared, pero está en el aire:
        else if (!playerMovement.IsGrounded)
        {
            targetAnimation = ANIM_JUMP;
        }
        // 3. Si está en el suelo:
        else
        {
            targetAnimation = Mathf.Abs(rb.linearVelocity.x) > 0.05f ? ANIM_WALK : ANIM_IDLE;
        }

        if (currentAnimation == targetAnimation) return;

        animator.Play(targetAnimation);
        currentAnimation = targetAnimation;
    }
}