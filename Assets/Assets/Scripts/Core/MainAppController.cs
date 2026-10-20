using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

public class MainAppController : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject waitingPanel;
    public GameObject smokePromptPanel;
    public GameObject pausePanel;
    public GameObject menuPanel;
    public GameObject activePausePanel;
    public GameObject settingsPanel;
    public GameObject confirmResetPanel;
    public GameObject sleepModePanel;
    public GameObject confirmEarlyWakePanel;
    public GameObject overBudgetPanel;
    public GameObject confirmResetProgressPanel;
    public GameObject recoveryPanel;
    public GameObject dayListPanel;
    public TaperCompletePanel taperCompletePanel;

    [Header("Controllers")]
    public CalendarController calendarController;

    [Header("Unaccounted Before End/Pause")]
    public GameObject unaccountedPromptPanel;
    public TMP_Text unaccountedPromptTitle;
    public Button accountUnaccountedButton;
    public Button forgetUnaccountedButton;

    [Header("Pause Resume Prompt")]
    public GameObject pauseResumePromptPanel;
    public Button stillMyDayButton;
    public Button endOfDayFromPauseButton;

    [Header("Ideal Number Panel")]
    public GameObject idealNumberPanel;
    public Button keepSmokingButton;
    public Button stopForDayButton;
    public Button reduceFrequencyButton;

    [Header("UI Text")]
    public TMP_Text statusText;
    public TMP_Text timerText;
    public TMP_Text pauseStatusText;
    public TMP_Text sleepStatusText;

    [Header("UI Buttons")]
    public Button smokeButton;
    public Button skipButton;
    public Button pauseButton;
    public Button confirmPauseButton;
    public Button cancelPauseButton;
    public Button endPauseButton;
    public Button settingsButton;
    public Button closeSettingsButton;
    public Button resetAppButton;
    public Button confirmResetYesButton;
    public Button confirmResetNoButton;
    public Button goingToSleepButton;
    public Button imUpNowButton;
    public Button cancelSleepButton;
    public Button earlyWakeYesButton;
    public Button earlyWakeNoButton;
    public Button overBudgetContinueButton;
    public Button overBudgetStopButton;
    public Button resetProgressButton;
    public Button confirmResetProgressYesButton;
    public Button confirmResetProgressNoButton;
    public Button recoveryYesButton;
    public Button recoveryNoButton;

    [Header("Pending Puffs")]
    public PendingPuffsController pendingPuffsController;

    [Header("Pause Settings")]
    public TMP_InputField pauseHoursInput;
    public ScrollPicker pickerManager;

    private const int SLEEP_MODE_MAX_HOURS = 24;
    private const float LONG_PAUSE_HOURS = 4f;
    private const float END_DAY_RECOVERY_HOURS = 4f;

    private const int PICKER_HOURS_MAX = 12;
    private const int PICKER_MINUTE_STEP = 5;
    private const int PICKER_MINUTE_STEPS = 12;

    [Header("Audio")]
    public AudioSource notificationSound;

    private List<long> schedule;
    private int currentPuffIndex = 0;
    private DateTime nextPuffTime;
    private bool isInitialized = false;
    private bool celebrationActive = false;

    private bool sleepMode = false;

    private DateTime? pauseEndTime = null;
    private bool IsPaused => pauseEndTime.HasValue;

    private enum DeferredAction { None, EndDay, Pause }
    private DeferredAction deferredAction = DeferredAction.None;

    private int pendingPauseHours = 1;
    private int pendingPauseMinutes = 0;

#if UNITY_IOS
    private string lastProcessedNotificationId = null;
#endif

    void Start()
    {
        if (taperCompletePanel != null && taperCompletePanel.panelRoot != null)
            taperCompletePanel.panelRoot.SetActive(false);

        schedule = UserData.LoadSchedule();

        if (schedule == null || schedule.Count == 0)
        {
            statusText.text = "No schedule found. Please complete onboarding.";
            return;
        }

        if (calendarController != null)
            calendarController.OnClosed += OnCalendarClosed;

        pauseEndTime = UserData.PauseEndTime;
        if (pauseEndTime.HasValue && TimeService.Now >= pauseEndTime.Value)
        {
            bool needsPrompt = WasPauseLongOrCrossedSleep(UserData.PauseStartTime, TimeService.Now);
            pauseEndTime = null;
            UserData.PauseEndTime = null;
            UserData.PauseStartTime = null;
            if (needsPrompt)
            {
                Debug.Log("[Pause] Expired while closed, showing resume prompt.");
                ShowPauseResumePrompt();
            }
        }

        sleepMode = UserData.SleepModeActive;
        if (sleepMode && UserData.SleepModeStartedAt.HasValue)
        {
            TimeSpan activeFor = TimeService.Now - UserData.SleepModeStartedAt.Value;
            if (activeFor.TotalHours >= SLEEP_MODE_MAX_HOURS)
            {
                Debug.Log("[Sleep] 24h timeout. Auto-exiting sleep mode.");
                sleepMode = false;
                UserData.SleepModeActive = false;
                UserData.SleepModeStartedAt = null;
                UserData.WakeNotificationFiredAt = null;
            }
        }

        currentPuffIndex = UserData.CurrentPuffIndex;

        smokeButton.onClick.AddListener(OnSmokeButtonClicked);
        skipButton.onClick.AddListener(OnSkipButtonClicked);
        pauseButton.onClick.AddListener(OpenPauseMenu);
        if (confirmPauseButton != null) confirmPauseButton.onClick.AddListener(ConfirmPause);
        cancelPauseButton.onClick.AddListener(CancelPause);
        endPauseButton.onClick.AddListener(EndPause);

        settingsButton.onClick.AddListener(OpenSettings);
        closeSettingsButton.onClick.AddListener(CloseSettings);
        resetAppButton.onClick.AddListener(ShowResetConfirmation);
        confirmResetYesButton.onClick.AddListener(ConfirmReset);
        confirmResetNoButton.onClick.AddListener(CancelReset);
        resetProgressButton.onClick.AddListener(ShowResetProgressConfirmation);
        confirmResetProgressYesButton.onClick.AddListener(ConfirmResetProgress);
        confirmResetProgressNoButton.onClick.AddListener(CancelResetProgress);

        if (goingToSleepButton != null)
            goingToSleepButton.onClick.AddListener(OnGoingToSleepClicked);
        if (imUpNowButton != null)
            imUpNowButton.onClick.AddListener(OnImUpNowClicked);
        if (cancelSleepButton != null)
            cancelSleepButton.onClick.AddListener(OnCancelSleepClicked);

        if (earlyWakeYesButton != null)
            earlyWakeYesButton.onClick.AddListener(OnEarlyWakeYesClicked);
        if (earlyWakeNoButton != null)
            earlyWakeNoButton.onClick.AddListener(OnEarlyWakeNoClicked);
        if (overBudgetContinueButton != null)
            overBudgetContinueButton.onClick.AddListener(OnOverBudgetContinueClicked);
        if (overBudgetStopButton != null)
            overBudgetStopButton.onClick.AddListener(OnOverBudgetStopClicked);

        if (recoveryYesButton != null) recoveryYesButton.onClick.AddListener(OnRecoveryYesClicked);
        if (recoveryNoButton != null) recoveryNoButton.onClick.AddListener(OnRecoveryNoClicked);

        if (accountUnaccountedButton != null)
            accountUnaccountedButton.onClick.AddListener(OnAccountUnaccountedClicked);
        if (forgetUnaccountedButton != null)
            forgetUnaccountedButton.onClick.AddListener(OnForgetUnaccountedClicked);

        if (stillMyDayButton != null)
            stillMyDayButton.onClick.AddListener(OnStillMyDayClicked);
        if (endOfDayFromPauseButton != null)
            endOfDayFromPauseButton.onClick.AddListener(OnEndOfDayFromPauseClicked);

        if (keepSmokingButton != null)
            keepSmokingButton.onClick.AddListener(OnKeepSmokingClicked);
        if (stopForDayButton != null)
            stopForDayButton.onClick.AddListener(OnStopForDayClicked);
        if (reduceFrequencyButton != null)
            reduceFrequencyButton.onClick.AddListener(OnReduceFrequencyClicked);

        if (unaccountedPromptPanel != null) unaccountedPromptPanel.SetActive(false);
        if (pauseResumePromptPanel != null) pauseResumePromptPanel.SetActive(false);
        if (idealNumberPanel != null) idealNumberPanel.SetActive(false);

        isInitialized = true;

#if UNITY_IOS
        StartCoroutine(StartupRoutine());
#else
        RunStartupCatchUp();
#endif
    }

    private void RunStartupCatchUp()
    {
        TryRecoverFromEndDay();
        ReconcileCurrentIndexFromRecords();
        RunCatchUp();
        ValidateExtendedDay();

        if (currentPuffIndex >= schedule.Count)
        {
            ShowFinishedState();
            return;
        }

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);
        if (!sleepMode && UserData.IsInSleepWindow(nextPuffTime))
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

        RefreshNotifications();
        UpdateUI();
    }

#if UNITY_IOS
    private IEnumerator StartupRoutine()
    {
        var op = iOSNotificationCenter.QueryLastRespondedNotification();
        yield return op;

        if (op.Notification != null &&
            !string.IsNullOrEmpty(op.Notification.Identifier) &&
            op.Notification.Identifier.StartsWith("puff_") &&
            lastProcessedNotificationId != op.Notification.Identifier)
        {
            lastProcessedNotificationId = op.Notification.Identifier;
            Debug.Log($"[Notif] Startup response - id={op.Notification.Identifier}, action={op.ActionId}");
            ApplyNotificationAnswer(op.Notification.Identifier, op.ActionId);
        }

        RunStartupCatchUp();
    }

    private void ApplyNotificationAnswer(string identifier, string actionId)
    {
        if (string.IsNullOrEmpty(identifier) || !identifier.StartsWith("puff_")) return;

        long answeredTicks;
        if (!long.TryParse(identifier.Substring(5), out answeredTicks)) return;

        int answeredIndex = -1;
        for (int i = 0; i < schedule.Count; i++)
        {
            if (schedule[i] == answeredTicks) { answeredIndex = i; break; }
        }

        if (answeredIndex < 0 || answeredIndex < currentPuffIndex)
        {
            Debug.Log($"[Notif] Stale answer discarded (id={identifier}).");
            return;
        }

        if (actionId == NotificationScheduler.ActionSmokedId)
        {
            UserData.LogPuffToday();
            UserData.RecordPuffResolution(answeredTicks, PuffStatus.Smoked);
        }
        else if (actionId == NotificationScheduler.ActionSkipId)
        {
            UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;
            UserData.RecordPuffResolution(answeredTicks, PuffStatus.Skipped);
        }

        currentPuffIndex = answeredIndex + 1;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        ReconcileCurrentIndexFromRecords();
    }
#endif

    void RunCatchUp()
    {
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();
    }

    private int GetOverdueCount()
    {
        if (schedule == null || currentPuffIndex >= schedule.Count) return 0;
        DateTime now = TimeService.Now;
        int count = 0;
        for (int i = currentPuffIndex; i < schedule.Count; i++)
        {
            if (new DateTime(schedule[i]) <= now)
            {
                var rec = UserData.GetPuffRecord(schedule[i]);
                if (!rec.HasValue) count++;
            }
            else break;
        }
        return count;
    }

    private void MarkPuffsErased(int fromIndex, int toIndexExclusive)
    {
        if (schedule == null) return;
        if (fromIndex < 0) fromIndex = 0;
        if (toIndexExclusive > schedule.Count) toIndexExclusive = schedule.Count;

        for (int i = fromIndex; i < toIndexExclusive; i++)
            UserData.RecordPuffResolution(schedule[i], PuffStatus.Erased);
    }

    private void ReconcileCurrentIndexFromRecords()
    {
        if (schedule == null || schedule.Count == 0) return;
        if (currentPuffIndex >= schedule.Count) return;

        int newIndex = currentPuffIndex;
        while (newIndex < schedule.Count)
        {
            var rec = UserData.GetPuffRecord(schedule[newIndex]);
            if (rec.HasValue) newIndex++;
            else break;
        }

        if (newIndex != currentPuffIndex)
        {
            currentPuffIndex = newIndex;
            UserData.CurrentPuffIndex = currentPuffIndex;
            UserData.Save();
        }

        if (currentPuffIndex < schedule.Count)
        {
            nextPuffTime = new DateTime(schedule[currentPuffIndex]);
            if (!sleepMode && UserData.IsInSleepWindow(nextPuffTime))
                nextPuffTime = UserData.NextWakeAfter(nextPuffTime);
        }
    }

    private void ErasePuffsInPauseWindow(DateTime pauseStart, DateTime pauseEnd)
    {
        if (schedule == null || schedule.Count == 0) return;
        if (pauseStart >= pauseEnd) return;

        long startTicks = pauseStart.Ticks;
        long endTicks = pauseEnd.Ticks;

        int erased = 0;
        for (int i = 0; i < schedule.Count; i++)
        {
            long t = schedule[i];
            if (t < startTicks) continue;
            if (t > endTicks) break;

            var rec = UserData.GetPuffRecord(t);
            if (rec.HasValue) continue;

            UserData.RecordPuffResolution(t, PuffStatus.Erased);
            erased++;
        }

        if (erased > 0)
            Debug.Log($"[Pause] Erased {erased} puffs inside pause window ({pauseStart:HH:mm} – {pauseEnd:HH:mm}).");

        ReconcileCurrentIndexFromRecords();
    }

    public void OnPuffResolvedExternally()
    {
        if (!isInitialized) return;

        ReconcileCurrentIndexFromRecords();

        if (currentPuffIndex >= schedule.Count)
        {
            ShowFinishedState();
            return;
        }

        RefreshNotifications();
        UpdateUI();
    }

    public void ResolvePendingSplit(int smoked, int declined)
    {
        if (smoked < 0) smoked = 0;
        if (declined < 0) declined = 0;

        int overdue = GetOverdueCount();
        int total = smoked + declined;
        Debug.Log($"[Pending] Resolving {total} puffs - smoked={smoked}, declined={declined} (overdue={overdue})");

        for (int i = 0; i < smoked; i++) UserData.LogPuffToday();
        for (int i = 0; i < declined; i++) UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;

        int toAdvance = Mathf.Min(total, overdue);

        int smokedRemaining = smoked;
        int declinedRemaining = declined;
        for (int i = 0; i < toAdvance; i++)
        {
            long scheduledTicks = schedule[currentPuffIndex + i];
            if (smokedRemaining > 0)
            {
                UserData.RecordPuffResolution(scheduledTicks, PuffStatus.Smoked);
                smokedRemaining--;
            }
            else if (declinedRemaining > 0)
            {
                UserData.RecordPuffResolution(scheduledTicks, PuffStatus.Skipped);
                declinedRemaining--;
            }
            else
            {
                UserData.RecordPuffResolution(scheduledTicks, PuffStatus.Erased);
            }
        }

        currentPuffIndex += toAdvance;
        if (currentPuffIndex > schedule.Count) currentPuffIndex = schedule.Count;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        ReconcileCurrentIndexFromRecords();

        if (deferredAction != DeferredAction.None)
        {
            var action = deferredAction;
            deferredAction = DeferredAction.None;
            Debug.Log($"[Pending] Running deferred action: {action}");
            if (action == DeferredAction.EndDay) { EndDay(); return; }
            if (action == DeferredAction.Pause)
            {
                menuPanel.SetActive(false);
                OpenPausePicker();
                return;
            }
        }

        if (currentPuffIndex >= schedule.Count) { ShowFinishedState(); return; }

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);
        if (!sleepMode && UserData.IsInSleepWindow(nextPuffTime))
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

        RefreshNotifications();
        CheckRecovery();
        UpdateUI();

        MaybeShowIdealPanel();
    }

    public void OpenCalendarForIndividualAccounting()
    {
        Debug.Log("[Pending] Individual accounting requested. Calendar day-list view not yet built.");
    }

    private void ShowUnaccountedPrompt(int count)
    {
        if (unaccountedPromptTitle != null)
        {
            string s = count == 1 ? "" : "s";
            unaccountedPromptTitle.text = $"You have {count} unaccounted-for puff{s}.";
        }
        if (unaccountedPromptPanel != null) unaccountedPromptPanel.SetActive(true);
    }

    public void OnAccountUnaccountedClicked()
    {
        if (unaccountedPromptPanel != null) unaccountedPromptPanel.SetActive(false);

        int overdue = GetOverdueCount();
        if (overdue > 0 && pendingPuffsController != null)
        {
            pendingPuffsController.ShowPendingPanel(overdue);
            return;
        }

        var action = deferredAction;
        deferredAction = DeferredAction.None;
        if (action == DeferredAction.EndDay) EndDay();
        else if (action == DeferredAction.Pause) { menuPanel.SetActive(false); OpenPausePicker(); }
    }

    public void OnForgetUnaccountedClicked()
    {
        int overdue = GetOverdueCount();
        Debug.Log($"[Unaccounted] Forgetting {overdue} puffs.");

        MarkPuffsErased(currentPuffIndex, currentPuffIndex + overdue);

        currentPuffIndex += overdue;
        if (currentPuffIndex > schedule.Count) currentPuffIndex = schedule.Count;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        ReconcileCurrentIndexFromRecords();

        if (unaccountedPromptPanel != null) unaccountedPromptPanel.SetActive(false);

        var action = deferredAction;
        deferredAction = DeferredAction.None;
        if (action == DeferredAction.EndDay) EndDay();
        else if (action == DeferredAction.Pause) { menuPanel.SetActive(false); OpenPausePicker(); }
    }

    private bool WasPauseLongOrCrossedSleep(DateTime? pauseStart, DateTime resumeTime)
    {
        if (!pauseStart.HasValue) return false;
        TimeSpan duration = resumeTime - pauseStart.Value;
        if (duration.TotalHours >= LONG_PAUSE_HOURS) return true;
        if (UserData.IsInSleepWindow(resumeTime)) return true;
        return false;
    }

    private void ShowPauseResumePrompt()
    {
        if (pauseResumePromptPanel != null) pauseResumePromptPanel.SetActive(true);
    }

    public void OnStillMyDayClicked()
    {
        Debug.Log("[Pause] User says still my day. Resuming.");
        if (pauseResumePromptPanel != null) pauseResumePromptPanel.SetActive(false);

        var start = UserData.PauseStartTime;
        var end = UserData.PauseEndTime;
        if (start.HasValue && end.HasValue)
            ErasePuffsInPauseWindow(start.Value, end.Value);

        pauseEndTime = null;
        UserData.PauseEndTime = null;
        UserData.PauseStartTime = null;
        UserData.Save();

        if (UserData.IsInSleepWindow(nextPuffTime))
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

        RefreshNotifications();
        UpdateUI();
    }

    public void OnEndOfDayFromPauseClicked()
    {
        Debug.Log("[Pause] User says end of day.");
        if (pauseResumePromptPanel != null) pauseResumePromptPanel.SetActive(false);

        EndDay();
    }

    private void MaybeShowIdealPanel()
    {
        if (idealNumberPanel == null) return;
        if (UserData.IdealPanelShownToday) return;
        if (!UserData.WokeEarlyToday) return;
        if (sleepMode || IsPaused) return;

        int smokedToday = UserData.GetPuffsForDate(TimeService.Now);
        if (smokedToday < UserData.PuffsPerDay) return;

        Debug.Log($"[Ideal] Ideal count reached ({smokedToday} >= {UserData.PuffsPerDay}). Showing panel.");
        UserData.IdealPanelShownToday = true;
        UserData.Save();
        idealNumberPanel.SetActive(true);
    }

    public void OnKeepSmokingClicked()
    {
        Debug.Log("[Ideal] User chose keep smoking.");
        if (idealNumberPanel != null) idealNumberPanel.SetActive(false);
        UpdateUI();
    }

    public void OnStopForDayClicked()
    {
        Debug.Log("[Ideal] User chose stop for the day.");
        if (idealNumberPanel != null) idealNumberPanel.SetActive(false);
        EndDay();
    }

    public void OnReduceFrequencyClicked()
    {
        Debug.Log("[Ideal] User chose reduce frequency. (Redistribution logic pending.)");
        if (idealNumberPanel != null) idealNumberPanel.SetActive(false);
        UpdateUI();
    }

    void Update()
    {
        if (!isInitialized || currentPuffIndex >= schedule.Count) return;

        if (pausePanel.activeSelf) { HideTopHalf(); return; }
        if (settingsPanel.activeSelf) { HideTopHalf(); return; }
        if (confirmResetPanel.activeSelf) { HideTopHalf(); return; }
        if (confirmResetProgressPanel != null && confirmResetProgressPanel.activeSelf) { HideTopHalf(); return; }
        if (confirmEarlyWakePanel != null && confirmEarlyWakePanel.activeSelf) { HideTopHalf(); return; }
        if (overBudgetPanel != null && overBudgetPanel.activeSelf) { HideTopHalf(); return; }
        if (recoveryPanel != null && recoveryPanel.activeSelf) { HideTopHalf(); return; }
        if (unaccountedPromptPanel != null && unaccountedPromptPanel.activeSelf) { HideTopHalf(); return; }
        if (pauseResumePromptPanel != null && pauseResumePromptPanel.activeSelf) { HideTopHalf(); return; }
        if (idealNumberPanel != null && idealNumberPanel.activeSelf) { HideTopHalf(); return; }
        if (dayListPanel != null && dayListPanel.activeSelf) { return; }
        if (pickerManager != null && pickerManager.IsOpen()) { HideTopHalf(); return; }

        DateTime now = TimeService.Now;
        ValidateExtendedDay();

        if (sleepMode)
        {
            UpdateSleepModeUI(now);
            if (pendingPuffsController != null) pendingPuffsController.HidePanel();
            return;
        }

        if (IsPaused && now >= pauseEndTime.Value)
        {
            DateTime? start = UserData.PauseStartTime;
            bool needsPrompt = WasPauseLongOrCrossedSleep(start, now);

            if (needsPrompt)
            {
                Debug.Log("[Pause] Expired. Showing resume prompt.");
                ShowPauseResumePrompt();
                return;
            }

            if (start.HasValue)
                ErasePuffsInPauseWindow(start.Value, pauseEndTime.Value);

            pauseEndTime = null;
            UserData.PauseEndTime = null;
            UserData.PauseStartTime = null;
            UserData.Save();

            if (UserData.IsInSleepWindow(nextPuffTime))
                nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

            RefreshNotifications();
            Debug.Log("Pause expired. Resuming.");
        }

        if (IsPaused)
        {
            waitingPanel.SetActive(false);
            smokePromptPanel.SetActive(false);
            menuPanel.SetActive(false);
            activePausePanel.SetActive(true);
            if (pendingPuffsController != null) pendingPuffsController.HidePanel();
            if (sleepModePanel != null) sleepModePanel.SetActive(false);

            TimeSpan left = pauseEndTime.Value - now;
            if (left.TotalHours >= 1)
                pauseStatusText.text = string.Format("Paused - {0:D2}:{1:D2}:{2:D2} left",
                    left.Hours, left.Minutes, left.Seconds);
            else
                pauseStatusText.text = string.Format("Paused - {0:D2}:{1:D2} left",
                    left.Minutes, left.Seconds);
            return;
        }

        activePausePanel.SetActive(false);

        int overdue = GetOverdueCount();

        if (overdue >= 2)
        {
            waitingPanel.SetActive(true);
            smokePromptPanel.SetActive(false);
            menuPanel.SetActive(false);
            if (sleepModePanel != null) sleepModePanel.SetActive(false);

            TimeSpan behind = now - new DateTime(schedule[currentPuffIndex]);
            statusText.text = "Next puff overdue:";
            timerText.text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)behind.TotalHours, behind.Minutes, behind.Seconds);

            if (pendingPuffsController != null)
            {
                if (!pendingPuffsController.IsPanelActive())
                    pendingPuffsController.ShowPendingPanel(overdue);
                else
                    pendingPuffsController.RefreshCount(overdue);
            }
            return;
        }

        if (pendingPuffsController != null) pendingPuffsController.HidePanel();

        menuPanel.SetActive(true);

        if (overdue == 1)
        {
            waitingPanel.SetActive(false);
            smokePromptPanel.SetActive(true);

            TimeSpan behind = now - new DateTime(schedule[currentPuffIndex]);
            statusText.text = "Time to smoke!";
            timerText.text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)behind.TotalHours, behind.Minutes, behind.Seconds);

            if (notificationSound != null && !notificationSound.isPlaying)
                notificationSound.Play();
            return;
        }

        smokePromptPanel.SetActive(false);

        if (now >= nextPuffTime)
        {
            waitingPanel.SetActive(true);
            statusText.text = "Next puff in:";
            timerText.text = "00:00:00";
        }
        else
        {
            waitingPanel.SetActive(true);

            TimeSpan timeLeft = nextPuffTime - now;
            statusText.text = "Next puff in:";
            timerText.text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                timeLeft.Hours, timeLeft.Minutes, timeLeft.Seconds);
        }
    }

    private void HideTopHalf()
    {
        waitingPanel.SetActive(false);
        smokePromptPanel.SetActive(false);
        if (sleepModePanel != null) sleepModePanel.SetActive(false);
    }

    void UpdateSleepModeUI(DateTime now)
    {
        waitingPanel.SetActive(false);
        smokePromptPanel.SetActive(false);
        menuPanel.SetActive(false);
        activePausePanel.SetActive(false);
        pausePanel.SetActive(false);

        if (sleepModePanel != null) sleepModePanel.SetActive(true);
        if (sleepStatusText != null)
        {
            TimeSpan sleepFor = now - (UserData.SleepModeStartedAt ?? now);
            string startInfo = UserData.SleepModeStartedAt.HasValue
                ? $"Sleeping since {UserData.SleepModeStartedAt.Value:HH:mm}"
                : "Sleeping";
            sleepStatusText.text = $"{startInfo}\n" +
                                   $"({sleepFor.Hours:D2}:{sleepFor.Minutes:D2} ago)\n\n" +
                                   "Tap 'I'm up now' to start your day.";
        }
    }

    void EnterSleepMode()
    {
        if (sleepMode) return;
        sleepMode = true;
        UserData.SleepModeActive = true;
        UserData.SleepModeStartedAt = TimeService.Now;
        UserData.WakeNotificationFiredAt = null;
        UserData.Save();

        NotificationScheduler.CancelAll();
        Debug.Log($"[Sleep] Entered sleep mode at {TimeService.Now}.");

        if (sleepModePanel != null) sleepModePanel.SetActive(true);
    }

    void ExitSleepMode()
    {
        if (!sleepMode) return;
        sleepMode = false;
        UserData.SleepModeActive = false;
        UserData.SleepModeStartedAt = null;
        UserData.WakeNotificationFiredAt = null;
        UserData.Save();
        Debug.Log($"[Sleep] Exited sleep mode at {TimeService.Now}.");
    }

    private bool TryRecoverFromEndDay()
    {
        int snapIndex = UserData.PreEndDayIndex;
        long snapTicks = UserData.PreEndDayTicks;

        if (snapIndex < 0 || snapTicks == 0) return false;

        UserData.PreEndDayIndex = -1;
        UserData.PreEndDayTicks = 0;

        DateTime endedAt = new DateTime(snapTicks);
        TimeSpan since = TimeService.Now - endedAt;

        if (since.TotalHours >= END_DAY_RECOVERY_HOURS)
        {
            Debug.Log($"[EndDay] Snapshot expired ({since.TotalHours:F2}h >= {END_DAY_RECOVERY_HOURS}h). Not recovering.");
            UserData.Save();
            return false;
        }

        if (snapIndex > schedule.Count) snapIndex = schedule.Count;
        if (snapIndex < 0) snapIndex = 0;

        for (int i = snapIndex; i < schedule.Count; i++)
        {
            long t = schedule[i];
            if (t < snapTicks) continue;
            var rec = UserData.GetPuffRecord(t);
            if (rec.HasValue && rec.Value.Status == PuffStatus.Erased)
                UserData.DeletePuffRecord(t);
        }

        currentPuffIndex = snapIndex;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        ReconcileCurrentIndexFromRecords();

        Debug.Log($"[EndDay] Recovered after {since.TotalMinutes:F1} min. Restored puff index to {currentPuffIndex}.");
        return true;
    }

    void EndDay()
    {
        Debug.Log("[EndDay] User ended the day.");
        NotificationScheduler.CancelAll();

        pauseEndTime = null;
        UserData.PauseEndTime = null;
        UserData.PauseStartTime = null;

        UserData.PreEndDayIndex = currentPuffIndex;
        UserData.PreEndDayTicks = TimeService.Now.Ticks;

        DateTime now = TimeService.Now;
        DateTime theoreticalWake = UserData.IsInSleepWindow(now)
            ? UserData.NextWakeAfter(now)
            : now.Date.AddDays(1).AddMinutes(UserData.WakeHour * 60 + UserData.WakeMinute);

        int eraseStart = currentPuffIndex;
        int eraseEnd = currentPuffIndex;
        while (eraseEnd < schedule.Count &&
               new DateTime(schedule[eraseEnd]) < theoreticalWake)
        {
            eraseEnd++;
        }

        MarkPuffsErased(eraseStart, eraseEnd);

        currentPuffIndex = eraseEnd;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        EnterSleepMode();
    }

    public void OnGoingToSleepClicked()
    {
        int overdue = GetOverdueCount();
        if (overdue > 0)
        {
            deferredAction = DeferredAction.EndDay;
            ShowUnaccountedPrompt(overdue);
            return;
        }
        Debug.Log("[Sleep] User tapped 'End day'.");
        EndDay();
    }

    public void OnImUpNowClicked()
    {
        DateTime now = TimeService.Now;
        Debug.Log($"[Sleep] User tapped 'I'm up now'.");

        TryRecoverFromEndDay();

        UserData.WokeEarlyToday = false;
        UserData.IdealPanelShownToday = false;
        UserData.Save();

        if (UserData.IsInSleepWindow(now))
        {
            if (confirmEarlyWakePanel != null)
            {
                if (sleepModePanel != null) sleepModePanel.SetActive(false);
                confirmEarlyWakePanel.SetActive(true);
                return;
            }
            else DoEarlyWakeYes();
            return;
        }

        ExitSleepMode();
        if (currentPuffIndex < schedule.Count)
            nextPuffTime = new DateTime(schedule[currentPuffIndex]);
        RefreshNotifications();
        UpdateUI();
    }

    public void OnEarlyWakeYesClicked()
    {
        if (confirmEarlyWakePanel != null) confirmEarlyWakePanel.SetActive(false);
        DoEarlyWakeYes();
    }

    public void OnEarlyWakeNoClicked()
    {
        if (confirmEarlyWakePanel != null) confirmEarlyWakePanel.SetActive(false);
        if (sleepModePanel != null) sleepModePanel.SetActive(true);
    }

    void DoEarlyWakeYes()
    {
        UserData.WokeEarlyToday = true;
        UserData.Save();

        ExitSleepMode();
        RefreshNotifications();
        UpdateUI();
    }

    public void OnCancelSleepClicked()
    {
        TryRecoverFromEndDay();

        ExitSleepMode();
        RefreshNotifications();
        UpdateUI();
    }

    void ValidateExtendedDay()
    {
        if (string.IsNullOrEmpty(UserData.ExtendedDayDate)) return;
        long sleepTicks = UserData.ExtendedDaySleepTicks;
        if (sleepTicks == 0) return;

        if (TimeService.Now.Ticks >= sleepTicks)
        {
            UserData.ExtendedDayDate = "";
            UserData.ExtendedDayBudget = -1;
            UserData.ExtendedDaySleepTicks = 0;
            UserData.OverBudgetDecision = 0;
            UserData.Save();
        }
    }

    public void OnOverBudgetContinueClicked()
    {
        UserData.OverBudgetDecision = 1;
        UserData.Save();
        if (overBudgetPanel != null) overBudgetPanel.SetActive(false);
        UpdateUI();
    }

    public void OnOverBudgetStopClicked()
    {
        UserData.OverBudgetDecision = 2;
        UserData.Save();
        if (overBudgetPanel != null) overBudgetPanel.SetActive(false);
        EndDay();
    }

    void CheckRecovery()
    {
        if (UserData.LastDeclinedIndex < 0) return;
        if (UserData.LastDeclinedAtTicks == 0) return;

        DateTime declinedAt = new DateTime(UserData.LastDeclinedAtTicks);
        TimeSpan since = TimeService.Now - declinedAt;

        if (since.TotalMinutes > 30)
        {
            UserData.LastDeclinedIndex = -1;
            UserData.Save();
            return;
        }

        if (recoveryPanel != null) recoveryPanel.SetActive(true);
    }

    public void OnRecoveryYesClicked()
    {
        int idx = UserData.LastDeclinedIndex;
        if (idx >= 0 && idx < schedule.Count)
            UserData.RecordPuffResolution(schedule[idx], PuffStatus.Smoked);

        UserData.DeclinedPuffs = Mathf.Max(0, UserData.DeclinedPuffs - 1);
        UserData.LogPuffToday();
        UserData.LastDeclinedIndex = -1;
        UserData.Save();
        if (recoveryPanel != null) recoveryPanel.SetActive(false);
    }

    public void OnRecoveryNoClicked()
    {
        UserData.LastDeclinedIndex = -1;
        UserData.Save();
        if (recoveryPanel != null) recoveryPanel.SetActive(false);
    }

    void OnSmokeButtonClicked()
    {
        long scheduledTicks = schedule[currentPuffIndex];
        UserData.LogPuffToday();
        UserData.RecordPuffResolution(scheduledTicks, PuffStatus.Smoked);

        currentPuffIndex++;
        if (currentPuffIndex > schedule.Count) currentPuffIndex = schedule.Count;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();
        AdvanceAfterResolve();
        MaybeShowIdealPanel();
    }

    void OnSkipButtonClicked()
    {
        long scheduledTicks = schedule[currentPuffIndex];
        UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;
        UserData.RecordPuffResolution(scheduledTicks, PuffStatus.Skipped);

        currentPuffIndex++;
        if (currentPuffIndex > schedule.Count) currentPuffIndex = schedule.Count;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();
        AdvanceAfterResolve();
    }

    void AdvanceAfterResolve()
    {
        ReconcileCurrentIndexFromRecords();

        if (currentPuffIndex >= schedule.Count) { ShowFinishedState(); return; }

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);

        while (currentPuffIndex < schedule.Count &&
               !sleepMode &&
               UserData.IsInSleepWindow(nextPuffTime))
        {
            currentPuffIndex++;
            UserData.CurrentPuffIndex = currentPuffIndex;
            if (currentPuffIndex < schedule.Count)
                nextPuffTime = new DateTime(schedule[currentPuffIndex]);
        }

        UserData.Save();
        if (currentPuffIndex >= schedule.Count) { ShowFinishedState(); return; }

        RefreshNotifications();
        UpdateUI();
    }

    void RefreshNotifications()
    {
        if (!isInitialized) return;
        if (IsPaused || sleepMode) { NotificationScheduler.CancelAll(); return; }

        NotificationScheduler.SchedulePuffs(
            schedule, currentPuffIndex, pauseEndTime, 0, UserData.PuffsPerDay);
    }

    void UpdateUI()
    {
        if (sleepMode)
        {
            if (sleepModePanel != null) sleepModePanel.SetActive(true);
        }
        else
        {
            if (sleepModePanel != null) sleepModePanel.SetActive(false);
        }
    }

    void ShowFinishedState()
    {
        waitingPanel.SetActive(false);
        smokePromptPanel.SetActive(false);
        activePausePanel.SetActive(false);
        if (sleepModePanel != null) sleepModePanel.SetActive(false);
        if (pendingPuffsController != null) pendingPuffsController.HidePanel();
        if (menuPanel != null) menuPanel.SetActive(false);
        NotificationScheduler.CancelAll();

        if (taperCompletePanel == null || schedule == null || schedule.Count == 0) return;
        if (celebrationActive) return;

        celebrationActive = true;
        DateTime startDate = new DateTime(schedule[0]).Date;
        DateTime finishDate = TimeService.Now.Date;

        taperCompletePanel.Show(startDate, finishDate,
            OnViewCalendarFromCelebration,
            OnOpenSettingsFromCelebration);
    }

    void OnViewCalendarFromCelebration()
    {
        celebrationActive = false;
        if (taperCompletePanel != null && taperCompletePanel.panelRoot != null)
            taperCompletePanel.panelRoot.SetActive(false);
        if (calendarController != null)
            calendarController.OpenCalendar();
    }

    void OnOpenSettingsFromCelebration()
    {
        celebrationActive = false;
        if (taperCompletePanel != null && taperCompletePanel.panelRoot != null)
            taperCompletePanel.panelRoot.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    void OnCalendarClosed()
    {
        if (currentPuffIndex >= schedule.Count && !celebrationActive)
            ShowFinishedState();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) OnResumeFromBackground();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;
        OnResumeFromBackground();
    }

    private void OnResumeFromBackground()
    {
        if (!isInitialized) return;
        RefreshNotifications();

#if UNITY_IOS
        StartCoroutine(ResumeRoutine());
#else
        HandlePendingPuffsOnResume();
#endif
    }

#if UNITY_IOS
    private IEnumerator ResumeRoutine()
    {
        var op = iOSNotificationCenter.QueryLastRespondedNotification();
        yield return op;

        if (op.Notification != null &&
            !string.IsNullOrEmpty(op.Notification.Identifier) &&
            op.Notification.Identifier.StartsWith("puff_") &&
            lastProcessedNotificationId != op.Notification.Identifier)
        {
            lastProcessedNotificationId = op.Notification.Identifier;
            ApplyNotificationAnswer(op.Notification.Identifier, op.Notification.ActionId);
        }

        HandlePendingPuffsOnResume();
    }
#endif

    private void HandlePendingPuffsOnResume()
    {
        if (pendingPuffsController != null && pendingPuffsController.IsPanelActive())
            return;

        if (currentPuffIndex >= schedule.Count)
        {
            if (!celebrationActive) ShowFinishedState();
            return;
        }

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);
        if (!sleepMode && UserData.IsInSleepWindow(nextPuffTime))
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

        CheckRecovery();
        RefreshNotifications();
        UpdateUI();
    }

    void OpenPauseMenu()
    {
        int overdue = GetOverdueCount();
        if (overdue > 0)
        {
            deferredAction = DeferredAction.Pause;
            ShowUnaccountedPrompt(overdue);
            return;
        }
        menuPanel.SetActive(false);
        OpenPausePicker();
    }

    void OpenPausePicker()
    {
        if (pickerManager == null)
        {
            Debug.LogWarning("[Pause] Picker not wired, falling back to old pause panel.");
            pausePanel.SetActive(true);
            return;
        }

        int hoursCount = PICKER_HOURS_MAX + 1;
        string[] hours = new string[hoursCount];
        for (int i = 0; i < hoursCount; i++)
        {
            int h = i;
            hours[i] = h == 1 ? "1 hr" : $"{h} hr";
        }

        string[] minutes = new string[PICKER_MINUTE_STEPS];
        for (int i = 0; i < PICKER_MINUTE_STEPS; i++)
        {
            int m = i * PICKER_MINUTE_STEP;
            minutes[i] = $"{m} min";
        }

        pickerManager.Show(
            "Pause for how long?",
            hours, minutes,
            pendingPauseHours, pendingPauseMinutes / PICKER_MINUTE_STEP,
            OnPausePicked, OnPausePickCancelled);
    }

    void OnPausePicked(int hoursIndex, int minutesIndex)
    {
        pendingPauseHours = hoursIndex;
        pendingPauseMinutes = minutesIndex * PICKER_MINUTE_STEP;

        int totalMinutes = pendingPauseHours * 60 + pendingPauseMinutes;
        if (totalMinutes < 5) totalMinutes = 5;

        DateTime now = TimeService.Now;
        pauseEndTime = now.AddMinutes(totalMinutes);
        UserData.PauseStartTime = now;
        UserData.PauseEndTime = pauseEndTime;
        UserData.Save();

        NotificationScheduler.CancelAll();
        Debug.Log($"[Pause] Paused for {totalMinutes} min until {pauseEndTime.Value}");
        UpdateUI();
    }

    void OnPausePickCancelled()
    {
        Debug.Log("[Pause] Cancelled by user.");
        menuPanel.SetActive(true);
    }

    void ConfirmPause()
    {
        if (pauseHoursInput == null)
        {
            Debug.LogWarning("[Pause] ConfirmPause called with no input field.");
            return;
        }
        if (!float.TryParse(pauseHoursInput.text.Trim(), out float hours) || hours <= 0)
        {
            Debug.LogWarning("[Pause] Invalid input.");
            return;
        }
        if (hours > 48f) hours = 48f;

        DateTime now = TimeService.Now;
        pauseEndTime = now.AddHours(hours);
        UserData.PauseStartTime = now;
        UserData.PauseEndTime = pauseEndTime;
        UserData.Save();

        pausePanel.SetActive(false);
        NotificationScheduler.CancelAll();
        Debug.Log($"Paused until {pauseEndTime.Value}");
    }

    void CancelPause()
    {
        pausePanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    void EndPause()
    {
        DateTime? start = UserData.PauseStartTime;
        DateTime now = TimeService.Now;
        bool needsPrompt = WasPauseLongOrCrossedSleep(start, now);

        if (needsPrompt)
        {
            pauseEndTime = now;
            UserData.PauseEndTime = now;
            UserData.Save();
            Debug.Log("[Pause] Ended early but long/crossed sleep. Showing prompt.");
            ShowPauseResumePrompt();
            return;
        }

        if (start.HasValue)
            ErasePuffsInPauseWindow(start.Value, now);

        pauseEndTime = null;
        UserData.PauseEndTime = null;
        UserData.PauseStartTime = null;
        UserData.Save();

        if (UserData.IsInSleepWindow(nextPuffTime))
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

        RefreshNotifications();
        Debug.Log("Pause ended early by user.");
    }

    void OpenSettings() { menuPanel.SetActive(false); settingsPanel.SetActive(true); }

    void CloseSettings()
    {
        settingsPanel.SetActive(false);

        if (currentPuffIndex >= schedule.Count)
        {
            celebrationActive = false;
            ShowFinishedState();
        }
        else
        {
            menuPanel.SetActive(true);
        }
    }

    void ShowResetConfirmation() { settingsPanel.SetActive(false); confirmResetPanel.SetActive(true); }
    void CancelReset() { confirmResetPanel.SetActive(false); settingsPanel.SetActive(true); }
    void ShowResetProgressConfirmation() { settingsPanel.SetActive(false); confirmResetProgressPanel.SetActive(true); }
    void CancelResetProgress() { confirmResetProgressPanel.SetActive(false); settingsPanel.SetActive(true); }

    void ConfirmResetProgress()
    {
        NotificationScheduler.CancelAll();
        UserData.ResetProgress();
        TaperEngine.GenerateAndSaveSchedule();
        SceneManager.LoadScene("MainApp");
    }

    void ConfirmReset()
    {
        NotificationScheduler.CancelAll();
        UserData.ResetAll();
        SceneManager.LoadScene("Onboarding");
    }

    public void DebugTriggerTaperComplete()
    {
        if (schedule == null || schedule.Count == 0)
        {
            Debug.LogWarning("[Debug] Cannot trigger: no schedule loaded.");
            return;
        }

        currentPuffIndex = schedule.Count;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        celebrationActive = false;
        ShowFinishedState();
    }
}