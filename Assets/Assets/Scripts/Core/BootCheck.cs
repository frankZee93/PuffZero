using UnityEngine;
using UnityEngine.SceneManagement;

public class BootCheck : MonoBehaviour
{
    void Start()
    {
        // Initialize notifications (request iOS permission, register Android channel).
        NotificationScheduler.Initialize();

        if (UserData.Onboarded)
        {
            Debug.Log("User already onboarded. Loading MainApp...");
            SceneManager.LoadScene("MainApp");
        }
    }
}