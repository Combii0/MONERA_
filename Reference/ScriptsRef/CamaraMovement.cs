using UnityEngine;

public class CamaraMovement : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform player;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.6f, -10f);

    [Header("Suavizado")]
    [SerializeField] private float smoothTimeX = 0.10f;
    [SerializeField] private float smoothTimeY = 0.16f;
    [SerializeField] private float maxSpeedX = 40f;
    [SerializeField] private float maxSpeedY = 30f;

    [Header("Comodidad")]
    [SerializeField] private Vector2 deadZoneSize = new Vector2(1.6f, 1.0f);
    [SerializeField] private float lookAheadDistance = 1.25f;
    [SerializeField] private float lookAheadThreshold = 0.08f;
    [SerializeField] private float lookAheadLerpSpeed = 7f;

    [Header("Limites")]
    [SerializeField] private bool clampMinY = true;
    [SerializeField] private float minCameraY = -10.5472f;

    private Rigidbody2D playerRb;
    private Vector3 focusPoint;
    private float lookAheadCurrent;
    private float velocityX;
    private float velocityY;

    private void Awake()
    {
        ResolvePlayer();
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
        float newX = Mathf.SmoothDamp(transform.position.x, desired.x, ref velocityX, Mathf.Max(0.01f, smoothTimeX), maxSpeedX, Time.deltaTime);
        float newY = Mathf.SmoothDamp(transform.position.y, desired.y, ref velocityY, Mathf.Max(0.01f, smoothTimeY), maxSpeedY, Time.deltaTime);
        if (clampMinY)
        {
            newY = Mathf.Max(minCameraY, newY);
        }

        transform.position = new Vector3(newX, newY, desired.z);
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null) return;

        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.45f);
        Vector3 center = Application.isPlaying ? focusPoint : player.position;
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0f), new Vector3(deadZoneSize.x, deadZoneSize.y, 0f));
    }
}
