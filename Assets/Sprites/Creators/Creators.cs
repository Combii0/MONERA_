using UnityEngine;

public class Creators : MonoBehaviour
{
    void Update()
    {
        if (transform.position.y <= -9f)
        {
            Destroy(gameObject);
        }
    }
}
