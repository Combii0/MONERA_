using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Seguimiento")]
    [SerializeField] private Transform target;
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float stoppingDistance = 0.3f;

    [Header("Detección y Aggro")]
    public bool useDetectionRadius = true;
    [SerializeField] private float detectionRadius = 8f; 
    [Tooltip("Tiempo en segundos que el jugador debe estar fuera del radio para que el enemigo se rinda.")]
    [SerializeField] private float aggroLoseTime = 5f; 

    [Header("Coral Vertical")]
    public bool isCoral = false;
    [SerializeField] private float coralTilesToMove = 3f;
    [SerializeField] private float coralTileSize = 1f;
    [SerializeField] private float coralMoveSpeed = 0.8f;

    [Header("Coral Azul")]
    public bool isBlueCoral = false;
    [SerializeField] private float circularFollowDelay = 1.5f; 
    [SerializeField] private float circularOrbitSpeed = 1f;    
    [SerializeField] private Vector2 radiusRange = new Vector2(2f, 5f); 
    [Tooltip("Qué tan rápido se agranda o encoge el círculo al cambiar de radio.")]
    [SerializeField] private float radiusTransitionSpeed = 1.5f; 
    [Tooltip("Qué tan suave sigue al jugador. Números bajos = más elástico/suave.")]
    [SerializeField] private float centerFollowSmoothness = 2.5f; // <-- ¡NUEVO! Suavidad del vuelo

    [Header("Vida")]
    [SerializeField] private int maxHealth = 4;
    [SerializeField] private float hitCooldown = 0.08f;
    [SerializeField] private float destroyDelayAfterDeath = 0f;
    [SerializeField] private string[] damageTags = { "PlayerAttack", "Projectile" };
    [SerializeField] private bool destroyDamageSourceOnHit = false;

    [Header("UI")]
    [SerializeField] private EnemyHealthBar healthBarPrefab;
    [SerializeField] private float healthBarYOffset = 0.5f;

    // --- EVENTS ---
    public event Action OnDeath;

    // --- CACHES & PUBLIC PROPERTIES ---
    public Rigidbody2D rb { get; private set; }
    public bool isDead { get; private set; }
    [HideInInspector] public float deathAnimationLength = 0.35f; 

    private Collider2D[] colliders;
    private EnemyHealthBar activeHealthBar;
    private static Transform healthBarContainer;

    // State Variables (General)
    private int currentHealth;
    private float nextAllowedHitTime;
    private Vector2 initialPosition; 
    
    // State Variables (Aggro)
    private bool isAggroed = false;
    private float timeOutOfRange = 0f;

    // State Variables (Coral Vertical)
    private float coralBaseY;

    // State Variables (Coral Azul)
    private Queue<float> targetHistoryX = new Queue<float>();
    private float currentCircularRadius;
    private float targetCircularRadius; 
    private float currentOrbitAngle;
    private Vector2 currentBlueCoralCenter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();
        
        rb.gravityScale = 0f;
        currentHealth = Mathf.Max(1, maxHealth);
        
        initialPosition = rb.position;
        coralBaseY = rb.position.y;
        currentBlueCoralCenter = rb.position;
        
        targetCircularRadius = UnityEngine.Random.Range(radiusRange.x, radiusRange.y);
        currentCircularRadius = targetCircularRadius;

        if (target == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player"); 
            if (playerObj != null) target = playerObj.transform;
        }
    }

    private void Start()
    {
        if (healthBarContainer == null)
        {
            GameObject containerObj = new GameObject("EnemyHealthBars");
            healthBarContainer = containerObj.transform;
        }

        if (healthBarPrefab != null)
        {
            activeHealthBar = Instantiate(healthBarPrefab, transform.position, Quaternion.identity, healthBarContainer);
            
            float finalYOffset = healthBarYOffset;
            if (colliders != null && colliders.Length > 0)
            {
                finalYOffset += colliders[0].bounds.extents.y;
            }

            activeHealthBar.Setup(transform, finalYOffset);
            activeHealthBar.UpdateHealth(currentHealth, maxHealth);
        }
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

        if (target == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // --- SISTEMA DE AGGRO Y DETECCIÓN ---
        if (useDetectionRadius)
        {
            float distanceToPlayer = Vector2.Distance(rb.position, target.position);
            
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
                    targetHistoryX.Clear(); 
                }
            }
        }
        else
        {
            isAggroed = true;
        }

        // --- LÓGICA DE RETORNO A CASA ---
        if (!isAggroed)
        {
            Vector2 toInitial = initialPosition - rb.position;
            
            if (toInitial.magnitude > 0.1f)
            {
                rb.linearVelocity = toInitial.normalized * speed;
                currentBlueCoralCenter = rb.position; 
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = initialPosition;
            }
            
            return; 
        }

        // --- LÓGICA DE PERSECUCIÓN ---

        if (isBlueCoral)
        {
            UpdateBlueCoralMovement();
            return;
        }

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - currentPos;
        
        if (toTarget.magnitude <= stoppingDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = toTarget.normalized * speed;
    }

    private void UpdateBlueCoralMovement()
    {
        if (target == null) return;

        targetHistoryX.Enqueue(target.position.x);

        int framesToDelay = Mathf.RoundToInt(circularFollowDelay / Time.fixedDeltaTime);
        float targetCenterX = target.position.x;

        if (targetHistoryX.Count > framesToDelay)
        {
            targetCenterX = targetHistoryX.Dequeue();
        }
        else if (targetHistoryX.Count > 0)
        {
            targetCenterX = targetHistoryX.Peek();
        }

        float targetCenterY = target.position.y;

        Vector2 targetCenter = new Vector2(targetCenterX, targetCenterY);
        
        // --- EL ARREGLO DE SUAVIDAD PARA EL VUELO ---
        // Lerp suaviza los movimientos bruscos del jugador. Absorbe el impacto de los cambios de dirección.
        currentBlueCoralCenter = Vector2.Lerp(currentBlueCoralCenter, targetCenter, centerFollowSmoothness * Time.fixedDeltaTime);

        // Lerp para el cambio de radio (que ya teníamos)
        currentCircularRadius = Mathf.Lerp(currentCircularRadius, targetCircularRadius, radiusTransitionSpeed * Time.fixedDeltaTime);

        currentOrbitAngle += circularOrbitSpeed * Time.fixedDeltaTime;

        float nextX = currentBlueCoralCenter.x + Mathf.Cos(currentOrbitAngle) * currentCircularRadius;
        float nextY = currentBlueCoralCenter.y + Mathf.Sin(currentOrbitAngle) * currentCircularRadius;

        rb.MovePosition(new Vector2(nextX, nextY));
        rb.linearVelocity = Vector2.zero;

        if (currentOrbitAngle >= Mathf.PI * 2f)
        {
            currentOrbitAngle -= Mathf.PI * 2f;
            targetCircularRadius = UnityEngine.Random.Range(radiusRange.x, radiusRange.y);
        }
    }

    private void UpdateCoralMovement()
    {
        float travelDistance = Mathf.Max(0f, coralTilesToMove) * Mathf.Max(0.01f, coralTileSize);
        if (travelDistance <= 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float sineWave = (Mathf.Sin(Time.time * coralMoveSpeed) + 1f) * 0.5f;
        float nextY = coralBaseY + (travelDistance * sineWave);
        
        rb.MovePosition(new Vector2(rb.position.x, nextY));
        rb.linearVelocity = Vector2.zero;
    }

    public void TakeDamage(int amount = 1)
    {
        if (isDead || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        
        if (activeHealthBar != null) activeHealthBar.UpdateHealth(currentHealth, maxHealth);
        
        if (currentHealth <= 0) Die();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryReceiveTagDamage(other.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => TryReceiveTagDamage(collision.gameObject);

    private void TryReceiveTagDamage(GameObject damageSource)
    {
        if (isDead || damageSource == null || damageTags == null) return;
        if (Time.time < nextAllowedHitTime) return;

        for (int i = 0; i < damageTags.Length; i++)
        {
            string tagName = damageTags[i];
            if (string.IsNullOrEmpty(tagName)) continue;
            
            if (!damageSource.CompareTag(tagName)) continue;

            TakeDamage(1);
            nextAllowedHitTime = Time.time + Mathf.Max(0f, hitCooldown);

            if (destroyDamageSourceOnHit) Destroy(damageSource);
            return;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        rb.linearVelocity = Vector2.zero;

        if (activeHealthBar != null) Destroy(activeHealthBar.gameObject);

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        OnDeath?.Invoke(); 

        StartCoroutine(DestroyAfterDeath(deathAnimationLength + Mathf.Max(0f, destroyDelayAfterDeath)));
    }

    private IEnumerator DestroyAfterDeath(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (useDetectionRadius)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}