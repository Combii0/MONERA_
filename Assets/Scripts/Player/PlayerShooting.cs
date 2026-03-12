using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AudioSource))]
public class PlayerShooting : MonoBehaviour
{
    [Header("Disparo")]
    [SerializeField] private bool enableShooting = true;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Sprite projectileSprite;
    [SerializeField, HideInInspector] private float projectileSpeed = 14f;
    [SerializeField, HideInInspector] private int projectileDamage = 1;
    [SerializeField, HideInInspector] private float projectileLifetime = 2.5f;
    [SerializeField] private float shootCooldown = 0.2f;
    [SerializeField, HideInInspector] private Vector2 shootSpawnOffset = new Vector2(0.55f, 0.05f);
    [SerializeField, HideInInspector] private float projectileColliderRadius = 0.12f;
    [SerializeField, HideInInspector] private float projectileScale = 1f;
    [SerializeField, HideInInspector] private int projectileSortingOrderOffset = 2;
    [SerializeField, HideInInspector] private Color projectileColor = Color.white;

    [Header("Audio Disparo")]
    [FormerlySerializedAs("shootSfx")]
    [SerializeField] private AudioClip projectileShootSfx;
    [FormerlySerializedAs("shootSfxVolume")]
    [SerializeField, Range(0f, 1f), HideInInspector] private float projectileShootSfxVolume = 0.9f;

    private InputAction shootAction;
    private float nextShootTime;
    private Transform projectileParent;

    private PlayerMovement playerMovement;
    private SpriteRenderer spriteRenderer;
    private AudioSource sfxSource;
    private Collider2D[] playerColliders;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        sfxSource = GetComponent<AudioSource>();
        playerColliders = GetComponents<Collider2D>();

        BuildInputActions();
    }

    private void BuildInputActions()
    {
        shootAction = new InputAction(name: "Shoot", type: InputActionType.Button);
        shootAction.AddBinding("<Mouse>/leftButton");
        shootAction.AddBinding("<Keyboard>/j");
        shootAction.AddBinding("<Keyboard>/k");
        shootAction.AddBinding("<Gamepad>/buttonWest");
        shootAction.AddBinding("<Gamepad>/rightTrigger");
    }

    private void OnEnable() => shootAction?.Enable();
    private void OnDisable() => shootAction?.Disable();
    private void OnDestroy() => shootAction?.Dispose();

    private void Update()
    {
        TryShoot();
    }

    private void TryShoot()
    {
        if (!enableShooting || shootAction == null || !shootAction.WasPressedThisFrame() || Time.time < nextShootTime) return;

        nextShootTime = Time.time + Mathf.Max(0.02f, shootCooldown);
        SpawnProjectile(GetShootDirection());
        PlaySfx(projectileShootSfx, projectileShootSfxVolume);
    }

    private Vector2 GetShootDirection()
    {
        if (TryGetPointerWorldPosition(out Vector3 pointerWorldPosition))
        {
            Vector2 toPointer = (Vector2)(pointerWorldPosition - transform.position);
            if (toPointer.sqrMagnitude > 0.0001f)
            {
                return toPointer.normalized;
            }
        }

        int facing = playerMovement.FacingDirection == 0 ? 1 : playerMovement.FacingDirection;
        return new Vector2(facing, 0f);
    }

    private bool TryGetPointerWorldPosition(out Vector3 pointerWorldPosition)
    {
        pointerWorldPosition = default;
        if (Mouse.current == null) return false;
        Camera activeCamera = Camera.main;
        if (activeCamera == null) return false;

        Vector2 pointerScreen = Mouse.current.position.ReadValue();
        if (!activeCamera.pixelRect.Contains(pointerScreen)) return false;

        float cameraDistance = Mathf.Abs(activeCamera.transform.position.z - transform.position.z);
        pointerWorldPosition = activeCamera.ScreenToWorldPoint(new Vector3(pointerScreen.x, pointerScreen.y, cameraDistance));
        pointerWorldPosition.z = transform.position.z;
        return true;
    }

    private void SpawnProjectile(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            int facing = playerMovement.FacingDirection == 0 ? 1 : playerMovement.FacingDirection;
            direction = new Vector2(facing, 0f);
        }

        direction.Normalize();
        Vector3 spawnOffset = (Vector3)(direction * shootSpawnOffset.x) + new Vector3(0f, shootSpawnOffset.y, 0f);
        Vector3 spawnPosition = transform.position + spawnOffset;

        bool usingPrefab = projectilePrefab != null;
        GameObject projectileObj = usingPrefab
            ? Instantiate(projectilePrefab, spawnPosition, Quaternion.identity)
            : new GameObject("PlayerProjectile");

        // Agrupar en la carpeta PlayerProjectiles
        if (projectileParent == null)
        {
            GameObject parentObj = GameObject.Find("PlayerProjectiles");
            if (parentObj == null)
            {
                parentObj = new GameObject("PlayerProjectiles");
            }
            projectileParent = parentObj.transform;
        }
        projectileObj.transform.SetParent(projectileParent);
        
        if (!usingPrefab)
        {
            projectileObj.transform.position = spawnPosition;
            projectileObj.transform.localScale = Vector3.one * Mathf.Max(0.05f, projectileScale);
        }

        SpriteRenderer projectileRenderer = projectileObj.GetComponent<SpriteRenderer>();
        if (projectileRenderer == null) projectileRenderer = projectileObj.AddComponent<SpriteRenderer>();

        if (!usingPrefab || projectileSprite != null)
        {
            projectileRenderer.sprite = projectileSprite != null ? projectileSprite : spriteRenderer.sprite;
        }
        if (!usingPrefab)
        {
            projectileRenderer.color = projectileColor;
        }
        projectileRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        projectileRenderer.sortingOrder = spriteRenderer.sortingOrder + projectileSortingOrderOffset;

        CircleCollider2D projectileCollider = projectileObj.GetComponent<CircleCollider2D>();
        if (projectileCollider == null) projectileCollider = projectileObj.AddComponent<CircleCollider2D>();
        projectileCollider.isTrigger = true;
        
        if (!usingPrefab)
        {
            projectileCollider.radius = Mathf.Max(0.01f, projectileColliderRadius);
        }

        Rigidbody2D projectileRb = projectileObj.GetComponent<Rigidbody2D>();
        if (projectileRb == null) projectileRb = projectileObj.AddComponent<Rigidbody2D>();
        projectileRb.bodyType = RigidbodyType2D.Kinematic;
        projectileRb.gravityScale = 0f;
        projectileRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        projectileRb.linearVelocity = direction * Mathf.Max(0.1f, projectileSpeed);

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D ownCollider = playerColliders[i];
            if (ownCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, ownCollider, true);
            }
        }

        PlayerProjectile projectile = projectileObj.GetComponent<PlayerProjectile>();
        if (projectile == null) projectile = projectileObj.AddComponent<PlayerProjectile>();
        projectile.Initialize(Mathf.Max(1, projectileDamage), Mathf.Max(0.05f, projectileLifetime));
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (projectileSprite == null)
        {
            projectileSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Attacks/powerOrb.png");
        }
    }
#endif
}