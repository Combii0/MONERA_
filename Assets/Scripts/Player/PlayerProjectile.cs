using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerProjectile : MonoBehaviour
{
    [SerializeField, HideInInspector] private int damage = 1;
    [SerializeField, HideInInspector] private float lifeTime = 2.5f;
    [SerializeField] private bool destroyOnWorldHit = true;

    private bool hasHit;

    public void Initialize(int projectileDamage, float projectileLifeTime)
    {
        damage = Mathf.Max(1, projectileDamage);
        lifeTime = Mathf.Max(0.05f, projectileLifeTime);
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
        
        // Evitamos que el proyectil intente dañar al propio pingüino, Rojitas, me encanta como agarras el script del pingüino, 10/10
        if (other.CompareTag("Player")) return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        Protector protector = other.GetComponentInParent<Protector>();
        if (enemy != null || protector != null)
        {
            return;
        }

        if (!destroyOnWorldHit) return;
        if (other.isTrigger) return;

        hasHit = true;
        Destroy(gameObject);
    }

    public bool TryConsumeImpactDamage(out int impactDamage)
    {
        impactDamage = 0;
        if (hasHit) return false;

        hasHit = true;
        impactDamage = Mathf.Max(1, damage);
        Destroy(gameObject);
        return true;
    }
}
