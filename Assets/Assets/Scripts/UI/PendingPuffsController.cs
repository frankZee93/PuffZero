using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PendingPuffsController : MonoBehaviour
{
    public GameObject pendingPanel;
    public TMP_Text pendingText;
    public Slider puffSlider;
    public TMP_Text sliderPreviewText;
    public Button confirmButton;
    public Button accountIndividuallyButton;

    private MainAppController mainApp;
    private int pendingCount;

    void Start()
    {
        mainApp = FindFirstObjectByType<MainAppController>();
        if (mainApp == null) { Debug.LogError("MainAppController not found."); return; }

        confirmButton.onClick.AddListener(OnConfirm);
        accountIndividuallyButton.onClick.AddListener(OnAccountIndividually);
        puffSlider.onValueChanged.AddListener(OnSliderChanged);

        pendingPanel.SetActive(false);
    }

    public void ShowPendingPanel(int count)
    {
        pendingCount = count;
        pendingText.text = $"You have {pendingCount} unaccounted-for puff{(pendingCount == 1 ? "" : "s")}. " +
                           $"How many did you smoke?";

        puffSlider.value = 0;
        OnSliderChanged(0);

        pendingPanel.SetActive(true);
    }

    public void RefreshCount(int count)
    {
        if (count == pendingCount) return;
        pendingCount = count;
        pendingText.text = $"You have {pendingCount} unaccounted-for puff{(pendingCount == 1 ? "" : "s")}. " +
                           $"How many did you smoke?";
        OnSliderChanged(puffSlider.value);
    }

    public void HidePanel()
    {
        if (pendingPanel != null && pendingPanel.activeSelf)
            pendingPanel.SetActive(false);
    }

    public bool IsPanelActive()
    {
        return pendingPanel != null && pendingPanel.activeSelf;
    }

    void OnSliderChanged(float value)
    {
        if (pendingCount <= 0) { sliderPreviewText.text = ""; return; }

        int smoked = Mathf.RoundToInt(pendingCount * (value / 100f));
        int declined = pendingCount - smoked;

        sliderPreviewText.text = $"{smoked} smoked  |  {declined} declined";
    }

    void OnConfirm()
    {
        int smoked = Mathf.RoundToInt(pendingCount * (puffSlider.value / 100f));
        int declined = pendingCount - smoked;

        Debug.Log($"[Pending] Confirm - smoked={smoked}, declined={declined} (of {pendingCount})");

        mainApp.ResolvePendingSplit(smoked, declined);
        pendingPanel.SetActive(false);
    }

    void OnAccountIndividually()
    {
        Debug.Log("[Pending] Account individually requested.");
        mainApp.OpenCalendarForIndividualAccounting();
        pendingPanel.SetActive(false);
    }
}