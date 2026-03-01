using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerProjectile : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifeTime = 2.5f;
    [SerializeField] private bool destroyOnWorldHit = true;

    private bool hasHit;

    public void Initialize(int projectileDamage, float projectileLifeTime)
    {
        damage = Mathf.Max(1, projectileDamage);
        lifeTime = Mathf.Max(0.05f, projectileLifeTime);
        Destroy(gameObject, lifeTime);
    }

    private void OnEnable()
    {
        Destroy(gameObject, Mathf.Max(0.05f, lifeTime));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHandleImpact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleImpact(collision.collider);
    }

    private void TryHandleImpact(Collider2D other)
    {
        if (hasHit || other == null) return;

        EnemyMovement enemy = other.GetComponentInParent<EnemyMovement>();
        if (enemy != null)
        {
            hasHit = true;
            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (!destroyOnWorldHit) return;
        if (other.isTrigger) return;

        hasHit = true;
        Destroy(gameObject);
    }
}
