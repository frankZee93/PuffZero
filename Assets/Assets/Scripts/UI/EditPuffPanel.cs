using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EditPuffPanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text currentStatusText;

    [Header("Options")]
    public Button smokedButton;
    public Button skippedButton;
    public Button erasedButton;
    public Button cancelButton;

    private long scheduledTicks;
    private PuffStatus currentStatus;
    private Action<PuffStatus> onConfirm;
    private Action onCancel;

    void Start()
    {
        if (smokedButton != null) smokedButton.onClick.AddListener(() => Pick(PuffStatus.Smoked));
        if (skippedButton != null) skippedButton.onClick.AddListener(() => Pick(PuffStatus.Skipped));
        if (erasedButton != null) erasedButton.onClick.AddListener(() => Pick(PuffStatus.Erased));
        if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open(long ticks, PuffStatus status, Action<PuffStatus> confirm, Action cancel)
    {
        scheduledTicks = ticks;
        currentStatus = status;
        onConfirm = confirm;
        onCancel = cancel;

        if (currentStatusText != null)
            currentStatusText.text = $"Currently: {LabelFor(status)}";
        if (titleText != null)
            titleText.text = "Change this puff to:";
        if (panelRoot != null) panelRoot.SetActive(true);
    }

    public bool IsOpen() => panelRoot != null && panelRoot.activeSelf;

    void Pick(PuffStatus newStatus)
    {
        var cb = onConfirm;
        Hide();
        if (newStatus != currentStatus) cb?.Invoke(newStatus);
    }

    void Cancel()
    {
        var cb = onCancel;
        Hide();
        cb?.Invoke();
    }

    void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    static string LabelFor(PuffStatus status)
    {
        switch (status)
        {
            case PuffStatus.Smoked: return "✓ Smoked";
            case PuffStatus.Skipped: return "✗ Skipped";
            case PuffStatus.Erased: return "— Erased";
        }
        return "—";
    }
}