using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PendingPuffsController : MonoBehaviour
{
    public GameObject pendingPanel;
    public TMP_Text pendingText;
    public Button declineAllButton;
    public Button smokeAllButton;

    private MainAppController mainApp;
    private int pendingCount;

    void Start()
    {
        mainApp = FindFirstObjectByType<MainAppController>();
        if (mainApp == null) { Debug.LogError("MainAppController not found."); return; }

        declineAllButton.onClick.AddListener(OnDeclineAll);
        smokeAllButton.onClick.AddListener(OnSmokeAll);
        pendingPanel.SetActive(false);
    }

    public void ShowPendingPanel(int count)
    {
        pendingCount = count;
        pendingText.text = $"You have {pendingCount} unaccounted-for puff{(pendingCount > 1 ? "s" : "")}. What would you like to do?";
        pendingPanel.SetActive(true);
    }

    public bool IsPanelActive()
    {
        return pendingPanel != null && pendingPanel.activeSelf;
    }

    void OnDeclineAll()
    {
        mainApp.ResolveAllPending(false);
        pendingPanel.SetActive(false);
    }

    void OnSmokeAll()
    {
        mainApp.ResolveAllPending(true);
        pendingPanel.SetActive(false);
    }
}