using UnityEngine;

public class combi : MonoBehaviour
{
    void Update()
    {
        if (transform.position.y <= -9f)
        {
            Destroy(gameObject);
        }
    }
}
