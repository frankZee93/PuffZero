using UnityEngine;
using UnityEngine.UI;

public class TermsScreenController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pageRoot;
    public Toggle agreeToggle;
    public Button continueButton;

    void Start()
    {
        // If they already accepted the terms in a previous session, hide this whole screen immediately.
        if (UserData.TermsAccepted)
        {
            if (pageRoot != null) pageRoot.SetActive(false);
            return;
        }

        // Otherwise, make sure it's visible and lock the button.
        if (pageRoot != null) pageRoot.SetActive(true);
        if (continueButton != null) continueButton.interactable = false;

        // Listen for the toggle changing
        if (agreeToggle != null) agreeToggle.onValueChanged.AddListener(OnToggleChanged);

        // Listen for the button click
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
    }

    void OnToggleChanged(bool isOn)
    {
        // Only allow the button to be clicked if the toggle is checked
        if (continueButton != null) continueButton.interactable = isOn;
    }

    void OnContinueClicked()
    {
        // Save the flag so they never see this screen again
        UserData.TermsAccepted = true;
        UserData.Save();

        // Hide the screen
        if (pageRoot != null) pageRoot.SetActive(false);
    }
}