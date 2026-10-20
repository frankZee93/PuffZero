using System.Collections;
using UnityEngine;

public class SplashScreenController : MonoBehaviour
{
    public float displayTime = 2f;
    public float fadeDuration = 0.5f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Start()
    {
        StartCoroutine(SplashRoutine());
    }

    IEnumerator SplashRoutine()
    {
        // Wait for the full display time (e.g., 2 seconds)
        yield return new WaitForSeconds(displayTime);

        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}