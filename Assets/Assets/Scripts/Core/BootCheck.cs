using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BootCheck : MonoBehaviour
{
    [Header("UI References")]
    public GameObject onboardingRoot; // Add this to hide it on launch

    void Start()
    {
        NotificationScheduler.Initialize();

        if (UserData.Onboarded)
        {
            // If already onboarded, instantly hide the onboarding UI 
            // so it doesn't flash behind the splash screen.
            if (onboardingRoot != null)
            {
                onboardingRoot.SetActive(false);
            }

            StartCoroutine(LoadMainAppDelayed());
        }
    }

    IEnumerator LoadMainAppDelayed()
    {
        // Wait for the splash screen to display and fade out
        yield return new WaitForSeconds(2.5f);

        Debug.Log("User already onboarded. Loading MainApp...");
        SceneManager.LoadScene("MainApp");
    }
}