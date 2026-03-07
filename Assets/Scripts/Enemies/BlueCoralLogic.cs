using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))] 
public class BlueCoralLogic : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Si es falso, el coral se moverá desde el inicio.")]
    [SerializeField] private bool requireDetection = true;
    [SerializeField] private float activationRadius = 8f;
    
    [Tooltip("Puedes ver si el enemigo te ha detectado aquí.")]
    [SerializeField] private bool isActive = false;

    [Header("Orbit Settings")]
    [SerializeField] private Transform playerTarget; 
    [SerializeField] private float circularFollowDelay = 1.5f; 
    [SerializeField] private float circularOrbitSpeed = 1f;    
    [SerializeField] private Vector2 radiusRange = new Vector2(2f, 5f); 
    [SerializeField] private float radiusTransitionSpeed = 1.5f; 
    [SerializeField] private float centerFollowSmoothness = 2.5f;

    private Rigidbody2D rb;
    private EnemyHealth health;
    private Queue<float> targetHistoryX = new Queue<float>();
    private float currentCircularRadius;
    private float targetCircularRadius; 
    private float currentOrbitAngle;
    private Vector2 currentBlueCoralCenter;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        if (health == null)
        {
            health = gameObject.AddComponent<EnemyHealth>();
        }
        
        currentBlueCoralCenter = rb.position;
        targetCircularRadius = UnityEngine.Random.Range(radiusRange.x, radiusRange.y);
        currentCircularRadius = targetCircularRadius;

        if (playerTarget == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerTarget = playerObj.transform;
        }

        if (!requireDetection)
        {
            isActive = true;
        }
    }

    private void Update()
    {
        if (health.isDead || playerTarget == null) return;

        if (requireDetection && !isActive)
        {
            if (Vector2.Distance(transform.position, playerTarget.position) <= activationRadius)
            {
                isActive = true;
            }
        }
    }

    private void FixedUpdate()
    {
        // Don't move until active
        if (health.isDead || playerTarget == null || !isActive) return;

        targetHistoryX.Enqueue(playerTarget.position.x);

        int framesToDelay = Mathf.RoundToInt(circularFollowDelay / Time.fixedDeltaTime);
        float targetCenterX = playerTarget.position.x;

        if (targetHistoryX.Count > framesToDelay) targetCenterX = targetHistoryX.Dequeue();
        else if (targetHistoryX.Count > 0) targetCenterX = targetHistoryX.Peek();

        Vector2 targetCenter = new Vector2(targetCenterX, playerTarget.position.y);
        
        currentBlueCoralCenter = Vector2.Lerp(currentBlueCoralCenter, targetCenter, centerFollowSmoothness * Time.fixedDeltaTime);
        currentCircularRadius = Mathf.Lerp(currentCircularRadius, targetCircularRadius, radiusTransitionSpeed * Time.fixedDeltaTime);
        currentOrbitAngle += circularOrbitSpeed * Time.fixedDeltaTime;

        float nextX = currentBlueCoralCenter.x + Mathf.Cos(currentOrbitAngle) * currentCircularRadius;
        float nextY = currentBlueCoralCenter.y + Mathf.Sin(currentOrbitAngle) * currentCircularRadius;

        rb.MovePosition(new Vector2(nextX, nextY));

        if (currentOrbitAngle >= Mathf.PI * 2f)
        {
            currentOrbitAngle -= Mathf.PI * 2f;
            targetCircularRadius = UnityEngine.Random.Range(radiusRange.x, radiusRange.y);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (requireDetection)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}
