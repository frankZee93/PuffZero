using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CalendarController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject calendarPanel;
    public TMP_Text monthYearText;
    public Button prevMonthButton;
    public Button nextMonthButton;
    public Button closeButton;
    public Button openButton;
    public GameObject dayCellPrefab;
    public Transform gridParent;

    [Header("Day List")]
    public DayListController dayListController;

    [Header("Colors")]
    public Color emptyDayColor = new Color(1f, 1f, 1f, 0.10f);
    public Color activeDayColor = new Color(1f, 1f, 1f, 0.30f);
    public Color todayColor = new Color(0.25f, 0.55f, 1f, 0.55f);

    public Action OnClosed;

    private DateTime displayedMonth;
    private readonly List<GameObject> spawnedCells = new List<GameObject>();

    void Start()
    {
        displayedMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        if (prevMonthButton != null) prevMonthButton.onClick.AddListener(PrevMonth);
        if (nextMonthButton != null) nextMonthButton.onClick.AddListener(NextMonth);
        if (closeButton != null) closeButton.onClick.AddListener(CloseCalendar);
        if (openButton != null) openButton.onClick.AddListener(OpenCalendar);

        if (calendarPanel != null) calendarPanel.SetActive(false);
    }

    public void OpenCalendar()
    {
        calendarPanel.SetActive(true);
        displayedMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        RefreshCalendar();
    }

    public void CloseCalendar()
    {
        calendarPanel.SetActive(false);
        OnClosed?.Invoke();
    }

    void PrevMonth() { displayedMonth = displayedMonth.AddMonths(-1); RefreshCalendar(); }
    void NextMonth() { displayedMonth = displayedMonth.AddMonths(1); RefreshCalendar(); }

    void RefreshCalendar()
    {
        if (monthYearText != null)
            monthYearText.text = displayedMonth.ToString("MMMM yyyy");

        foreach (var cell in spawnedCells)
            if (cell != null) Destroy(cell);
        spawnedCells.Clear();

        int firstWeekday = ((int)displayedMonth.DayOfWeek + 6) % 7;
        int daysInMonth = DateTime.DaysInMonth(displayedMonth.Year, displayedMonth.Month);

        for (int i = 0; i < firstWeekday; i++) SpawnCell(null);
        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime date = new DateTime(displayedMonth.Year, displayedMonth.Month, day);
            SpawnCell(date);
        }

        int totalCells = firstWeekday + daysInMonth;
        int remainder = totalCells % 7;
        if (remainder != 0)
            for (int i = 0; i < (7 - remainder); i++) SpawnCell(null);
    }

    void SpawnCell(DateTime? date)
    {
        if (dayCellPrefab == null || gridParent == null) return;

        GameObject cell = Instantiate(dayCellPrefab, gridParent);
        spawnedCells.Add(cell);

        TMP_Text dayText = cell.transform.Find("DayNumberText")?.GetComponent<TMP_Text>();
        TMP_Text countText = cell.transform.Find("PuffCountText")?.GetComponent<TMP_Text>();
        Image bg = cell.GetComponent<Image>();

        if (date.HasValue)
        {
            int puffs = UserData.GetPuffsForDate(date.Value);
            DateTime today = DateTime.Now.Date;

            if (dayText != null) dayText.text = date.Value.Day.ToString();
            if (countText != null) countText.text = puffs > 0 ? puffs.ToString() : "";

            if (bg != null)
            {
                if (date.Value.Date == today) bg.color = todayColor;
                else bg.color = activeDayColor;
            }

            var btn = cell.GetComponent<Button>();
            if (btn == null) btn = cell.AddComponent<Button>();
            if (btn.targetGraphic == null && bg != null) btn.targetGraphic = bg;
            btn.onClick.RemoveAllListeners();
            DateTime capturedDate = date.Value;
            btn.onClick.AddListener(() => OnDayCellClicked(capturedDate));
        }
        else
        {
            if (dayText != null) dayText.text = "";
            if (countText != null) countText.text = "";
            if (bg != null) bg.color = emptyDayColor;
        }
    }

    void OnDayCellClicked(DateTime date)
    {
        if (calendarPanel != null) calendarPanel.SetActive(false);
        if (dayListController != null)
            dayListController.OpenForDate(date);
        else
            Debug.LogWarning("[Calendar] DayListController not wired.");
    }
}