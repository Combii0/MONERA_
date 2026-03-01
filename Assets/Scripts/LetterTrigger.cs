using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LetterTrigger : MonoBehaviour
{
    [SerializeField] private Collider2D triggerCollider;
    [SerializeField] private bool disableColliderAfterTrigger = true;

    private bool hasTriggered;

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryTriggerLevelEnd(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryTriggerLevelEnd(collision.collider);
    }

    private void TryTriggerLevelEnd(Collider2D other)
    {
        if (hasTriggered || other == null) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            gm = FindFirstObjectByType<GameManager>();
        }

        if (gm == null) return;
        if (!gm.TriggerLetterEnding(player.transform)) return;

        hasTriggered = true;
        if (disableColliderAfterTrigger && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureTriggerCollider();
    }
#endif
}
