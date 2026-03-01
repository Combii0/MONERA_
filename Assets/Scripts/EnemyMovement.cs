using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class EnemyMovement : MonoBehaviour
{
    [Header("Seguimiento")]
    [SerializeField] private Transform target;
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float stoppingDistance = 0.3f;

    [Header("Coral")]
    public bool isCoral = false;
    [SerializeField] private float coralTilesToMove = 3f;
    [SerializeField] private float coralTileSize = 1f;
    [SerializeField] private float coralMoveSpeed = 0.8f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private string movingStateName = "MOVING";
    [SerializeField] private string movingClipKey = "GreenBacteriaMoving";
    [SerializeField] private AnimationClip movingClip;
    [SerializeField] private AnimationClip deathClip;

    [Header("Vida")]
    [SerializeField] private int maxHealth = 2;
    [SerializeField] private float hitCooldown = 0.08f;
    [SerializeField] private float destroyDelayAfterDeath = 0f;
    [SerializeField] private string[] damageTags = { "PlayerAttack", "Projectile" };
    [SerializeField] private bool destroyDamageSourceOnHit = false;

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

    private static Sprite healthBarSprite;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        currentHealth = Mathf.Max(1, maxHealth);
        coralBaseY = rb.position.y;

        EnsureVisualComponents();

        SetupAnimator();

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }
    }

    private void Start()
    {
        resolvedMovingStateName = ResolvePlayableStateName(movingStateName);
        SetupHealthBar();
        PlayMoveAnimation();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        EnsureVisualComponents();
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

        Vector2 currentPos = rb.position;
        Vector2 targetPos = target.position;
        Vector2 toTarget = targetPos - currentPos;
        float distance = toTarget.magnitude;

        if (distance <= stoppingDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = toTarget.normalized;
        rb.linearVelocity = direction * speed;

        if (spriteRenderer != null)
        {
            if (direction.x > 0.01f) spriteRenderer.flipX = true;
            else if (direction.x < -0.01f) spriteRenderer.flipX = false;
        }
    }

    private void LateUpdate()
    {
        UpdateHealthBarTransform();
    }

    private void UpdateCoralMovement()
    {
        float travelDistance = Mathf.Max(0f, coralTilesToMove) * Mathf.Max(0.01f, coralTileSize);
        if (travelDistance <= 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float targetY = coralMovingUp ? (coralBaseY + travelDistance) : coralBaseY;
        float step = Mathf.Max(0.01f, coralMoveSpeed) * Time.fixedDeltaTime;
        float nextY = Mathf.MoveTowards(rb.position.y, targetY, step);
        rb.MovePosition(new Vector2(rb.position.x, nextY));
        rb.linearVelocity = Vector2.zero;

        if (Mathf.Abs(nextY - targetY) <= 0.001f)
        {
            coralMovingUp = !coralMovingUp;
        }
    }

    public void TakeDamage(int amount = 1)
    {
        if (isDead || amount <= 0) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
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
        TryReceiveTagDamage(collision.gameObject);
    }

    private void TryReceiveTagDamage(GameObject damageSource)
    {
        if (isDead || damageSource == null || damageTags == null) return;
        if (Time.time < nextAllowedHitTime) return;

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

        if (healthBarRoot != null)
        {
            healthBarRoot.gameObject.SetActive(false);
        }

        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        float deathDuration = 0.35f;
        if (deathClip != null)
        {
            if (!TryOverrideMovingClip(deathClip))
            {
                deathDuration = deathClip.length;
            }
            else
            {
                deathDuration = deathClip.length;
                PlayMoveAnimation();
            }
        }

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

    private string ResolvePlayableStateName(string preferredStateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return string.Empty;

        if (CanPlayState(preferredStateName)) return preferredStateName;
        string preferredBaseLayer = "Base Layer." + preferredStateName;
        if (CanPlayState(preferredBaseLayer)) return preferredBaseLayer;

        const string fallbackState = "MOVING";
        if (CanPlayState(fallbackState)) return fallbackState;
        string fallbackBaseLayer = "Base Layer." + fallbackState;
        if (CanPlayState(fallbackBaseLayer)) return fallbackBaseLayer;

        Debug.LogWarning($"No se encontro un estado valido para EnemyMovement en '{name}'. Revisa 'movingStateName' y el Animator Controller.", this);
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
}
