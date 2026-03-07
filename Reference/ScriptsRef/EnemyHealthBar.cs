using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Transform fillTransform;
    [SerializeField] private float barWidth = 0.7f;
    
    private Transform targetToFollow;
    private float yOffset;

    public void Setup(Transform target, float offset)
    {
        targetToFollow = target;
        yOffset = offset;
    }

    private void LateUpdate()
    {
        if (targetToFollow != null)
        {
            transform.position = targetToFollow.position + new Vector3(0, yOffset, 0);
        }
    }

    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        float ratio = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, maxHealth));
        
        fillTransform.localScale = new Vector3(barWidth * ratio, fillTransform.localScale.y, 1f);
        fillTransform.localPosition = new Vector3(-(barWidth * (1f - ratio) * 0.5f), 0f, 0f);
        fillTransform.gameObject.SetActive(ratio > 0f);
    }
}