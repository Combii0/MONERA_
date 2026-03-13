using System;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Protector : MonoBehaviour
{
    private enum AttackModeSelection
    {
        ProtectorMoving = 0,
        ProtectorAttack = 1,
        ProtectorSummoner = 2,
        ProtectorTP = 3
    }

    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 60;
    [SerializeField] private float hitCooldown = 0.08f;
    [SerializeField] private string[] damageTags = { "PlayerAttack", "Projectile" };

    [Header("Health Bar")]
    [Tooltip("Arrastra aquí el prefab normal (GameObject).")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float healthBarYOffset = 0.5f;

    [Header("Damage Feedback")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip hitSfx;
    [SerializeField, Range(0f, 1f)] private float hitSfxVolume = 0.9f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.16f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f)] private float damageFlashStrength = 0.9f;
    [SerializeField] private float damageFlashDuration = 0.12f;

    [Header("Boss Audio")]
    [SerializeField] private AudioClip finalHitSfx;
    [SerializeField, Range(0f, 1f)] private float finalHitSfxVolume = 1f;
    [SerializeField] private AudioClip appearanceSfx;
    [SerializeField, Range(0f, 1f)] private float appearanceSfxVolume = 1f;
    [SerializeField, Min(0f)] private float appearanceSfxStartTime = 0.4f;
    [SerializeField] private AudioClip bossMainThemeClip;
    [SerializeField, Range(0f, 1f)] private float bossMainThemeVolume = 1f;
    [FormerlySerializedAs("bossBackgroundLoopClip")]
    [SerializeField] private AudioClip backgroundMusicClip;
    [FormerlySerializedAs("bossBackgroundLoopVolume")]
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 1f;
    [SerializeField, Min(0f)] private float entryBackgroundFadeOutDuration = 0.12f;
    [SerializeField, Min(0f)] private float lethalHitBackgroundFadeInDuration = 0.35f;

    [Header("Entry Dialogue")]
    [SerializeField, TextArea(2, 6)] private string entryDialogueText;
    [SerializeField, Min(1f)] private float entryDialogueRevealCharsPerSecond = 14f;
    [SerializeField] private Vector3 entryDialogueOffset = new Vector3(0f, 1.75f, 0f);
    [SerializeField, Min(1f)] private float entryDialogueWidth = 8f;
    [SerializeField, Min(0.5f)] private float entryDialogueHeight = 2.5f;
    [SerializeField, Min(0.25f)] private float entryDialogueFontSize = 1.15f;
    [SerializeField] private int entryDialogueSortingOrderBoost = 120;
    [SerializeField] private string entryDialogueContinueLabel = "Press E";
    [SerializeField] private Vector3 entryDialogueContinueOffsetFromPlayer = new Vector3(0f, -1.15f, 0f);

    [Header("Contact Damage To Player")]
    [SerializeField] private float contactKnockbackForce = 8.5f;
    [SerializeField] private float contactKnockbackUpward = 2.4f;
    [SerializeField] private float contactFlashDuration = 0.5f;

    [Header("Visual Configuration")]
    [SerializeField] private bool playMovingStateOnStart = false;
    [SerializeField] private string movingStateName = "MOVING";
    [SerializeField] private string deathStateName = "OVER";
    [Tooltip("Opcional: se usa para reproducir la animacion de muerte y/o calcular tiempo de destruccion.")]
    public AnimationClip deathAnimationClip;
    [SerializeField] private bool flipByVelocity = false;
    [SerializeField] private bool faceRightWhenVelocityPositive = false;

    [Header("Death")]
    [SerializeField, Min(0.05f)] private float fallbackDestroyDelaySeconds = 2f;
    [SerializeField] private bool disableMovementScriptOnDeath = true;
    [SerializeField] private Color deathTintColor = Color.white;

    [Header("Defeat Transition")]
    [SerializeField] private bool killAllEnemiesOnDeath = true;
    [SerializeField] private string victorySceneName = "The Tunnel";
    [SerializeField, Min(0.05f)] private float deathFinalFadeOutDuration = 6f;
    [SerializeField, Min(0f)] private float deathBlackScreenHoldSeconds = 3f;
    [SerializeField, Range(2, 64)] private int deathFadeSteps = 28;
    [SerializeField, Min(0.1f)] private float tunnelIntroFadeInDuration = 1.8f;
    [SerializeField, Range(2, 64)] private int tunnelIntroFadeInSteps = 30;

    [Header("Defeat Camera")]
    [SerializeField] private bool zoomCameraToProtectorOnDeath = true;
    [SerializeField] private Vector2 deathCameraFocusOffset = new Vector2(0f, 0.45f);
    [SerializeField, Min(0.05f)] private float deathCameraMoveDuration = 1.6f;
    [SerializeField, Min(0.05f)] private float deathCameraZoomDuration = 1.8f;
    [SerializeField, Min(0.05f)] private float deathCameraTargetOrthoSize = 2.6f;
    [SerializeField, Range(1f, 179f)] private float deathCameraTargetFieldOfView = 38f;

    [Header("Ataque Aleatorio")]
    [SerializeField, Min(0.1f)] private float movementChangeIntervalSeconds = 5f;
    [Range(0f, 1f)] public float sweepAttackChancePerInterval = 0.6f;
    [Range(0f, 1f)] public float summonerAttackChancePerInterval = 0.25f;
    [Range(0f, 1f)] public float tpAttackChancePerInterval = 0.1f;
    [SerializeField] private bool alternateAttackAndMovement = true;

    public event Action OnDeath;
    public event Action OnDamaged;

    public int CurrentHealth => currentHealth;
    public bool IsDead { get; private set; }
    public bool isDead => IsDead;
    public bool isInvulnerable;
    public Rigidbody2D Rigidbody => body;
    public Rigidbody2D rb => body;

    private int currentHealth;
    private float nextAllowedHitTime;
    private float damageFlashTimer;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer[] damageFlashRenderers;
    private Color[] damageFlashBaseColors;
    private Animator animator;
    private Color baseSpriteColor;
    private ProtectorDialogue protectorDialogue;
    private ProtectorMoving protectorMoving;
    private ProtectorAttack protectorAttack;
    private ProtectorSummoner protectorSummoner;
    private ProtectorTP protectorTP;

    private GameObject activeHealthBar;
    private Transform fillTransform;
    private static Transform healthBarsContainer;
    private float initialFillScaleX;
    private float initialFillPosX;

    private int movingStateHash;
    private int movingStateFullPathHash;
    private int deathStateHash;
    private int deathStateFullPathHash;

    private PlayableGraph deathPlayableGraph;
    private bool isDeathPlayableActive;
    private bool lockPositionOnDeath;
    private Vector3 deathLockedWorldPosition;
    private float nextMovementChangeTime;
    private bool randomAttackSchedulerStarted;
    private bool hasPendingAttackModeSelection;
    private AttackModeSelection pendingAttackModeSelection = AttackModeSelection.ProtectorMoving;
    private AttackModeSelection lastAppliedAttackMode = AttackModeSelection.ProtectorMoving;
    private bool deathTransitionStarted;
    private Canvas deathOverlayCanvas;
    private Image deathOverlayImage;
    private CamaraMovement disabledDeathCameraMovement;
    private bool keepDeathCameraLockedDuringSceneTransition;
    private bool backgroundMusicStarted;
    private bool mainThemeStarted;
    private bool appearanceSfxPlayed;
    private AudioSource timedSfxSource;
    private GameObject entryDialogueRoot;
    private TextMeshPro entryDialogueTextMesh;
    private TextMeshPro entryDialogueContinueMesh;
    private bool entryDialogueSequenceStarted;
    private bool entryDialogueCompleted;
    private float nextEntryDialogueTypeSfxTime;
    private PlayerMovement entryDialogueFrozenPlayer;
    private PlayerShooting entryDialogueFrozenShooting;
    private PlayerAnimation entryDialogueFrozenAnimation;
    private bool entryDialoguePlayerMovementWasEnabled;
    private bool entryDialoguePlayerShootingWasEnabled;
    private bool entryDialoguePlayerAnimationWasEnabled;
    private bool entryDialogueGameplayFrozen;
    private float entryDialoguePreviousTimeScale = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        CacheDamageFlashRenderers();
        animator = GetComponent<Animator>();
        protectorDialogue = GetComponent<ProtectorDialogue>();
        protectorMoving = GetComponent<ProtectorMoving>();
        protectorAttack = GetComponent<ProtectorAttack>();
        protectorSummoner = GetComponent<ProtectorSummoner>();
        protectorTP = GetComponent<ProtectorTP>();
        if (spriteRenderer != null) baseSpriteColor = spriteRenderer.color;

        currentHealth = Mathf.Max(1, maxHealth);

        movingStateHash = Animator.StringToHash(movingStateName);
        movingStateFullPathHash = Animator.StringToHash($"Base Layer.{movingStateName}");
        deathStateHash = Animator.StringToHash(deathStateName);
        deathStateFullPathHash = Animator.StringToHash($"Base Layer.{deathStateName}");

        if (body != null)
        {
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.angularVelocity = 0f;
        }

        DisableLegacyEnemyComponents();

        // Se reemplazo la logica de visuales en este script. Evitamos que se duplique.
        ProtectorVisuals legacyVisuals = GetComponent<ProtectorVisuals>();
        if (legacyVisuals != null) legacyVisuals.enabled = false;
    }

    private void Start()
    {
        SpawnHealthBar();
        if (playMovingStateOnStart) PlayMovingState();
        StartBackgroundMusicIfNeeded();

        nextMovementChangeTime = float.PositiveInfinity;
        if (protectorMoving != null)
        {
            protectorMoving.OnEntrySequenceFinished += HandleEntrySequenceFinished;
            if (protectorMoving.IsEntrySequenceFinished)
            {
                TryPlayAppearanceSfxIfSpriteIsWhite();
                HandleEntrySequenceFinished();
            }
        }
        else
        {
            TryPlayAppearanceSfxIfSpriteIsWhite();
            HandleEntrySequenceFinished();
        }
    }

    private void Update()
    {
        UpdateHealthBarPosition();
        UpdateFacingByVelocity();
        UpdateRandomMovementChanges();
    }

    private void LateUpdate()
    {
        UpdateDamageFlash();
        TryPlayAppearanceSfxOnWhiteFrame();
        KeepDeathPositionLocked();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        TryReceiveTagDamage(other.gameObject);
        TryDamagePlayerByContact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        TryReceiveTagDamage(collision.gameObject);
        TryDamagePlayerByContact(collision.collider);
    }

    public void TakeDamage(int amount = 1)
    {
        if (IsDead || amount <= 0 || isInvulnerable) return;

        bool isLethalHit = currentHealth - amount <= 0;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        TriggerDamageFeedback(isLethalHit);
        UpdateHealthBarVisuals();
        OnDamaged?.Invoke();

        if (currentHealth <= 0) Die();
    }

    private void TryReceiveTagDamage(GameObject damageSource)
    {
        if (IsDead || damageSource == null) return;

        PlayerProjectile projectile = damageSource.GetComponentInParent<PlayerProjectile>();
        if (projectile != null)
        {
            if (!projectile.TryConsumeImpactDamage(out int projectileDamage)) return;

            TakeDamage(projectileDamage);
            nextAllowedHitTime = Time.time + Mathf.Max(0.01f, hitCooldown);
            return;
        }

        if (Time.time < nextAllowedHitTime) return;

        for (int i = 0; i < damageTags.Length; i++)
        {
            if (!MatchesTagSafe(damageSource, damageTags[i])) continue;

            TakeDamage(1);
            nextAllowedHitTime = Time.time + Mathf.Max(0.01f, hitCooldown);
            return;
        }
    }

    private void TryDamagePlayerByContact(Collider2D hitCollider)
    {
        if (IsDead || hitCollider == null) return;

        PlayerMovement player = hitCollider.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TryDamagePlayer(
                transform.position,
                contactKnockbackForce,
                contactKnockbackUpward,
                contactFlashDuration
            );
        }
    }

    private void TriggerDamageFeedback(bool isLethalHit)
    {
        damageFlashTimer = Mathf.Max(0.02f, damageFlashDuration);

        if (isLethalHit && GameManager.Instance != null)
        {
            GameManager.Instance.TransitionProtectorDeathMusic(lethalHitBackgroundFadeInDuration);
        }

        AudioClip clipToPlay = isLethalHit && finalHitSfx != null ? finalHitSfx : hitSfx;
        float volumeToPlay = isLethalHit && finalHitSfx != null ? finalHitSfxVolume : hitSfxVolume;
        PlayBossSfx(clipToPlay, volumeToPlay);
    }

    private void UpdateDamageFlash()
    {
        if (damageFlashRenderers == null || damageFlashRenderers.Length == 0) return;
        if (IsDead)
        {
            SetDamageFlashColor(deathTintColor);
            return;
        }

        if (damageFlashTimer > 0f)
        {
            damageFlashTimer -= Time.deltaTime;
            SetDamageFlashColor(Color.Lerp(baseSpriteColor, damageFlashColor, damageFlashStrength));
        }
        else
        {
            for (int i = 0; i < damageFlashRenderers.Length; i++)
            {
                SpriteRenderer renderer = damageFlashRenderers[i];
                if (renderer == null) continue;

                Color baseColor = i < damageFlashBaseColors.Length ? damageFlashBaseColors[i] : baseSpriteColor;
                renderer.color = Color.Lerp(renderer.color, baseColor, Time.deltaTime * 18f);
            }
        }
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
            Debug.LogWarning("No se encontro 'Fill' en el prefab de la barra de vida.", this);
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
        if (activeHealthBar == null || IsDead) return;

        float finalYOffset = healthBarYOffset;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) finalYOffset += col.bounds.extents.y;

        activeHealthBar.transform.position = transform.position + new Vector3(0f, finalYOffset, 0f);
    }

    private void UpdateHealthBarVisuals()
    {
        if (fillTransform == null) return;

        float ratio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
        fillTransform.localScale = new Vector3(
            initialFillScaleX * ratio,
            fillTransform.localScale.y,
            fillTransform.localScale.z
        );
        fillTransform.localPosition = new Vector3(
            initialFillPosX - (initialFillScaleX * (1f - ratio) * 0.5f),
            fillTransform.localPosition.y,
            fillTransform.localPosition.z
        );
        fillTransform.gameObject.SetActive(ratio > 0f);
    }

    private void PlayMovingState()
    {
        if (animator == null || string.IsNullOrWhiteSpace(movingStateName)) return;

        animator.enabled = true;

        bool hasMovingState = animator.HasState(0, movingStateFullPathHash) || animator.HasState(0, movingStateHash);
        if (!hasMovingState)
        {
            Debug.LogWarning($"Protector: El estado '{movingStateName}' no existe en el Animator.", this);
            return;
        }

        int stateHash = animator.HasState(0, movingStateFullPathHash) ? movingStateFullPathHash : movingStateHash;
        animator.Play(stateHash, 0, 0f);
    }

    private void UpdateFacingByVelocity()
    {
        if (!flipByVelocity || spriteRenderer == null || body == null || IsDead) return;

        float velocityX = body.linearVelocity.x;
        if (velocityX > 0.01f) spriteRenderer.flipX = !faceRightWhenVelocityPositive;
        else if (velocityX < -0.01f) spriteRenderer.flipX = faceRightWhenVelocityPositive;
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        deathTransitionStarted = false;
        keepDeathCameraLockedDuringSceneTransition = false;
        deathLockedWorldPosition = transform.position;
        lockPositionOnDeath = true;
        damageFlashTimer = 0f;
        SetDamageFlashColor(deathTintColor);

        if (body != null) body.linearVelocity = Vector2.zero;
        if (disableMovementScriptOnDeath && protectorMoving != null) protectorMoving.enabled = false;
        if (CanUseAttackMode()) protectorAttack.ForceReturnToIdle();
        if (CanUseSummonerMode()) protectorSummoner.ForceReturnToIdle();
        if (CanUseTpMode()) protectorTP.ForceReturnToIdle();

        Collider2D[] allColliders = GetComponents<Collider2D>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null) allColliders[i].enabled = false;
        }

        if (killAllEnemiesOnDeath)
        {
            EliminateAllEnemies();
        }

        if (activeHealthBar != null) Destroy(activeHealthBar.gameObject);
        OnDeath?.Invoke();
        KeepDeathPositionLocked();
        StartCoroutine(HandleDeathCinematicSequence());
    }

    private IEnumerator HandleDeathCinematicSequence()
    {
        if (deathTransitionStarted) yield break;
        deathTransitionStarted = true;

        yield return FadeDeathOverlayToAlpha(0f, 0f);

        if (zoomCameraToProtectorOnDeath)
        {
            yield return FocusDeathCameraOnProtector();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopMainMusicKeepBackground();
        }

        if (protectorDialogue != null)
        {
            yield return protectorDialogue.PlayPreDeathDialogue();
        }

        float deathDuration = PlayDeathAnimationAndResolveDuration();
        float elapsed = 0f;
        while (elapsed < deathDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        StopDeathPlayable();
        if (protectorDialogue != null)
        {
            protectorDialogue.PlayPostDeathAudio();
        }

        if (protectorDialogue != null && protectorDialogue.HasCustomPostDeathFlow)
        {
            yield return BeginPostDeathTransitionWithDialogue();
        }
        else
        {
            BeginPostDeathTransitionAndDestroy();
        }
    }

    private IEnumerator FocusDeathCameraOnProtector()
    {
        Camera sceneCamera = Camera.main;
        if (sceneCamera == null) yield break;

        CamaraMovement cameraMovement = sceneCamera.GetComponent<CamaraMovement>();
        if (cameraMovement != null && cameraMovement.enabled)
        {
            cameraMovement.enabled = false;
            disabledDeathCameraMovement = cameraMovement;
        }

        Transform cameraTransform = sceneCamera.transform;
        Vector3 startPosition = cameraTransform.position;
        Vector3 targetPosition = new Vector3(
            deathLockedWorldPosition.x + deathCameraFocusOffset.x,
            deathLockedWorldPosition.y + deathCameraFocusOffset.y,
            startPosition.z);

        float startOrthoSize = sceneCamera.orthographicSize;
        float targetOrthoSize = Mathf.Max(0.05f, deathCameraTargetOrthoSize);
        float startFieldOfView = sceneCamera.fieldOfView;
        float targetFieldOfView = Mathf.Clamp(deathCameraTargetFieldOfView, 1f, 179f);

        float moveDuration = Mathf.Max(0.05f, deathCameraMoveDuration);
        float zoomDuration = Mathf.Max(0.05f, deathCameraZoomDuration);
        float sequenceDuration = Mathf.Max(moveDuration, zoomDuration);

        float timer = 0f;
        while (timer < sequenceDuration)
        {
            timer += Time.unscaledDeltaTime;

            float moveT = Mathf.Clamp01(timer / moveDuration);
            float zoomT = Mathf.Clamp01(timer / zoomDuration);
            float moveEasedT = moveT * moveT * (3f - (2f * moveT));
            float zoomEasedT = zoomT * zoomT * (3f - (2f * zoomT));

            cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, moveEasedT);
            if (sceneCamera.orthographic)
            {
                sceneCamera.orthographicSize = Mathf.Lerp(startOrthoSize, targetOrthoSize, zoomEasedT);
            }
            else
            {
                sceneCamera.fieldOfView = Mathf.Lerp(startFieldOfView, targetFieldOfView, zoomEasedT);
            }

            yield return null;
        }

        cameraTransform.position = targetPosition;
        if (sceneCamera.orthographic) sceneCamera.orthographicSize = targetOrthoSize;
        else sceneCamera.fieldOfView = targetFieldOfView;
    }

    private void BeginPostDeathTransitionAndDestroy()
    {
        EnsureDeathOverlay();

        float finalFadeDuration = Mathf.Max(6f, deathFinalFadeOutDuration);
        float blackHoldDuration = Mathf.Max(0f, deathBlackScreenHoldSeconds);
        int safeFadeSteps = Mathf.Max(2, deathFadeSteps);

        if (deathOverlayCanvas == null || deathOverlayImage == null)
        {
            RestoreDeathCameraMovement();
            LoadVictorySceneAfterDefeat();
            Destroy(gameObject);
            return;
        }

        ProtectorDeathTransitionRunner runner = deathOverlayCanvas.GetComponent<ProtectorDeathTransitionRunner>();
        if (runner == null)
        {
            runner = deathOverlayCanvas.gameObject.AddComponent<ProtectorDeathTransitionRunner>();
        }

        keepDeathCameraLockedDuringSceneTransition = true;
        runner.Begin(
            deathOverlayImage,
            finalFadeDuration,
            safeFadeSteps,
            blackHoldDuration,
            victorySceneName,
            tunnelIntroFadeInDuration,
            tunnelIntroFadeInSteps,
            disabledDeathCameraMovement);

        Destroy(gameObject);
    }

    private IEnumerator BeginPostDeathTransitionWithDialogue()
    {
        EnsureDeathOverlay();

        if (deathOverlayCanvas == null || deathOverlayImage == null)
        {
            RestoreDeathCameraMovement();
            LoadVictorySceneAfterDefeat();
            Destroy(gameObject);
            yield break;
        }

        float finalFadeDuration = Mathf.Max(0.05f, deathFinalFadeOutDuration);
        yield return FadeDeathOverlayToAlpha(1f, finalFadeDuration);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopConfiguredMusicImmediate();
        }

        if (protectorDialogue != null)
        {
            yield return protectorDialogue.ShowRewardMessageAndDelay();
        }

        Time.timeScale = 1f;
        RestoreDeathCameraMovement();
        LoadVictorySceneAfterDefeat();
        Destroy(gameObject);
    }

    private float PlayDeathAnimationAndResolveDuration()
    {
        float deathDuration = Mathf.Max(0.05f, fallbackDestroyDelaySeconds);

        if (deathAnimationClip != null)
        {
            PlayDeathClip(deathAnimationClip);
            return Mathf.Max(0.05f, deathAnimationClip.length);
        }

        if (animator == null) return deathDuration;

        animator.enabled = true;
        bool hasDeathState = animator.HasState(0, deathStateFullPathHash) || animator.HasState(0, deathStateHash);
        if (!hasDeathState)
        {
            Debug.LogWarning($"Protector: El estado '{deathStateName}' no existe en el Animator.", this);
            return deathDuration;
        }

        int stateHash = animator.HasState(0, deathStateFullPathHash) ? deathStateFullPathHash : deathStateHash;
        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool isDeathState = stateInfo.shortNameHash == deathStateHash || stateInfo.fullPathHash == deathStateFullPathHash;
        if (isDeathState && stateInfo.length > 0f)
        {
            deathDuration = Mathf.Max(0.05f, stateInfo.length);
        }

        return deathDuration;
    }

    private void EliminateAllEnemies()
    {
        EnemyHealth[] allEnemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < allEnemies.Length; i++)
        {
            EnemyHealth enemy = allEnemies[i];
            if (enemy == null || enemy.gameObject == gameObject) continue;
            enemy.TakeDamage(int.MaxValue);
        }
    }

    private IEnumerator FadeDeathOverlayToAlpha(float targetAlpha, float duration)
    {
        EnsureDeathOverlay();
        if (deathOverlayImage == null) yield break;

        Color baseColor = deathOverlayImage.color;
        float startAlpha = baseColor.a;
        float clampedTargetAlpha = Mathf.Clamp01(targetAlpha);
        float safeDuration = Mathf.Max(0f, duration);
        int steps = Mathf.Max(2, deathFadeSteps);

        if (safeDuration <= 0.0001f)
        {
            deathOverlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, clampedTargetAlpha);
            yield break;
        }

        float timer = 0f;
        while (timer < safeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float rawT = Mathf.Clamp01(timer / safeDuration);
            float stepped = Mathf.Floor(rawT * steps) / steps;
            float alpha = Mathf.Lerp(startAlpha, clampedTargetAlpha, stepped);
            deathOverlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        deathOverlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, clampedTargetAlpha);
    }

    private void EnsureDeathOverlay()
    {
        if (deathOverlayCanvas != null && deathOverlayImage != null) return;

        GameObject canvasObject = new GameObject("ProtectorDeathOverlayCanvas");
        deathOverlayCanvas = canvasObject.AddComponent<Canvas>();
        deathOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        deathOverlayCanvas.sortingOrder = 5000;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(canvasObject.transform, false);
        deathOverlayImage = imageObject.AddComponent<Image>();
        deathOverlayImage.color = new Color(0f, 0f, 0f, 0f);
        deathOverlayImage.raycastTarget = false;

        RectTransform rect = deathOverlayImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void LoadVictorySceneAfterDefeat()
    {
        Time.timeScale = 1f;
        RestoreDeathCameraMovement();

        if (string.IsNullOrWhiteSpace(victorySceneName))
        {
            Debug.LogWarning("Protector: victorySceneName está vacío; no se pudo cambiar de escena.", this);
            if (deathOverlayCanvas != null) Destroy(deathOverlayCanvas.gameObject);
            return;
        }

        GameManager.ConfigureNextSceneIntroFade(tunnelIntroFadeInDuration, tunnelIntroFadeInSteps);

        int sceneIndex = ResolveBuildSceneIndexByName(victorySceneName);
        if (sceneIndex >= 0)
        {
            SceneManager.LoadScene(sceneIndex);
            return;
        }

        SceneManager.LoadScene(victorySceneName);
    }

    private void RestoreDeathCameraMovement()
    {
        if (disabledDeathCameraMovement == null) return;
        disabledDeathCameraMovement.enabled = true;
        disabledDeathCameraMovement = null;
    }

    private static int ResolveBuildSceneIndexByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return -1;

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(path)) continue;

            string buildSceneName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(buildSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void PlayDeathClip(AnimationClip clip)
    {
        if (clip == null) return;
        if (animator == null) return;
        StopDeathPlayable();

        deathPlayableGraph = PlayableGraph.Create($"{name}_DeathClipGraph");
        deathPlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        animator.enabled = true;
        animator.Rebind();
        animator.Update(0f);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(deathPlayableGraph, "DeathClipOutput", animator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(deathPlayableGraph, clip);
        output.SetSourcePlayable(clipPlayable);
        deathPlayableGraph.Play();
        isDeathPlayableActive = true;
    }

    private void StopDeathPlayable()
    {
        if (!isDeathPlayableActive) return;
        if (deathPlayableGraph.IsValid()) deathPlayableGraph.Destroy();
        isDeathPlayableActive = false;
    }

    private void KeepDeathPositionLocked()
    {
        if (!lockPositionOnDeath || !IsDead) return;

        transform.position = deathLockedWorldPosition;
        if (body != null)
        {
            body.position = new Vector2(deathLockedWorldPosition.x, deathLockedWorldPosition.y);
        }
    }

    private void UpdateRandomMovementChanges()
    {
        if (IsDead) return;
        if (!randomAttackSchedulerStarted) return;
        bool canUseAttack = CanUseAttackMode();
        bool canUseSummoner = CanUseSummonerMode();
        bool canUseTp = CanUseTpMode();
        if (!canUseAttack && !canUseSummoner && !canUseTp) return;
        if (protectorMoving != null && !protectorMoving.IsEntrySequenceFinished) return;

        // Nunca cambiar de modo mientras una habilidad especial este en curso.
        if (IsAnySpecialModeExecuting()) return;

        if (Time.time >= nextMovementChangeTime)
        {
            nextMovementChangeTime = Time.time + Mathf.Max(0.1f, movementChangeIntervalSeconds);
            ScheduleRandomAttackModeSelection();
        }

        ApplyPendingAttackModeSelection();

        // Fail-safe: si por alguna razon quedo override activo sin habilidad en curso, soltamos control.
        if (!hasPendingAttackModeSelection && canUseAttack && !protectorAttack.IsExecutingAttack && protectorAttack.IsMovementOverrideApplied)
        {
            protectorAttack.ForceReturnToIdle();
        }

        if (!hasPendingAttackModeSelection && canUseSummoner && !protectorSummoner.IsExecutingSummon && protectorSummoner.IsMovementOverrideApplied)
        {
            protectorSummoner.ForceReturnToIdle();
        }

        if (!hasPendingAttackModeSelection && canUseTp && !protectorTP.IsExecutingTp && protectorTP.IsMovementOverrideApplied)
        {
            protectorTP.ForceReturnToIdle();
        }
    }

    private void HandleEntrySequenceFinished()
    {
        if (ShouldShowEntryDialogue())
        {
            if (!entryDialogueSequenceStarted)
            {
                StartCoroutine(HandleEntryDialogueSequence());
            }

            return;
        }

        StartCombatAfterEntryIfNeeded();
    }

    private bool ShouldShowEntryDialogue()
    {
        return !entryDialogueCompleted && !string.IsNullOrWhiteSpace(entryDialogueText);
    }

    private IEnumerator HandleEntryDialogueSequence()
    {
        if (entryDialogueSequenceStarted) yield break;
        entryDialogueSequenceStarted = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopConfiguredMusicImmediate();
        }

        yield return ShowEntryDialogueAboveProtector();

        entryDialogueCompleted = true;
        entryDialogueSequenceStarted = false;
        StartCombatAfterEntryIfNeeded();
    }

    private void StartCombatAfterEntryIfNeeded()
    {
        if (!mainThemeStarted)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartProtectorCombatMusic(
                    bossMainThemeClip,
                    bossMainThemeVolume,
                    entryBackgroundFadeOutDuration);
                mainThemeStarted = bossMainThemeClip != null;
            }
            else
            {
                StartMainThemeIfNeeded();
            }
        }

        ActivateRandomAttackScheduler();
    }

    private IEnumerator ShowEntryDialogueAboveProtector()
    {
        FreezeEntryDialogueGameplay();
        EnsureEntryDialogueVisual();
        ApplyEntryDialogueStyle();

        if (entryDialogueRoot == null || entryDialogueTextMesh == null)
        {
            RestoreEntryDialogueGameplay();
            yield break;
        }

        entryDialogueRoot.transform.localPosition = entryDialogueOffset;
        entryDialogueRoot.SetActive(true);

        if (entryDialogueContinueMesh != null)
        {
            entryDialogueContinueMesh.text = string.IsNullOrWhiteSpace(entryDialogueContinueLabel)
                ? "Press E"
                : entryDialogueContinueLabel;
            entryDialogueContinueMesh.maxVisibleCharacters = int.MaxValue;
            SetEntryDialogueContinueVisible(false);
            UpdateEntryDialogueContinuePosition();
        }

        entryDialogueTextMesh.text = entryDialogueText;
        entryDialogueTextMesh.ForceMeshUpdate();
        entryDialogueTextMesh.maxVisibleCharacters = 0;

        int totalChars = entryDialogueTextMesh.textInfo.characterCount;
        float visible = 0f;
        float revealSpeed = Mathf.Max(1f, entryDialogueRevealCharsPerSecond);

        yield return null;

        while (entryDialogueTextMesh.maxVisibleCharacters < totalChars)
        {
            if (WasEntryDialogueInteractPressedThisFrame())
            {
                entryDialogueTextMesh.maxVisibleCharacters = totalChars;
                break;
            }

            visible += revealSpeed * Time.unscaledDeltaTime;
            int nextVisible = Mathf.Min(totalChars, Mathf.FloorToInt(visible));

            while (entryDialogueTextMesh.maxVisibleCharacters < nextVisible)
            {
                entryDialogueTextMesh.maxVisibleCharacters++;
                PlayEntryDialogueTypeSfx();
            }

            yield return null;
        }

        if (entryDialogueContinueMesh != null)
        {
            SetEntryDialogueContinueVisible(true);
            UpdateEntryDialogueContinuePosition();
        }

        yield return null;

        while (!WasEntryDialogueInteractPressedThisFrame())
        {
            UpdateEntryDialogueContinuePosition();
            UpdateEntryDialoguePromptPulse();
            yield return null;
        }

        if (entryDialogueRoot != null)
        {
            entryDialogueRoot.SetActive(false);
        }

        RestoreEntryDialogueGameplay();
    }

    private void StartBackgroundMusicIfNeeded()
    {
        if (backgroundMusicStarted) return;
        backgroundMusicStarted = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayEncounterBackgroundLoop(backgroundMusicClip, backgroundMusicVolume);
        }
    }

    private void StartMainThemeIfNeeded()
    {
        if (mainThemeStarted) return;
        mainThemeStarted = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayEncounterMainTheme(bossMainThemeClip, bossMainThemeVolume);
        }
    }

    private void FreezeEntryDialogueGameplay()
    {
        if (entryDialogueGameplayFrozen) return;

        entryDialoguePreviousTimeScale = Time.timeScale;
        entryDialogueGameplayFrozen = true;
        Time.timeScale = 0f;

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        entryDialogueFrozenPlayer = FindFirstObjectByType<PlayerMovement>();
        if (entryDialogueFrozenPlayer != null)
        {
            Rigidbody2D playerBody = entryDialogueFrozenPlayer.GetComponent<Rigidbody2D>();
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector2.zero;
                playerBody.angularVelocity = 0f;
            }

            entryDialoguePlayerMovementWasEnabled = entryDialogueFrozenPlayer.enabled;
            if (entryDialoguePlayerMovementWasEnabled) entryDialogueFrozenPlayer.enabled = false;

            entryDialogueFrozenShooting = entryDialogueFrozenPlayer.GetComponent<PlayerShooting>();
            entryDialoguePlayerShootingWasEnabled =
                entryDialogueFrozenShooting != null && entryDialogueFrozenShooting.enabled;
            if (entryDialoguePlayerShootingWasEnabled) entryDialogueFrozenShooting.enabled = false;

            entryDialogueFrozenAnimation = entryDialogueFrozenPlayer.GetComponent<PlayerAnimation>();
            entryDialoguePlayerAnimationWasEnabled =
                entryDialogueFrozenAnimation != null && entryDialogueFrozenAnimation.enabled;
            if (entryDialoguePlayerAnimationWasEnabled) entryDialogueFrozenAnimation.enabled = false;
        }
    }

    private void RestoreEntryDialogueGameplay()
    {
        if (!entryDialogueGameplayFrozen) return;

        if (entryDialogueFrozenPlayer != null)
        {
            entryDialogueFrozenPlayer.enabled = entryDialoguePlayerMovementWasEnabled;
        }

        if (entryDialogueFrozenShooting != null)
        {
            entryDialogueFrozenShooting.enabled = entryDialoguePlayerShootingWasEnabled;
        }

        if (entryDialogueFrozenAnimation != null)
        {
            entryDialogueFrozenAnimation.enabled = entryDialoguePlayerAnimationWasEnabled;
        }

        entryDialogueFrozenPlayer = null;
        entryDialogueFrozenShooting = null;
        entryDialogueFrozenAnimation = null;
        entryDialoguePlayerMovementWasEnabled = false;
        entryDialoguePlayerShootingWasEnabled = false;
        entryDialoguePlayerAnimationWasEnabled = false;

        Time.timeScale = entryDialoguePreviousTimeScale;
        entryDialogueGameplayFrozen = false;
    }

    private void EnsureEntryDialogueVisual()
    {
        if (entryDialogueRoot != null && entryDialogueTextMesh != null && entryDialogueContinueMesh != null) return;

        if (entryDialogueRoot == null)
        {
            entryDialogueRoot = new GameObject("EntryDialogue");
            entryDialogueRoot.transform.SetParent(transform, false);
        }

        if (entryDialogueTextMesh == null)
        {
            Transform existingMessage = entryDialogueRoot.transform.Find("Message");
            if (existingMessage != null) entryDialogueTextMesh = existingMessage.GetComponent<TextMeshPro>();
        }

        if (entryDialogueTextMesh == null)
        {
            GameObject messageObject = new GameObject("Message");
            messageObject.transform.SetParent(entryDialogueRoot.transform, false);
            entryDialogueTextMesh = messageObject.AddComponent<TextMeshPro>();
        }

        if (entryDialogueContinueMesh == null)
        {
            Transform existingContinue = entryDialogueRoot.transform.Find("Continue");
            if (existingContinue != null) entryDialogueContinueMesh = existingContinue.GetComponent<TextMeshPro>();
        }

        if (entryDialogueContinueMesh == null)
        {
            GameObject continueObject = new GameObject("Continue");
            continueObject.transform.SetParent(entryDialogueRoot.transform, false);
            entryDialogueContinueMesh = continueObject.AddComponent<TextMeshPro>();
        }

        ConfigureEntryDialogueTextMesh(entryDialogueTextMesh, isContinueHint: false);
        ConfigureEntryDialogueTextMesh(entryDialogueContinueMesh, isContinueHint: true);

        entryDialogueRoot.transform.localPosition = entryDialogueOffset;
        entryDialogueRoot.SetActive(false);
    }

    private void ConfigureEntryDialogueTextMesh(TextMeshPro textMesh, bool isContinueHint)
    {
        if (textMesh == null) return;

        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.enableAutoSizing = false;
        textMesh.fontSize = isContinueHint
            ? Mathf.Max(0.2f, entryDialogueFontSize * 0.72f)
            : Mathf.Max(0.25f, entryDialogueFontSize);
        textMesh.color = isContinueHint
            ? new Color(1f, 1f, 1f, 0f)
            : Color.white;
        textMesh.fontStyle = FontStyles.Normal;
        textMesh.textWrappingMode = isContinueHint ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        textMesh.overflowMode = TextOverflowModes.Overflow;
        textMesh.text = string.Empty;

        RectTransform rect = textMesh.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = isContinueHint
            ? new Vector2(entryDialogueWidth, Mathf.Max(0.5f, entryDialogueHeight * 0.28f))
            : new Vector2(entryDialogueWidth, entryDialogueHeight);
        rect.anchoredPosition = isContinueHint
            ? new Vector2(0f, -Mathf.Max(0.45f, entryDialogueHeight * 0.62f))
            : Vector2.zero;

        MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
        if (renderer != null && spriteRenderer != null)
        {
            renderer.sortingLayerID = spriteRenderer.sortingLayerID;
            renderer.sortingOrder = spriteRenderer.sortingOrder + entryDialogueSortingOrderBoost + (isContinueHint ? 1 : 0);
        }
    }

    private void ApplyEntryDialogueStyle()
    {
        if (entryDialogueTextMesh == null || entryDialogueContinueMesh == null) return;

        DialogueManager dialogueManager = DialogueManager.Instance;
        if (dialogueManager != null)
        {
            if (dialogueManager.DialogueFontAsset != null)
            {
                entryDialogueTextMesh.font = dialogueManager.DialogueFontAsset;
                entryDialogueContinueMesh.font = dialogueManager.DialogueFontAsset;
            }

            if (dialogueManager.DialogueFontMaterial != null)
            {
                entryDialogueTextMesh.fontSharedMaterial = dialogueManager.DialogueFontMaterial;
                entryDialogueContinueMesh.fontSharedMaterial = dialogueManager.DialogueFontMaterial;
            }

            entryDialogueTextMesh.fontStyle = dialogueManager.DialogueFontStyle;
            entryDialogueContinueMesh.fontStyle = dialogueManager.DialogueFontStyle;
            entryDialogueTextMesh.color = dialogueManager.DialogueTextColor;
        }

        SetEntryDialogueContinueVisible(false);
    }

    private void SetEntryDialogueContinueVisible(bool visible)
    {
        if (entryDialogueContinueMesh == null) return;

        Color color = entryDialogueContinueMesh.color;
        color.a = visible ? 0.9f : 0f;
        entryDialogueContinueMesh.color = color;
    }

    private void UpdateEntryDialoguePromptPulse()
    {
        if (entryDialogueContinueMesh == null) return;

        Color color = entryDialogueContinueMesh.color;
        color.a = 0.55f + (Mathf.Sin(Time.unscaledTime * 6f) * 0.25f + 0.25f);
        entryDialogueContinueMesh.color = color;
    }

    private void UpdateEntryDialogueContinuePosition()
    {
        if (entryDialogueContinueMesh == null) return;

        if (entryDialogueFrozenPlayer == null)
        {
            entryDialogueContinueMesh.rectTransform.anchoredPosition =
                new Vector2(0f, -Mathf.Max(0.45f, entryDialogueHeight * 0.62f));
            return;
        }

        entryDialogueContinueMesh.transform.position =
            entryDialogueFrozenPlayer.transform.position + entryDialogueContinueOffsetFromPlayer;
    }

    private void PlayEntryDialogueTypeSfx()
    {
        AudioClip clip = DialogueManager.Instance != null
            ? DialogueManager.Instance.DefaultTypeSfx
            : null;
        if (clip == null) return;
        if (Time.unscaledTime < nextEntryDialogueTypeSfxTime) return;

        nextEntryDialogueTypeSfxTime = Time.unscaledTime + 0.02f;

        float volume = DialogueManager.Instance != null
            ? Mathf.Clamp01(DialogueManager.Instance.DefaultTypeSfxVolume)
            : 0.65f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayUiSfx(clip, volume);
        }
    }

    private static bool WasEntryDialogueInteractPressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            pressed = Keyboard.current.eKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed)
        {
            pressed = Input.GetKeyDown(KeyCode.E);
        }
#endif

        return pressed;
    }

    private void TryPlayAppearanceSfxOnWhiteFrame()
    {
        if (appearanceSfxPlayed || IsDead) return;
        if (protectorMoving == null || protectorMoving.IsEntrySequenceFinished) return;
        if (!protectorMoving.IsEntryStateActive) return;

        TryPlayAppearanceSfxIfSpriteIsWhite();
    }

    private void TryPlayAppearanceSfxIfSpriteIsWhite()
    {
        if (appearanceSfxPlayed) return;
        if (spriteRenderer == null) return;

        Color currentColor = spriteRenderer.color;
        const float WhiteThreshold = 0.999f;
        bool isFullyWhite =
            currentColor.r >= WhiteThreshold &&
            currentColor.g >= WhiteThreshold &&
            currentColor.b >= WhiteThreshold &&
            currentColor.a >= WhiteThreshold;

        if (!isFullyWhite) return;

        PlayBossSfxFromTime(appearanceSfx, appearanceSfxVolume, appearanceSfxStartTime);
        appearanceSfxPlayed = true;
    }

    private void PlayBossSfx(AudioClip clip, float volume)
    {
        if (clip == null) return;

        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void PlayBossSfxFromTime(AudioClip clip, float volume, float startTimeSeconds)
    {
        if (clip == null) return;

        AudioSource source = ResolveTimedSfxSource();
        if (source == null) return;

        float safeStartTime = Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, clip.length - 0.01f));
        source.Stop();
        source.clip = clip;
        source.time = safeStartTime;
        source.volume = Mathf.Clamp01(volume);
        source.loop = false;
        source.Play();
    }

    private AudioSource ResolveTimedSfxSource()
    {
        if (timedSfxSource != null) return timedSfxSource;

        GameObject sourceObject = new GameObject("ProtectorTimedSfxSource");
        sourceObject.transform.SetParent(transform, false);

        timedSfxSource = sourceObject.AddComponent<AudioSource>();
        timedSfxSource.playOnAwake = false;
        timedSfxSource.loop = false;
        return timedSfxSource;
    }

    private void ActivateRandomAttackScheduler()
    {
        randomAttackSchedulerStarted = true;
        nextMovementChangeTime = Time.time + Mathf.Max(0.1f, movementChangeIntervalSeconds);
        hasPendingAttackModeSelection = false;
        pendingAttackModeSelection = AttackModeSelection.ProtectorMoving;
        lastAppliedAttackMode = AttackModeSelection.ProtectorMoving;
    }

    private void ScheduleRandomAttackModeSelection()
    {
        bool canUseAttack = CanUseAttackMode();
        bool canUseSummoner = CanUseSummonerMode();
        bool canUseTp = CanUseTpMode();

        float attackChance = canUseAttack ? Mathf.Clamp01(sweepAttackChancePerInterval) : 0f;
        float summonerChance = canUseSummoner ? Mathf.Clamp01(summonerAttackChancePerInterval) : 0f;
        float tpChance = canUseTp ? Mathf.Clamp01(tpAttackChancePerInterval) : 0f;

        float combinedChance = attackChance + summonerChance + tpChance;
        if (combinedChance > 1f)
        {
            attackChance /= combinedChance;
            summonerChance /= combinedChance;
            tpChance /= combinedChance;
        }

        float roll = UnityEngine.Random.value;
        AttackModeSelection selectedMode = AttackModeSelection.ProtectorMoving;
        if (roll < attackChance)
        {
            selectedMode = AttackModeSelection.ProtectorAttack;
        }
        else if (roll < attackChance + summonerChance)
        {
            selectedMode = AttackModeSelection.ProtectorSummoner;
        }
        else if (roll < attackChance + summonerChance + tpChance)
        {
            selectedMode = AttackModeSelection.ProtectorTP;
        }

        if (alternateAttackAndMovement && selectedMode == lastAppliedAttackMode)
        {
            selectedMode = ResolveAlternatedMode(selectedMode, canUseAttack, canUseSummoner, canUseTp);
        }

        pendingAttackModeSelection = selectedMode;
        hasPendingAttackModeSelection = true;
    }

    private void ApplyPendingAttackModeSelection()
    {
        if (!hasPendingAttackModeSelection) return;

        if (pendingAttackModeSelection == AttackModeSelection.ProtectorMoving)
        {
            // Si hay una habilidad en curso, esperamos a que termine y recién luego soltamos al IDLE.
            if (IsAnySpecialModeExecuting()) return;

            // Este modo prioriza volver al movimiento normal y libera solo overrides realmente activos.
            bool shouldReleaseAttack = CanUseAttackMode() &&
                (protectorAttack.IsMovementOverrideApplied || protectorAttack.IsAttacking || protectorAttack.IsExecutingAttack);
            bool shouldReleaseSummoner = CanUseSummonerMode() &&
                (protectorSummoner.IsMovementOverrideApplied || protectorSummoner.IsSummoning || protectorSummoner.IsExecutingSummon);
            bool shouldReleaseTp = CanUseTpMode() &&
                (protectorTP.IsMovementOverrideApplied || protectorTP.TP || protectorTP.IsExecutingTp);

            if (shouldReleaseAttack) protectorAttack.ForceReturnToIdle();
            if (shouldReleaseSummoner) protectorSummoner.ForceReturnToIdle();
            if (shouldReleaseTp) protectorTP.ForceReturnToIdle();
            hasPendingAttackModeSelection = false;
            lastAppliedAttackMode = AttackModeSelection.ProtectorMoving;
            return;
        }

        if (pendingAttackModeSelection == AttackModeSelection.ProtectorAttack)
        {
            if (!CanUseAttackMode())
            {
                pendingAttackModeSelection = CanUseSummonerMode()
                    ? AttackModeSelection.ProtectorSummoner
                    : CanUseTpMode()
                        ? AttackModeSelection.ProtectorTP
                    : AttackModeSelection.ProtectorMoving;
                return;
            }

            // Si ya esta ejecutando otra habilidad, mantenemos pendiente para lanzarlo apenas quede libre.
            if (IsAnySpecialModeExecuting() || protectorAttack.IsAttacking) return;

            protectorAttack.IsAttacking = true;
            hasPendingAttackModeSelection = false;
            lastAppliedAttackMode = AttackModeSelection.ProtectorAttack;
            return;
        }

        if (pendingAttackModeSelection == AttackModeSelection.ProtectorSummoner)
        {
            if (!CanUseSummonerMode())
            {
                pendingAttackModeSelection = CanUseAttackMode()
                    ? AttackModeSelection.ProtectorAttack
                    : CanUseTpMode()
                        ? AttackModeSelection.ProtectorTP
                    : AttackModeSelection.ProtectorMoving;
                return;
            }

            // Si ya esta ejecutando otra habilidad, mantenemos pendiente para lanzarlo apenas quede libre.
            if (IsAnySpecialModeExecuting() || protectorSummoner.IsSummoning) return;

            protectorSummoner.IsSummoning = true;
            hasPendingAttackModeSelection = false;
            lastAppliedAttackMode = AttackModeSelection.ProtectorSummoner;
            return;
        }

        if (pendingAttackModeSelection == AttackModeSelection.ProtectorTP)
        {
            if (!CanUseTpMode())
            {
                pendingAttackModeSelection = CanUseAttackMode()
                    ? AttackModeSelection.ProtectorAttack
                    : CanUseSummonerMode()
                        ? AttackModeSelection.ProtectorSummoner
                        : AttackModeSelection.ProtectorMoving;
                return;
            }

            // Si ya esta ejecutando otra habilidad, mantenemos pendiente para lanzarlo apenas quede libre.
            if (IsAnySpecialModeExecuting() || protectorTP.TP) return;

            protectorTP.TP = true;
            hasPendingAttackModeSelection = false;
            lastAppliedAttackMode = AttackModeSelection.ProtectorTP;
            return;
        }
    }

    private bool CanUseAttackMode()
    {
        return protectorAttack != null && protectorAttack.isActiveAndEnabled;
    }

    private bool CanUseSummonerMode()
    {
        return protectorSummoner != null && protectorSummoner.isActiveAndEnabled;
    }

    private bool CanUseTpMode()
    {
        return protectorTP != null && protectorTP.isActiveAndEnabled;
    }

    private bool IsAnySpecialModeExecuting()
    {
        bool attackExecuting = CanUseAttackMode() && protectorAttack.IsExecutingAttack;
        bool summonExecuting = CanUseSummonerMode() && protectorSummoner.IsExecutingSummon;
        bool tpExecuting = CanUseTpMode() && protectorTP.IsExecutingTp;
        return attackExecuting || summonExecuting || tpExecuting;
    }

    private AttackModeSelection ResolveAlternatedMode(
        AttackModeSelection selectedMode,
        bool canUseAttack,
        bool canUseSummoner,
        bool canUseTp)
    {
        // Si toca repetir una habilidad, forzamos movimiento para variar.
        if (selectedMode == AttackModeSelection.ProtectorAttack ||
            selectedMode == AttackModeSelection.ProtectorSummoner ||
            selectedMode == AttackModeSelection.ProtectorTP)
        {
            return AttackModeSelection.ProtectorMoving;
        }

        // Si toca repetir MOVING, forzamos una habilidad disponible.
        if (canUseAttack || canUseSummoner || canUseTp)
        {
            float attackWeight = canUseAttack ? Mathf.Clamp01(sweepAttackChancePerInterval) : 0f;
            float summonerWeight = canUseSummoner ? Mathf.Clamp01(summonerAttackChancePerInterval) : 0f;
            float tpWeight = canUseTp ? Mathf.Clamp01(tpAttackChancePerInterval) : 0f;
            float total = attackWeight + summonerWeight + tpWeight;

            if (total <= 0.0001f)
            {
                int availableCount = 0;
                if (canUseAttack) availableCount++;
                if (canUseSummoner) availableCount++;
                if (canUseTp) availableCount++;
                if (availableCount <= 0) return AttackModeSelection.ProtectorMoving;

                int pick = UnityEngine.Random.Range(0, availableCount);
                if (canUseAttack)
                {
                    if (pick == 0) return AttackModeSelection.ProtectorAttack;
                    pick--;
                }
                if (canUseSummoner)
                {
                    if (pick == 0) return AttackModeSelection.ProtectorSummoner;
                    pick--;
                }

                return AttackModeSelection.ProtectorTP;
            }

            float roll = UnityEngine.Random.value * total;
            if (roll < attackWeight) return AttackModeSelection.ProtectorAttack;
            if (roll < attackWeight + summonerWeight) return AttackModeSelection.ProtectorSummoner;
            return AttackModeSelection.ProtectorTP;
        }

        if (canUseAttack) return AttackModeSelection.ProtectorAttack;
        if (canUseSummoner) return AttackModeSelection.ProtectorSummoner;
        if (canUseTp) return AttackModeSelection.ProtectorTP;

        return AttackModeSelection.ProtectorMoving;
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
            return false;
        }
    }

    private void CacheDamageFlashRenderers()
    {
        damageFlashRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (damageFlashRenderers == null || damageFlashRenderers.Length == 0)
        {
            damageFlashRenderers = spriteRenderer != null
                ? new[] { spriteRenderer }
                : Array.Empty<SpriteRenderer>();
        }

        damageFlashBaseColors = new Color[damageFlashRenderers.Length];
        for (int i = 0; i < damageFlashRenderers.Length; i++)
        {
            SpriteRenderer renderer = damageFlashRenderers[i];
            damageFlashBaseColors[i] = renderer != null ? renderer.color : Color.white;
        }
    }

    private void SetDamageFlashColor(Color color)
    {
        if (damageFlashRenderers == null) return;

        for (int i = 0; i < damageFlashRenderers.Length; i++)
        {
            SpriteRenderer renderer = damageFlashRenderers[i];
            if (renderer == null) continue;
            renderer.color = color;
        }
    }

    private void DisableLegacyEnemyComponents()
    {
        EnemyHealth legacyEnemyHealth = GetComponent<EnemyHealth>();
        if (legacyEnemyHealth != null)
        {
            if (maxHealth == 60 && TryReadField(legacyEnemyHealth, "maxHealth", out int legacyMaxHealth))
            {
                maxHealth = Mathf.Max(1, legacyMaxHealth);
                currentHealth = Mathf.Max(1, maxHealth);
            }

            if (Mathf.Approximately(hitCooldown, 0.08f) && TryReadField(legacyEnemyHealth, "hitCooldown", out float legacyHitCooldown))
            {
                hitCooldown = Mathf.Max(0.01f, legacyHitCooldown);
            }

            if ((damageTags == null || damageTags.Length == 0) && TryReadField(legacyEnemyHealth, "damageTags", out string[] legacyDamageTags))
            {
                damageTags = legacyDamageTags;
            }

            if (healthBarPrefab == null && TryReadField(legacyEnemyHealth, "healthBarPrefab", out GameObject legacyBarPrefab))
            {
                healthBarPrefab = legacyBarPrefab;
            }

            if (Mathf.Approximately(healthBarYOffset, 0.5f) && TryReadField(legacyEnemyHealth, "healthBarYOffset", out float legacyBarYOffset))
            {
                healthBarYOffset = legacyBarYOffset;
            }

            if (sfxSource == null && TryReadField(legacyEnemyHealth, "sfxSource", out AudioSource legacySfxSource))
            {
                sfxSource = legacySfxSource;
            }

            if (hitSfx == null && TryReadField(legacyEnemyHealth, "hitSfx", out AudioClip legacyHitSfx))
            {
                hitSfx = legacyHitSfx;
            }

            if (Mathf.Approximately(hitSfxVolume, 0.9f) && TryReadField(legacyEnemyHealth, "hitSfxVolume", out float legacyHitVolume))
            {
                hitSfxVolume = Mathf.Clamp01(legacyHitVolume);
            }

            Debug.LogWarning("Protector: se detecto EnemyHealth legado en el mismo objeto; fue deshabilitado para evitar conflicto de vida/barra.", this);

            legacyEnemyHealth.enabled = false;
        }

        EnemyVisuals legacyEnemyVisuals = GetComponent<EnemyVisuals>();
        if (legacyEnemyVisuals != null)
        {
            legacyEnemyVisuals.enabled = false;
        }
    }

    private static bool TryReadField<T>(object source, string fieldName, out T value)
    {
        value = default;
        if (source == null || string.IsNullOrWhiteSpace(fieldName)) return false;

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = source.GetType().GetField(fieldName, Flags);
        if (field == null) return false;

        object rawValue = field.GetValue(source);
        if (!(rawValue is T castValue)) return false;

        value = castValue;
        return true;
    }

    private void CleanupEntryDialogueIfInterrupted()
    {
        entryDialogueSequenceStarted = false;
        RestoreEntryDialogueGameplay();

        if (entryDialogueRoot != null)
        {
            entryDialogueRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        CleanupEntryDialogueIfInterrupted();

        if (protectorMoving != null)
        {
            protectorMoving.OnEntrySequenceFinished -= HandleEntrySequenceFinished;
        }

        if (!keepDeathCameraLockedDuringSceneTransition)
        {
            RestoreDeathCameraMovement();
        }
        if (!IsDead) StopDeathPlayable();
    }

    private void OnDestroy()
    {
        CleanupEntryDialogueIfInterrupted();

        if (activeHealthBar != null) Destroy(activeHealthBar.gameObject);
        if (entryDialogueRoot != null) Destroy(entryDialogueRoot);
        if (!keepDeathCameraLockedDuringSceneTransition)
        {
            RestoreDeathCameraMovement();
        }
        StopDeathPlayable();
    }
}

internal sealed class ProtectorDeathTransitionRunner : MonoBehaviour
{
    private bool isRunning;

    public void Begin(
        Image overlayImage,
        float fadeDuration,
        int fadeSteps,
        float blackHoldSeconds,
        string targetSceneName,
        float nextSceneFadeInDuration,
        int nextSceneFadeInSteps,
        CamaraMovement cameraMovementToRestore)
    {
        if (isRunning) return;
        isRunning = true;

        StartCoroutine(RunTransition(
            overlayImage,
            Mathf.Max(0.05f, fadeDuration),
            Mathf.Max(2, fadeSteps),
            Mathf.Max(0f, blackHoldSeconds),
            targetSceneName,
            Mathf.Max(0.1f, nextSceneFadeInDuration),
            Mathf.Max(2, nextSceneFadeInSteps),
            cameraMovementToRestore));
    }

    private IEnumerator RunTransition(
        Image overlayImage,
        float fadeDuration,
        int fadeSteps,
        float blackHoldSeconds,
        string targetSceneName,
        float nextSceneFadeInDuration,
        int nextSceneFadeInSteps,
        CamaraMovement cameraMovementToRestore)
    {
        // Permite que la destruccion del Protector se procese antes de iniciar el fade.
        yield return null;
        yield return FadeOverlayToAlpha(overlayImage, 1f, fadeDuration, fadeSteps);

        if (blackHoldSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldSeconds);
        }

        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("Protector: victorySceneName está vacío; no se pudo cambiar de escena.");
            RestoreCameraMovement(cameraMovementToRestore);
            Destroy(gameObject);
            yield break;
        }

        GameManager.ConfigureNextSceneIntroFade(nextSceneFadeInDuration, nextSceneFadeInSteps);

        try
        {
            int sceneIndex = ResolveBuildSceneIndexByName(targetSceneName);
            if (sceneIndex >= 0)
            {
                SceneManager.LoadScene(sceneIndex);
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"Protector: no se pudo cargar la escena '{targetSceneName}'. {exception.Message}");
            RestoreCameraMovement(cameraMovementToRestore);
            Destroy(gameObject);
        }
    }

    private static IEnumerator FadeOverlayToAlpha(Image overlayImage, float targetAlpha, float duration, int steps)
    {
        if (overlayImage == null) yield break;

        Color baseColor = overlayImage.color;
        float startAlpha = baseColor.a;
        float clampedTargetAlpha = Mathf.Clamp01(targetAlpha);

        if (duration <= 0.0001f)
        {
            overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, clampedTargetAlpha);
            yield break;
        }

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float rawT = Mathf.Clamp01(timer / duration);
            float steppedT = Mathf.Floor(rawT * steps) / steps;
            float alpha = Mathf.Lerp(startAlpha, clampedTargetAlpha, steppedT);
            overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }

        overlayImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, clampedTargetAlpha);
    }

    private static int ResolveBuildSceneIndexByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return -1;

        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(path)) continue;

            string buildSceneName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(buildSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void RestoreCameraMovement(CamaraMovement cameraMovementToRestore)
    {
        if (cameraMovementToRestore == null) return;
        cameraMovementToRestore.enabled = true;
    }
}
