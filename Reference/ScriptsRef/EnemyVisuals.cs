using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyMovement))]
public class EnemyVisuals : MonoBehaviour
{
    [Header("Visual Configuration")]
    [SerializeField] private string movingStateName = "MOVING";
    [SerializeField] private string deathStateName = "OVER";
    
    [Tooltip("Solo usamos esto para saber cuánto dura la animación antes de destruir el objeto.")]
    [SerializeField] private AnimationClip deathClip; 

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private EnemyMovement enemyMovement;

    private int movingStateHash;
    private int deathStateHash;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        enemyMovement = GetComponent<EnemyMovement>();

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
        enemyMovement.OnDeath += HandleDeathAnimation;
    }

    private void OnDisable()
    {
        enemyMovement.OnDeath -= HandleDeathAnimation;
    }

    private void Update()
    {
        if (enemyMovement.isDead) return;

        // Flip the sprite based on the Movement script's Rigidbody velocity
        float velX = enemyMovement.rb.linearVelocity.x;
        if (velX > 0.01f) spriteRenderer.flipX = true;
        else if (velX < -0.01f) spriteRenderer.flipX = false;
    }

    private void HandleDeathAnimation()
    {
        // Tell the movement script how long to wait before destroying the object
        if (deathClip != null)
        {
            enemyMovement.deathAnimationLength = deathClip.length;
        }

        // Play the OVER state directly!
        if (animator.HasState(0, deathStateHash))
        {
            animator.Play(deathStateHash, 0, 0f);
        }
        else
        {
            Debug.LogWarning($"El estado '{deathStateName}' no existe en el Animator.", this);
        }
    }
}