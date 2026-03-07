using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Letter : MonoBehaviour
{
    [SerializeField] private Collider2D letterCollider;
    [SerializeField] private bool requirePlayerAbove = true;
    [SerializeField] private float minPlayerYFromLetterCenter = -0.05f;
    [SerializeField] private bool disableColliderAfterTrigger = true;

    private bool hasTriggered;

    private void Awake()
    {
        if (letterCollider == null)
        {
            letterCollider = GetComponent<Collider2D>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTriggerEnding(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryTriggerEnding(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryTriggerEnding(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryTriggerEnding(collision.collider);
    }

    private void TryTriggerEnding(Collider2D other)
    {
        if (hasTriggered || other == null) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        if (requirePlayerAbove)
        {
            float minY = transform.position.y + minPlayerYFromLetterCenter;
            if (player.transform.position.y < minY) return;
        }

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            gm = FindFirstObjectByType<GameManager>();
        }

        if (gm == null) return;
        if (!gm.TriggerLetterEnding(player.transform)) return;

        hasTriggered = true;
        if (disableColliderAfterTrigger && letterCollider != null)
        {
            letterCollider.enabled = false;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (letterCollider == null)
        {
            letterCollider = GetComponent<Collider2D>();
        }
    }
#endif
}
