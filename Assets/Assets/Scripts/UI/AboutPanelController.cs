using UnityEngine;
using UnityEngine.UI;

public class AboutPanelController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject aboutPanel;
    public Button openButton;
    public Button closeButton;

    void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(OpenPanel);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

        if (aboutPanel != null) aboutPanel.SetActive(false);
    }

    public void OpenPanel()
    {
        if (aboutPanel != null) aboutPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        if (aboutPanel != null) aboutPanel.SetActive(false);
    }

}