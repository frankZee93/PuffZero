using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TaperCompletePanel : MonoBehaviour
{
    [Header("Panel")]
    public GameObject panelRoot;
    public TMP_Text titleText;
    public TMP_Text daysText;
    public TMP_Text puffsText;
    public TMP_Text datesText;

    [Header("Actions")]
    public Button viewCalendarButton;
    public Button settingsButton;

    private Action onViewCalendar;
    private Action onOpenSettings;

    void Awake()
    {
        if (viewCalendarButton != null) viewCalendarButton.onClick.AddListener(() => onViewCalendar?.Invoke());
        if (settingsButton != null) settingsButton.onClick.AddListener(() => onOpenSettings?.Invoke());
    }

    public void Show(DateTime startDate, DateTime finishDate, Action viewCalendar, Action openSettings)
    {
        if (panelRoot == null) return;

        int days = (finishDate.Date - startDate.Date).Days + 1;
        if (days < 1) days = 1;

        int totalPuffs = 0;
        DateTime cursor = startDate.Date;
        DateTime end = finishDate.Date;
        while (cursor <= end)
        {
            totalPuffs += UserData.GetPuffsForDate(cursor);
            cursor = cursor.AddDays(1);
        }

        if (titleText != null) titleText.text = "You did it.";
        if (daysText != null)
            daysText.text = days == 1 ? "1 day" : $"{days} days";
        if (puffsText != null)
            puffsText.text = totalPuffs == 1 ? "1 puff smoked" : $"{totalPuffs} puffs smoked";
        if (datesText != null)
            datesText.text = $"{startDate:dd MMM yyyy}  \u2192  {finishDate:dd MMM yyyy}";

        onViewCalendar = viewCalendar;
        onOpenSettings = openSettings;
        panelRoot.SetActive(true);
    }
}