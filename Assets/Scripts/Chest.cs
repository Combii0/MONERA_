using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class Chest : MonoBehaviour
{
    [Header("Cofre")]
    [SerializeField] private SpriteRenderer chestRenderer;
    [SerializeField] private Collider2D triggerCollider;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openedSprite;
    [SerializeField] private int lifeReward = 1;
    [SerializeField] private bool requirePlayerToPassAbove = true;
    [SerializeField] private float minPlayerYFromChestCenter = -0.05f;

    private bool isOpened;

    private void Awake()
    {
        if (chestRenderer == null)
        {
            chestRenderer = GetComponent<SpriteRenderer>();
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (closedSprite == null && chestRenderer != null)
        {
            closedSprite = chestRenderer.sprite;
        }

        if (chestRenderer != null && closedSprite != null)
        {
            chestRenderer.sprite = closedSprite;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryOpen(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryOpen(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryOpen(collision.collider);
    }

    private void TryOpen(Collider2D other)
    {
        if (isOpened || other == null) return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        if (requirePlayerToPassAbove)
        {
            float minY = transform.position.y + minPlayerYFromChestCenter;
            if (player.transform.position.y < minY)
            {
                // Allow opening from near side contact to avoid strict pivot-dependent misses.
                float horizontalDistance = Mathf.Abs(player.transform.position.x - transform.position.x);
                if (horizontalDistance > 0.95f) return;
            }
        }

        OpenChest();
    }

    private void OpenChest()
    {
        isOpened = true;
        if (chestRenderer != null && openedSprite != null)
        {
            chestRenderer.sprite = openedSprite;
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            gm = FindFirstObjectByType<GameManager>();
        }

        if (gm != null)
        {
            gm.TryAddLife(Mathf.Max(1, lifeReward));
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (chestRenderer == null)
        {
            chestRenderer = GetComponent<SpriteRenderer>();
        }

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (closedSprite == null && chestRenderer != null)
        {
            closedSprite = chestRenderer.sprite;
        }

        if (openedSprite == null)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Spritesheets/chest.png");
            for (int i = 0; i < assets.Length; i++)
            {
                Sprite sprite = assets[i] as Sprite;
                if (sprite == null) continue;
                if (sprite.name == "chest_1")
                {
                    openedSprite = sprite;
                    break;
                }
            }
        }
    }
#endif
}
