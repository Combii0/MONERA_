using UnityEngine;

public class CamaraMovement : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.6f, -10f);

    [Header("Suavizado")]
    [SerializeField] private float smoothTimeX = 0.10f;
    [SerializeField] private float smoothTimeY = 0.16f;
    [SerializeField, HideInInspector] private float maxSpeedX = 40f;
    [SerializeField, HideInInspector] private float maxSpeedY = 30f;
    [SerializeField, HideInInspector] private float stopSmoothingRadiusX = 1.2f;
    [SerializeField, HideInInspector] private float stopSmoothingRadiusY = 1.1f;
    [SerializeField, Range(0.1f, 1f), HideInInspector] private float stopSpeedMultiplier = 0.42f;

    [Header("Comodidad")]
    [SerializeField] private Vector2 deadZoneSize = new Vector2(1.6f, 1.0f);
    [SerializeField, HideInInspector] private float lookAheadDistance = 1.25f;
    [SerializeField, HideInInspector] private float lookAheadThreshold = 0.08f;
    [SerializeField, HideInInspector] private float lookAheadLerpSpeed = 7f;

    [Header("Limites")]
    [SerializeField] private bool clampMinY = true;
    [SerializeField] private float minCameraY = -10.5472f;
    [SerializeField, HideInInspector] private bool useDeathYAsMinCameraY = true;
    [SerializeField, HideInInspector] private float minCameraYOffsetFromDeath = 0f;

    private Rigidbody2D playerRb;
    private Vector3 focusPoint;
    private float lookAheadCurrent;
    private float velocityX;
    private float velocityY;
    private bool dynamicMinYResolved;

    private void Awake()
    {
        ResolvePlayer();
        ResolveDynamicMinY();
    }

    private void Start()
    {
        if (player == null) return;

        focusPoint = player.position;
        Vector3 startPos = focusPoint + offset;
        if (clampMinY)
        {
            startPos.y = Mathf.Max(minCameraY, startPos.y);
        }
        transform.position = startPos;
    }

    private void LateUpdate()
    {
        ResolveDynamicMinY();
        if (player == null) return;

        UpdateLookAhead();
        UpdateFocusPoint();
        UpdateCameraPosition();
    }

    private void ResolvePlayer()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (player != null) playerRb = player.GetComponent<Rigidbody2D>();
    }

    private void ResolveDynamicMinY()
    {
        if (dynamicMinYResolved || !useDeathYAsMinCameraY) return;

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null) return;

        clampMinY = true;
        minCameraY = gm.InstantDeathY + offset.y + minCameraYOffsetFromDeath;
        dynamicMinYResolved = true;
    }

    private void UpdateLookAhead()
    {
        float targetLookAhead = 0f;
        float horizontalSpeed = playerRb != null ? playerRb.linearVelocity.x : 0f;

        if (Mathf.Abs(horizontalSpeed) > lookAheadThreshold)
        {
            targetLookAhead = Mathf.Sign(horizontalSpeed) * lookAheadDistance;
        }

        lookAheadCurrent = Mathf.Lerp(lookAheadCurrent, targetLookAhead, lookAheadLerpSpeed * Time.deltaTime);
    }

    private void UpdateFocusPoint()
    {
        Vector3 playerPos = player.position;
        playerPos.x += lookAheadCurrent;

        float halfDeadX = Mathf.Max(0.01f, deadZoneSize.x * 0.5f);
        float halfDeadY = Mathf.Max(0.01f, deadZoneSize.y * 0.5f);

        float deltaX = playerPos.x - focusPoint.x;
        float deltaY = playerPos.y - focusPoint.y;

        if (Mathf.Abs(deltaX) > halfDeadX)
        {
            focusPoint.x += deltaX - (Mathf.Sign(deltaX) * halfDeadX);
        }

        if (Mathf.Abs(deltaY) > halfDeadY)
        {
            focusPoint.y += deltaY - (Mathf.Sign(deltaY) * halfDeadY);
        }
    }

    private void UpdateCameraPosition()
    {
        Vector3 desired = focusPoint + offset;
        float dynamicMaxSpeedX = ComputeStopSmoothedMaxSpeed(maxSpeedX, Mathf.Abs(desired.x - transform.position.x), stopSmoothingRadiusX);
        float dynamicMaxSpeedY = ComputeStopSmoothedMaxSpeed(maxSpeedY, Mathf.Abs(desired.y - transform.position.y), stopSmoothingRadiusY);

        float newX = Mathf.SmoothDamp(transform.position.x, desired.x, ref velocityX, Mathf.Max(0.01f, smoothTimeX), dynamicMaxSpeedX, Time.deltaTime);
        float newY = Mathf.SmoothDamp(transform.position.y, desired.y, ref velocityY, Mathf.Max(0.01f, smoothTimeY), dynamicMaxSpeedY, Time.deltaTime);
        if (clampMinY)
        {
            newY = Mathf.Max(minCameraY, newY);
        }

        transform.position = new Vector3(newX, newY, desired.z);
    }

    private float ComputeStopSmoothedMaxSpeed(float baseSpeed, float distanceToTarget, float radius)
    {
        float speed = Mathf.Max(0.01f, baseSpeed);
        float safeRadius = Mathf.Max(0.001f, radius);
        if (distanceToTarget >= safeRadius)
        {
            return speed;
        }

        float t = Mathf.Clamp01(distanceToTarget / safeRadius);
        float easedT = t * t * (3f - (2f * t));
        float minSpeed = speed * Mathf.Clamp(stopSpeedMultiplier, 0.05f, 1f);
        return Mathf.Lerp(minSpeed, speed, easedT);
    }

    public void SnapToPlayerImmediate()
    {
        if (player == null)
        {
            ResolvePlayer();
            if (player == null) return;
        }

        Vector3 playerPos = player.position;
        playerPos.x += lookAheadCurrent;
        focusPoint = playerPos;

        Vector3 desired = focusPoint + offset;
        if (clampMinY)
        {
            desired.y = Mathf.Max(minCameraY, desired.y);
        }

        velocityX = 0f;
        velocityY = 0f;
        transform.position = desired;
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.45f);
        Vector3 center = Application.isPlaying ? focusPoint : player.position;
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(deadZoneSize.x, deadZoneSize.y, 0f));
    }
}
