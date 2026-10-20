using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DayListController : MonoBehaviour
{
    [Header("Panel")]
    public GameObject dayListPanel;
    public Button backButton;
    public TMP_Text headerText;
    public Button nextUnaccountedButton;
    public ScrollRect scrollRect;
    public RectTransform contentRect;
    public RectTransform viewportRect;

    [Header("Prefab")]
    public GameObject dayListRowPrefab;

    [Header("Navigation")]
    public CalendarController calendarController;
    public MainAppController mainApp;

    [Header("Edit")]
    public EditPuffPanel editPanel;

    [Header("Layout")]
    public float rowHeight = 120f;
    public float rowSpacing = 20f;
    public float topPadding = 20f;
    public float bottomPadding = 20f;
    public int bufferRows = 2;

    [Header("Live Refresh")]
    public float liveRefreshInterval = 2f;

    private DateTime currentDate;
    private List<RowData> rowData = new List<RowData>();
    private List<DayListRow> visibleRows = new List<DayListRow>();
    private Stack<DayListRow> rowPool = new Stack<DayListRow>();
    private int lastUnaccountedIndex = -1;
    private float liveRefreshTimer;
    private bool isOpen;

    private struct RowData
    {
        public long Ticks;
        public int PuffNumber;
        public PuffStatus? Status;
        public bool IsUnaccounted;
        public bool IsUpcoming;
    }

    void Start()
    {
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
        if (nextUnaccountedButton != null) nextUnaccountedButton.onClick.AddListener(OnNextUnaccountedClicked);
        if (scrollRect != null) scrollRect.onValueChanged.AddListener(OnScrollChanged);
        if (dayListPanel != null) dayListPanel.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;
        if (editPanel != null && editPanel.IsOpen()) return;

        liveRefreshTimer -= Time.unscaledDeltaTime;
        if (liveRefreshTimer > 0f) return;
        liveRefreshTimer = liveRefreshInterval;

        LiveRefresh();
    }

    public void OpenForDate(DateTime date)
    {
        currentDate = date.Date;
        lastUnaccountedIndex = -1;
        liveRefreshTimer = liveRefreshInterval;

        if (dayListPanel != null) dayListPanel.SetActive(true);
        isOpen = true;
        BuildData();
        UpdateVisibleRows();
        StartCoroutine(ScrollToFirstUnaccountedNextFrame());
    }

    public void ClosePanel()
    {
        if (dayListPanel != null) dayListPanel.SetActive(false);
        isOpen = false;
    }

    public bool IsPanelActive()
    {
        return isOpen;
    }

    void OnBackClicked()
    {
        ClosePanel();
        if (calendarController != null)
            calendarController.OpenCalendar();
    }

    void BuildData()
    {
        rowData.Clear();

        if (headerText != null)
            headerText.text = currentDate.ToString("dddd d MMMM yyyy");

        var schedule = UserData.LoadSchedule();
        if (schedule == null || schedule.Count == 0)
        {
            SetContentHeight(0);
            return;
        }

        var todaysTicks = new List<long>();
        for (int i = 0; i < schedule.Count; i++)
        {
            long t = schedule[i];
            if (new DateTime(t).Date == currentDate) todaysTicks.Add(t);
        }
        todaysTicks.Sort();

        var records = UserData.GetPuffRecordsForDate(currentDate);
        var recordByTicks = new Dictionary<long, PuffRecord>(records.Count);
        for (int i = 0; i < records.Count; i++)
            recordByTicks[records[i].ScheduledTicks] = records[i];

        DateTime now = DateTime.Now;
        for (int i = 0; i < todaysTicks.Count; i++)
        {
            long ticks = todaysTicks[i];
            PuffStatus? status = null;
            if (recordByTicks.TryGetValue(ticks, out var rec)) status = rec.Status;
            DateTime scheduled = new DateTime(ticks);
            bool isUnacc = (status == null) && (scheduled <= now);
            bool isUpcoming = (status == null) && (scheduled > now);

            rowData.Add(new RowData
            {
                Ticks = ticks,
                PuffNumber = i + 1,
                Status = status,
                IsUnaccounted = isUnacc,
                IsUpcoming = isUpcoming,
            });
        }

        SetContentHeight(rowData.Count);
    }

    void SetContentHeight(int rowCount)
    {
        if (contentRect == null) return;
        float h = 0f;
        if (rowCount > 0)
            h = rowCount * rowHeight + (rowCount - 1) * rowSpacing + topPadding + bottomPadding;
        contentRect.sizeDelta = new Vector2(0f, h);
    }

    void OnScrollChanged(Vector2 _)
    {
        if (!isOpen) return;
        UpdateVisibleRows();
    }

    void UpdateVisibleRows()
    {
        if (contentRect == null || viewportRect == null || scrollRect == null) return;
        if (rowData.Count == 0) { RecycleAll(); return; }

        float viewportHeight = viewportRect.rect.height;
        float contentHeight = contentRect.rect.height;

        float scrollable = contentHeight - viewportHeight;
        if (scrollable < 0f) scrollable = 0f;
        float scrollOffset = 1f - scrollRect.verticalNormalizedPosition;
        float topY = scrollOffset * scrollable;

        int firstVisible = Mathf.FloorToInt((topY - topPadding) / (rowHeight + rowSpacing));
        if (firstVisible < 0) firstVisible = 0;
        int lastVisible = Mathf.CeilToInt((topY + viewportHeight - topPadding) / (rowHeight + rowSpacing));
        if (lastVisible >= rowData.Count) lastVisible = rowData.Count - 1;

        firstVisible -= bufferRows;
        if (firstVisible < 0) firstVisible = 0;
        lastVisible += bufferRows;
        if (lastVisible >= rowData.Count) lastVisible = rowData.Count - 1;

        int needed = lastVisible - firstVisible + 1;

        while (visibleRows.Count > needed)
        {
            var row = visibleRows[visibleRows.Count - 1];
            visibleRows.RemoveAt(visibleRows.Count - 1);
            row.gameObject.SetActive(false);
            rowPool.Push(row);
        }

        while (visibleRows.Count < needed)
        {
            var row = GetOrCreateRow();
            if (row == null) return;
            visibleRows.Add(row);
        }

        for (int i = 0; i < needed; i++)
        {
            int dataIndex = firstVisible + i;
            var row = visibleRows[i];
            var data = rowData[dataIndex];

            float y = -(topPadding + dataIndex * (rowHeight + rowSpacing));
            var rt = row.transform as RectTransform;
            rt.anchoredPosition = new Vector2(0f, y);

            row.Configure(data.Ticks, data.PuffNumber, data.Status, data.IsUnaccounted, data.IsUpcoming);
            row.gameObject.SetActive(true);
        }
    }

    void RecycleAll()
    {
        for (int i = 0; i < visibleRows.Count; i++)
        {
            var r = visibleRows[i];
            if (r != null) { r.gameObject.SetActive(false); rowPool.Push(r); }
        }
        visibleRows.Clear();
    }

    DayListRow GetOrCreateRow()
    {
        DayListRow row;
        while (rowPool.Count > 0)
        {
            row = rowPool.Pop();
            if (row != null) return row;
        }

        var go = Instantiate(dayListRowPrefab, contentRect);
        row = go.GetComponent<DayListRow>();
        if (row == null)
        {
            Debug.LogError("[DayList] Prefab missing DayListRow.");
            return null;
        }
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, rowHeight);
        }
        row.Bind(OnRowSmoked, OnRowSkipped, OnRowEdit);
        return row;
    }

    void LiveRefresh()
    {
        DateTime now = DateTime.Now;
        bool changed = false;
        for (int i = 0; i < rowData.Count; i++)
        {
            var d = rowData[i];
            if (!d.IsUpcoming) continue;
            if (new DateTime(d.Ticks) > now) continue;
            d.IsUpcoming = false;
            d.IsUnaccounted = true;
            rowData[i] = d;
            changed = true;
        }
        if (changed) UpdateVisibleRows();
    }

    void OnRowSmoked(long scheduledTicks)
    {
        if (UserData.GetPuffRecord(scheduledTicks).HasValue) return;

        UserData.LogPuffToday();
        UserData.RecordPuffResolution(scheduledTicks, PuffStatus.Smoked);
        UserData.Save();

        if (mainApp != null) mainApp.OnPuffResolvedExternally();

        RefreshAfterAction();
    }

    void OnRowSkipped(long scheduledTicks)
    {
        if (UserData.GetPuffRecord(scheduledTicks).HasValue) return;

        UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;
        UserData.RecordPuffResolution(scheduledTicks, PuffStatus.Skipped);
        UserData.Save();

        if (mainApp != null) mainApp.OnPuffResolvedExternally();

        RefreshAfterAction();
    }

    void OnRowEdit(long scheduledTicks)
    {
        if (editPanel == null)
        {
            Debug.Log($"[DayList] Edit requested for {new DateTime(scheduledTicks)} (no panel wired).");
            return;
        }

        var rec = UserData.GetPuffRecord(scheduledTicks);
        if (!rec.HasValue) return;

        editPanel.Open(scheduledTicks, rec.Value.Status, newStatus =>
        {
            UserData.ChangePuffStatus(scheduledTicks, newStatus);
            if (mainApp != null) mainApp.OnPuffResolvedExternally();
            RefreshAfterAction();
        }, () =>
        {
            // cancelled — nothing to do
        });
    }

    void RefreshAfterAction()
    {
        float savedNorm = scrollRect != null ? scrollRect.verticalNormalizedPosition : 1f;
        BuildData();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = savedNorm;
        UpdateVisibleRows();
    }

    void OnNextUnaccountedClicked()
    {
        int next = -1;
        for (int i = lastUnaccountedIndex + 1; i < rowData.Count; i++)
            if (rowData[i].IsUnaccounted) { next = i; break; }
        if (next < 0 && lastUnaccountedIndex >= 0)
            for (int i = 0; i < rowData.Count; i++)
                if (rowData[i].IsUnaccounted) { next = i; break; }
        if (next < 0) { Debug.Log("[DayList] No unaccounted rows."); return; }
        lastUnaccountedIndex = next;
        ScrollToRow(next);
    }

    IEnumerator ScrollToFirstUnaccountedNextFrame()
    {
        yield return null;
        for (int i = 0; i < rowData.Count; i++)
        {
            if (rowData[i].IsUnaccounted)
            {
                lastUnaccountedIndex = i;
                ScrollToRow(i);
                yield break;
            }
        }
    }

    void ScrollToRow(int index)
    {
        if (scrollRect == null || contentRect == null) return;
        if (index < 0 || index >= rowData.Count) return;

        float contentHeight = contentRect.rect.height;
        float viewportHeight = viewportRect.rect.height;
        float scrollable = contentHeight - viewportHeight;
        if (scrollable <= 0f) { scrollRect.verticalNormalizedPosition = 1f; return; }

        float rowTop = topPadding + index * (rowHeight + rowSpacing);
        rowTop = Mathf.Clamp(rowTop, 0f, scrollable);
        scrollRect.verticalNormalizedPosition = 1f - (rowTop / scrollable);
        UpdateVisibleRows();
    }
}