using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayListRow : MonoBehaviour
{
    [Header("References")]
    public TMP_Text puffNumberText;
    public TMP_Text timeText;
    public Image background;
    public GameObject unaccountedGroup;
    public GameObject assignedGroup;
    public Button smokedButton;
    public Button skippedButton;
    public Button editButton;
    public TMP_Text statusText;

    [Header("Colors")]
    public Color unaccountedColor = new Color(0.90f, 0.60f, 0.20f, 0.35f);
    public Color smokedColor = new Color(0.25f, 0.70f, 0.35f, 0.30f);
    public Color skippedColor = new Color(0.80f, 0.30f, 0.30f, 0.30f);
    public Color erasedColor = new Color(1f, 1f, 1f, 0.10f);
    public Color upcomingColor = new Color(1f, 1f, 1f, 0.15f);

    public long ScheduledTicks { get; private set; }
    public bool IsUnaccounted { get; private set; }
    public bool IsUpcoming { get; private set; }

    private int puffNumber;
    private Action<long> onSmoked;
    private Action<long> onSkipped;
    private Action<long> onEdit;
    private bool wired;

    void Awake()
    {
        if (wired) return;
        if (smokedButton != null) smokedButton.onClick.AddListener(() => onSmoked?.Invoke(ScheduledTicks));
        if (skippedButton != null) skippedButton.onClick.AddListener(() => onSkipped?.Invoke(ScheduledTicks));
        if (editButton != null) editButton.onClick.AddListener(() => onEdit?.Invoke(ScheduledTicks));
        wired = true;
    }

    public void Bind(Action<long> smoked, Action<long> skipped, Action<long> edit)
    {
        onSmoked = smoked;
        onSkipped = skipped;
        onEdit = edit;
    }

    public void Configure(long scheduledTicks, int puffNumber, PuffStatus? status, bool isUnaccounted, bool isUpcoming)
    {
        this.ScheduledTicks = scheduledTicks;
        this.puffNumber = puffNumber;
        this.IsUnaccounted = isUnaccounted;
        this.IsUpcoming = isUpcoming;

        if (puffNumberText != null) puffNumberText.text = $"Puff #{puffNumber}";
        if (timeText != null) timeText.text = new DateTime(scheduledTicks).ToString("HH:mm");

        if (isUnaccounted)
        {
            if (background != null) background.color = unaccountedColor;
            if (unaccountedGroup != null) unaccountedGroup.SetActive(true);
            if (assignedGroup != null) assignedGroup.SetActive(false);
        }
        else
        {
            if (background != null) background.color = ColorFor(status, isUpcoming);
            if (unaccountedGroup != null) unaccountedGroup.SetActive(false);
            if (assignedGroup != null) assignedGroup.SetActive(true);
            if (statusText != null) statusText.text = LabelFor(status, isUpcoming);
        }
    }

    // Flips an upcoming row to unaccounted if its scheduled time has passed.
    // Used by live refresh while the panel is open.
    public bool TryPromoteToUnaccounted()
    {
        if (!IsUpcoming) return false;
        if (new DateTime(ScheduledTicks) > DateTime.Now) return false;

        Configure(ScheduledTicks, puffNumber, null, true, false);
        return true;
    }

    Color ColorFor(PuffStatus? status, bool isUpcoming)
    {
        if (isUpcoming) return upcomingColor;
        if (!status.HasValue) return erasedColor;
        switch (status.Value)
        {
            case PuffStatus.Smoked: return smokedColor;
            case PuffStatus.Skipped: return skippedColor;
            case PuffStatus.Erased: return erasedColor;
        }
        return erasedColor;
    }

    string LabelFor(PuffStatus? status, bool isUpcoming)
    {
        if (isUpcoming) return "";
        if (!status.HasValue) return "\u2014";
        switch (status.Value)
        {
            case PuffStatus.Smoked: return "\u2713 Smoked";
            case PuffStatus.Skipped: return "\u2717 Skipped";
            case PuffStatus.Erased: return "\u2014 Erased";
        }
        return "";
    }
}