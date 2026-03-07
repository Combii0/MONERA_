using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class EnemyBullet : MonoBehaviour
{
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private bool destroyOnWorldHit = true;

    private void Start()
    {
        Destroy(gameObject, lifetime);
        
        // Ensure it's a trigger so it doesn't push the player physically
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
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
