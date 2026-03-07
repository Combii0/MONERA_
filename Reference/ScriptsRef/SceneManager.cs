using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    private int actualScene;

    void Awake()
    {
        actualScene = SceneManager.GetActiveScene().buildIndex;
    }

    public void Play()
    {
        TriggerTransition(1);
    }

    public void Restart()
    {
        TriggerTransition(actualScene);
    }

    public void Menu()
    {
        TriggerTransition(0);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {       
            int nextScene = actualScene + 1;
            if (nextScene < SceneManager.sceneCountInBuildSettings)
            {
                TriggerTransition(nextScene);
            }
            else
            {
                Debug.LogWarning("No more scenes in Build Settings!");
                Menu(); 
            }
        }
    }

    // New helper method to talk to the GameManager
    private void TriggerTransition(int sceneIndex)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TransitionToScene(sceneIndex);
        }
        else
        {
            // Fallback: Instantly load if playing a scene without the GameManager
            SceneManager.LoadScene(sceneIndex);
        }
    }
}