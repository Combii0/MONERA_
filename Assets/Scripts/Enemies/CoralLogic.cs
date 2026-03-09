using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class CoralLogic : MonoBehaviour
{
    [Header("Vertical Movement Settings")]
    [SerializeField] private float coralTilesToMove = 3f;
    [SerializeField] private float coralTileSize = 1f;
    [SerializeField] private float coralMoveSpeed = 0.8f;
    [SerializeField] private float coralSmoothTime = 0.2f;
    [SerializeField] private float coralArrivalDistance = 0.02f;
    [SerializeField] private float coralPauseAtEnds = 0.08f;
    public float delayStartMovingCoralTime = 0f;

    [Header("Physics Stabilization")]
    [SerializeField] private bool forceKinematicBody = true;
    [SerializeField] private bool lockPositionDuringStartDelay = true;

    private Rigidbody2D rb;
    private EnemyHealth health;
    
    private float coralBaseX;
    private float coralBaseY;
    private bool coralMovingUp = true;
    private float coralVelocityY;
    private float coralPauseTimer;
    private float coralStartDelayTimer;
    private Collider2D[] cachedEnemyColliders;
    private bool isPrimaryController = true;

    private void Awake()
    {
        isPrimaryController = EnsureSingleCoralController();
        if (!isPrimaryController)
        {
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        ResolvePrimaryHealthComponent();

        ConfigureRigidbody();
        coralBaseX = rb.position.x;
        coralBaseY = rb.position.y;
        coralStartDelayTimer = Mathf.Max(0f, delayStartMovingCoralTime);
        
        IgnoreAllTileCollisionsForCoral();
    }

    private void FixedUpdate()
    {
        if (!isPrimaryController) return;

        // Stop moving if the coral is dead
        if (health.isDead) return;

        UpdateCoralMovement();
    }

    private void UpdateCoralMovement()
    {
        if (coralStartDelayTimer > 0f)
        {
            coralStartDelayTimer = Mathf.Max(0f, coralStartDelayTimer - Time.fixedDeltaTime);
            if (lockPositionDuringStartDelay)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.MovePosition(new Vector2(coralBaseX, coralBaseY));
            }
            return;
        }

        if (coralPauseTimer > 0f)
        {
            coralPauseTimer = Mathf.Max(0f, coralPauseTimer - Time.fixedDeltaTime);
            return;
        }

        float travelDistance = Mathf.Max(0f, coralTilesToMove) * Mathf.Max(0.01f, coralTileSize);
        if (travelDistance <= 0.0001f) return;

        float targetY = coralMovingUp ? (coralBaseY + travelDistance) : coralBaseY;
        float nextY = Mathf.SmoothDamp(
            rb.position.y,
            targetY,
            ref coralVelocityY,
            Mathf.Max(0.02f, coralSmoothTime),
            Mathf.Max(0.05f, coralMoveSpeed),
            Time.fixedDeltaTime
        );
        
        rb.MovePosition(new Vector2(coralBaseX, nextY));

        if (Mathf.Abs(nextY - targetY) <= Mathf.Max(0.001f, coralArrivalDistance))
        {
            rb.MovePosition(new Vector2(coralBaseX, targetY));
            coralVelocityY = 0f;
            coralMovingUp = !coralMovingUp;
            coralPauseTimer = Mathf.Max(0f, coralPauseAtEnds);
        }
    }

    private void ConfigureRigidbody()
    {
        if (rb == null) return;

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (forceKinematicBody)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private bool EnsureSingleCoralController()
    {
        CoralLogic[] controllers = GetComponents<CoralLogic>();
        if (controllers == null || controllers.Length <= 1) return true;

        CoralLogic primary = controllers[0];
        if (primary != this) return false;

        for (int i = 1; i < controllers.Length; i++)
        {
            if (controllers[i] == null) continue;
            controllers[i].enabled = false;
        }

        Debug.LogWarning($"CoralLogic: {name} tenia componentes duplicados. Se desactivaron los extras.", this);
        return true;
    }

    private void ResolvePrimaryHealthComponent()
    {
        EnemyHealth[] allHealth = GetComponents<EnemyHealth>();
        if (allHealth == null || allHealth.Length == 0)
        {
            health = gameObject.AddComponent<EnemyHealth>();
            return;
        }

        health = allHealth[0];
        for (int i = 1; i < allHealth.Length; i++)
        {
            if (allHealth[i] == null) continue;
            allHealth[i].enabled = false;
        }
    }

    private void IgnoreAllTileCollisionsForCoral()
    {
        cachedEnemyColliders = GetComponents<Collider2D>();
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
}
