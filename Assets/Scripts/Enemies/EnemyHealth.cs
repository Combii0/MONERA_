using UnityEngine;
using System;

[RequireComponent(typeof(Collider2D))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 4;
    [SerializeField] private float hitCooldown = 0.08f;
    [SerializeField] private string[] damageTags = { "PlayerAttack", "Projectile"};

    [Header("Health Bar")]
    [Tooltip("Arrastra aquí el prefab normal (GameObject).")]
    [SerializeField] private GameObject healthBarPrefab; 
    [SerializeField] private float healthBarYOffset = 0.5f;

    [Header("Feedback")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip hitSfx;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.9f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.16f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f)] private float damageFlashStrength = 0.9f;

    public event Action OnDeath;
    public event Action OnDamaged;

    public bool isDead { get; private set; }
    public bool isInvulnerable = false;
    public Rigidbody2D rb { get; private set; }

    private int currentHealth;
    private float nextAllowedHitTime;
    private SpriteRenderer spriteRenderer;
    private Color baseSpriteColor;
    private float damageFlashTimer;
    
    private GameObject activeHealthBar;
    private Transform fillTransform;
    private static Transform healthBarsContainer;
    
    // Auto-detect the original size
    private float initialFillScaleX;
    private float initialFillPosX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) baseSpriteColor = spriteRenderer.color;
        
        currentHealth = maxHealth;
    }

    private void Start()
    {
        SpawnHealthBar();
    }

    private void Update()
    {
        UpdateDamageFlash();
        UpdateHealthBarPosition();
    }

    private void SpawnHealthBar()
    {
        if (healthBarPrefab == null) return;

        if (healthBarsContainer == null)
        {
            GameObject containerObj = GameObject.Find("HealthBars");
            if (containerObj == null) containerObj = new GameObject("HealthBars");
            healthBarsContainer = containerObj.transform;
        }

        activeHealthBar = Instantiate(healthBarPrefab, Vector3.zero, Quaternion.identity, healthBarsContainer);
        
        fillTransform = FindChildRecursive(activeHealthBar.transform, "Fill");
        
        if (fillTransform != null)
        {
            initialFillScaleX = fillTransform.localScale.x;
            initialFillPosX = fillTransform.localPosition.x;
        }
        else
        {
            Debug.LogWarning("No se encontró 'Fill' en el prefab de la barra de vida.", this);
        }

        UpdateHealthBarVisuals();
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name == targetName) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private void UpdateHealthBarPosition()
    {
        if (activeHealthBar != null && !isDead)
        {
            float finalYOffset = healthBarYOffset;
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) finalYOffset += col.bounds.extents.y;

            activeHealthBar.transform.position = transform.position + new Vector3(0, finalYOffset, 0);
        }
    }

    private void UpdateHealthBarVisuals()
    {
        if (fillTransform == null) return;

        float ratio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
        
        fillTransform.localScale = new Vector3(initialFillScaleX * ratio, fillTransform.localScale.y, fillTransform.localScale.z);
        fillTransform.localPosition = new Vector3(initialFillPosX - (initialFillScaleX * (1f - ratio) * 0.5f), fillTransform.localPosition.y, fillTransform.localPosition.z);
        fillTransform.gameObject.SetActive(ratio > 0f);
    }

    public void TakeDamage(int amount = 1)
    {
        if (isDead || amount <= 0 || isInvulnerable) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        TriggerDamageFeedback();
        
        UpdateHealthBarVisuals();
        OnDamaged?.Invoke();
        
        if (currentHealth <= 0) Die();
    }

    private void OnTriggerEnter2D(Collider2D other) => TryReceiveTagDamage(other.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => TryReceiveTagDamage(collision.gameObject);

    private void TryReceiveTagDamage(GameObject damageSource)
    {
        if (isDead || damageSource == null) return;
        if (Time.time < nextAllowedHitTime) return;

        for (int i = 0; i < damageTags.Length; i++)
        {
            if (MatchesTagSafe(damageSource, damageTags[i]))
            {
                TakeDamage(1);
                nextAllowedHitTime = Time.time + hitCooldown;
                return;
            }
        }
    }

    private static bool MatchesTagSafe(GameObject gameObjectToCheck, string tagName)
    {
        if (gameObjectToCheck == null || string.IsNullOrWhiteSpace(tagName)) return false;

        try
        {
            return gameObjectToCheck.CompareTag(tagName);
        }
        catch (UnityException)
        {
            // If a tag is missing in TagManager, we just ignore it instead of spamming errors.
            return false;
        }
    }

    private void TriggerDamageFeedback()
    {
        damageFlashTimer = 0.12f; 
        if (sfxSource != null && hitSfx != null) sfxSource.PlayOneShot(hitSfx, hitSfxVolume);
    }

    private void UpdateDamageFlash()
    {
        if (spriteRenderer == null) return;
        if (damageFlashTimer > 0f)
        {
            damageFlashTimer -= Time.deltaTime;
            spriteRenderer.color = Color.Lerp(baseSpriteColor, damageFlashColor, damageFlashStrength);
        }
        else
        {
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, baseSpriteColor, Time.deltaTime * 18f);
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        
        if (rb != null) rb.linearVelocity = Vector2.zero;

        foreach (var col in GetComponents<Collider2D>()) col.enabled = false;

        if (activeHealthBar != null) Destroy(activeHealthBar.gameObject);

        bool hasDeathListeners = OnDeath != null;
        OnDeath?.Invoke();

        // Fallback: if no visual handler is subscribed, still remove the dead enemy.
        if (!hasDeathListeners)
        {
            Destroy(gameObject, 0.35f);
        }
    }

    public bool ShouldDamagePlayerFromCollider(Collider2D col)
    {
        if (gameObject.GetComponent<BacteriaLogic>() != null)
        {
            return col is BoxCollider2D;
        }
        return true;
    }
}
