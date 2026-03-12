using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AudioSource))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Daño")]
    [SerializeField] private bool detectEnemyContacts = true;
    [SerializeField] private float contactKnockbackForce = 8.5f;
    [SerializeField] private float contactKnockbackUpward = 2.4f;
    [SerializeField] private float contactFlashDuration = 0.5f;
    [SerializeField] private float contactInvulnerabilityDuration = 2f;
    [SerializeField] private float invulnerabilityRefreshInterval = 0.12f;
    [SerializeField] private bool showInvulnerabilityBlink = true;
    [SerializeField] private float invulnerabilityBlinkSpeed = 12f;
    [SerializeField, Range(0f, 1f)] private float invulnerabilityBlinkStrength = 0.55f;
    [SerializeField] private Color invulnerabilityBlinkColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.16f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f)] private float damageFlashStrength = 0.92f;

    [Header("Audio Daño")]
    [SerializeField] private AudioClip hurtSfx;
    [SerializeField, Range(0f, 1f)] private float hurtSfxVolume = 1f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private AudioSource sfxSource;

    private float damageFlashTimer;
    private float invulnerabilityTimer;
    private float invulnerabilityRefreshTimer;

    private Collider2D[] playerColliders;
    private readonly List<Collider2D> ignoredEnemyColliders = new List<Collider2D>(24);
    private GameManager gameManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        sfxSource = GetComponent<AudioSource>();
        playerColliders = GetComponents<Collider2D>();
    }

    private void OnEnable() => CacheGameManagerReference();

    private void OnDisable() => EndInvulnerabilityPhase();

    private void OnDestroy() => EndInvulnerabilityPhase();

    private void Update()
    {
        UpdateDamageFlash();
        UpdateInvulnerability();
        ApplyInvulnerabilityBlink();
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

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}