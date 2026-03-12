using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class EnemyBullet : MonoBehaviour
{
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private bool destroyOnWorldHit = true;
    [SerializeField] private bool ignoreProtectorCollision = true;
    [SerializeField] private bool forceRenderOnTop = true;
    [SerializeField] private int projectileSortingOrder = 120;

    private Collider2D bulletCollider;
    private SpriteRenderer[] projectileRenderers;

    private void Awake()
    {
        bulletCollider = GetComponent<Collider2D>();
        projectileRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        ApplyRenderPriority();
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
        
        // Ensure it's a trigger so it doesn't push the player physically
        if (bulletCollider != null) bulletCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        if (ignoreProtectorCollision && other.GetComponentInParent<Protector>() != null)
        {
            return;
        }

        // 1. Check if it hit the player
        if (MatchesTagSafe(other.gameObject, "Player") || other.GetComponentInParent<PlayerMovement>() != null)
        {
            // Note: Since you use GameManager for player damage, we find it here
            GameManager gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                // Trigger the standard damage pipeline
                gm.TryDamagePlayer(transform.position, 6f, 2f, 0.4f);
            }
            Destroy(gameObject);
            return;
        }

        // 2. Destroy on walls (tilemap), but ignore other enemies
        if (destroyOnWorldHit)
        {
            // If it's not an enemy and not a trigger, it's likely a wall/floor
            if (other.GetComponentInParent<EnemyHealth>() == null && !other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }

    private void ApplyRenderPriority()
    {
        if (!forceRenderOnTop || projectileRenderers == null) return;

        int desiredOrder = Mathf.Max(0, projectileSortingOrder);
        for (int i = 0; i < projectileRenderers.Length; i++)
        {
            SpriteRenderer renderer = projectileRenderers[i];
            if (renderer == null) continue;

            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, desiredOrder);
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
            return false;
        }
    }
}
