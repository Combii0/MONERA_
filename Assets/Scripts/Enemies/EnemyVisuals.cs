using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))] // Added this so Unity knows it needs the health script
public class EnemyVisuals : MonoBehaviour
{
    [Header("Visual Configuration")]
    [SerializeField] private string movingStateName = "MOVING";
    [SerializeField] private string deathStateName = "OVER";
    
    [Tooltip("Solo usamos esto para saber cuánto dura la animación antes de destruir el objeto.")]
    [SerializeField] private AnimationClip deathClip; 

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private EnemyHealth enemyHealth;

    private int movingStateHash;
    private int deathStateHash;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        enemyHealth = GetComponent<EnemyHealth>(); // Swapped to the new modular system

        // Cache the string names into hashes for performance
        movingStateHash = Animator.StringToHash(movingStateName);
        deathStateHash = Animator.StringToHash(deathStateName);
    }

    private void Start()
    {
        // Play the default moving state
        if (animator.HasState(0, movingStateHash))
        {
            animator.Play(movingStateHash, 0, 0f);
        }
        else
        {
            Debug.LogWarning($"El estado '{movingStateName}' no existe en el Animator.", this);
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleDeathAnimation;
        }
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleDeathAnimation;
        }
    }

    private void Update()
    {
        if (enemyHealth == null || enemyHealth.isDead) return;

        // Flip the sprite based on the velocity (reading from the rb exposed in EnemyHealth)
        if (enemyHealth.rb != null)
        {
            float velX = enemyHealth.rb.linearVelocity.x;
            if (velX > 0.01f) spriteRenderer.flipX = true;
            else if (velX < -0.01f) spriteRenderer.flipX = false;
        }
    }

    private void HandleDeathAnimation()
    {
        // Play the OVER state directly!
        if (animator.HasState(0, deathStateHash))
        {
            animator.Play(deathStateHash, 0, 0f);
        }
        else
        {
            Debug.LogWarning($"El estado '{deathStateName}' no existe en el Animator.", this);
        }

        // EnemyVisuals now handles destroying the object after the animation plays
        float destroyDelay = deathClip != null ? deathClip.length : 0.35f;
        Destroy(gameObject, destroyDelay);
    }
}