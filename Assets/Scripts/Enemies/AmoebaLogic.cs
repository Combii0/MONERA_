using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyHealth))]
public class AmoebaLogic : MonoBehaviour
{
    [Header("Amoeba Type")]
    [Tooltip("If true, the amoeba stays still and shoots toward the player.")]
    public bool isSmall = true;

    [Header("Detection")]
    [SerializeField] private float activationRadius = 8f;
    private Transform playerTarget;
    private bool isActive = false;

    [Header("Shooting Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 5f;
    [SerializeField] private float burstInterval = 3f;
    [SerializeField] private int bulletsPerBurst = 3; 
    [SerializeField] private float timeBetweenShots = 0.2f; // Delay between sequential bullets
    [SerializeField] private float shootSpawnOffset = 0.5f;

    private EnemyHealth health;
    private float burstTimer;
    private bool isShootingBurst = false;
    
    private static Transform projectilesContainer;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) playerTarget = playerObj.transform;

        EnsureContainerExists();
    }

    private void EnsureContainerExists()
    {
        if (projectilesContainer == null)
        {
            GameObject containerObj = GameObject.Find("EnemyProjectiles");
            if (containerObj == null)
            {
                containerObj = new GameObject("EnemyProjectiles");
            }
            projectilesContainer = containerObj.transform;
        }
    }

    private void Update()
    {
        if (health.isDead || playerTarget == null) return;

        float distance = Vector2.Distance(transform.position, playerTarget.position);
        isActive = distance <= activationRadius;

        if (isSmall && isActive && !isShootingBurst)
        {
            HandleBurstTimer();
        }
    }

    private void HandleBurstTimer()
    {
        burstTimer += Time.deltaTime;

        if (burstTimer >= burstInterval)
        {
            StartCoroutine(ShootBurstRoutine());
            burstTimer = 0f;
        }
    }

    private IEnumerator ShootBurstRoutine()
    {
        isShootingBurst = true;

        for (int i = 0; i < bulletsPerBurst; i++)
        {
            // If the enemy dies while shooting the burst, stop firing
            if (health.isDead || playerTarget == null) yield break;

            ShootSingleBulletAtPlayer();
            
            // Wait for the specific sequence delay
            yield return new WaitForSeconds(timeBetweenShots);
        }

        isShootingBurst = false;
    }

    private void ShootSingleBulletAtPlayer()
    {
        if (bulletPrefab == null || playerTarget == null) return;
        if (projectilesContainer == null) EnsureContainerExists();

        Vector2 bulletDir = (playerTarget.position - transform.position).normalized;
        Vector3 spawnPos = transform.position + (Vector3)bulletDir * shootSpawnOffset;
        
        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity, projectilesContainer);

        Rigidbody2D rb = bullet.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = bulletDir * bulletSpeed;

        float rotAngle = Mathf.Atan2(bulletDir.y, bulletDir.x) * Mathf.Rad2Deg;
        bullet.transform.rotation = Quaternion.AngleAxis(rotAngle, Vector3.forward);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, activationRadius);
    }
}