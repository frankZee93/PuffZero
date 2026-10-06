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
    public GameObject finishedPanel;
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

    [Header("Pause Settings")]
    public TMP_InputField pauseHoursInput;

    private const int PUFF_EXPIRY_MINUTES = 20;
    private const int CONSECUTIVE_MISSES_FOR_AUTO_SLEEP = 5;
    private const int CONSECUTIVE_MISSES_HARD_OVERRIDE = 10;
    private const int SLEEP_MODE_MAX_HOURS = 24;

    [Header("Audio")]
    public AudioSource notificationSound;

    private List<long> schedule;
    private int currentPuffIndex = 0;
    private DateTime nextPuffTime;
    private bool isInitialized = false;

    private bool sleepMode = false;
    private int consecutiveMisses = 0;
    private DateTime? smokePromptShownAt = null;

    private DateTime? pauseEndTime = null;
    private bool IsPaused => pauseEndTime.HasValue;

    // Tracks the last notification response we already handled, to prevent double-processing.
#if UNITY_IOS
    private string lastProcessedNotificationId = null;
#endif

    // ================================================================
    // LIFECYCLE
    // ================================================================

    void Start()
    {
        schedule = UserData.LoadSchedule();

        if (schedule == null || schedule.Count == 0)
        {
            statusText.text = "No schedule found. Please complete onboarding.";
            return;
        }

        pauseEndTime = UserData.PauseEndTime;
        if (pauseEndTime.HasValue && DateTime.Now >= pauseEndTime.Value)
        {
            pauseEndTime = null;
            UserData.PauseEndTime = null;
            UserData.PauseStartTime = null;
        }

        sleepMode = UserData.SleepModeActive;
        if (sleepMode && UserData.SleepModeStartedAt.HasValue)
        {
            TimeSpan activeFor = DateTime.Now - UserData.SleepModeStartedAt.Value;
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

        DateTime now = DateTime.Now;
        DateTime? pauseStart = UserData.PauseStartTime;
        DateTime? pauseEnd = pauseEndTime;

        while (currentPuffIndex < schedule.Count &&
               new DateTime(schedule[currentPuffIndex]) <= now)
        {
            DateTime puffTime = new DateTime(schedule[currentPuffIndex]);

            if (sleepMode)
            {
                Debug.Log($"Puff {currentPuffIndex} at {puffTime} skipped — sleep mode active.");
            }
            else
            {
                bool wasDuringPause = pauseStart.HasValue &&
                                      pauseEnd.HasValue &&
                                      puffTime >= pauseStart.Value &&
                                      puffTime <= pauseEnd.Value;

                if (wasDuringPause)
                {
                    Debug.Log($"Puff {currentPuffIndex} at {puffTime} skipped — inside pause window.");
                }
                else
                {
                    Debug.Log($"Puff {currentPuffIndex} at {puffTime} missed — declined.");
                    UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;
                    consecutiveMisses++;
                    UserData.LastDeclinedAtTicks = puffTime.Ticks;
                    UserData.LastDeclinedIndex = currentPuffIndex;
                }
            }

            currentPuffIndex++;
        }

        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        CheckAutoSleepTrigger();
        ValidateExtendedDay();

        if (currentPuffIndex >= schedule.Count)
        {
            ShowFinishedState();
            return;
        }

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);

        if (!sleepMode && UserData.IsInSleepWindow(nextPuffTime))
        {
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);
            Debug.Log($"[MainApp] Next puff pushed out of sleep window -> {nextPuffTime}");
        }

        smokeButton.onClick.AddListener(OnSmokeButtonClicked);
        skipButton.onClick.AddListener(OnSkipButtonClicked);
        pauseButton.onClick.AddListener(OpenPauseMenu);
        confirmPauseButton.onClick.AddListener(ConfirmPause);
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

        CheckRecovery();

        if (!IsPaused && !sleepMode)
            RefreshNotifications();

        isInitialized = true;
        UpdateUI();

#if UNITY_IOS
        // Handle any notification action that was tapped while the app was closed.
        ProcessPendingNotificationAction();
#endif
    }

    void Update()
    {
        if (!isInitialized || currentPuffIndex >= schedule.Count) return;

        if (pausePanel.activeSelf) return;
        if (settingsPanel.activeSelf) return;
        if (confirmResetPanel.activeSelf) return;
        if (confirmResetProgressPanel != null && confirmResetProgressPanel.activeSelf) return;
        if (confirmEarlyWakePanel != null && confirmEarlyWakePanel.activeSelf) return;
        if (overBudgetPanel != null && overBudgetPanel.activeSelf) return;
        if (recoveryPanel != null && recoveryPanel.activeSelf) return;

        DateTime now = DateTime.Now;

        ValidateExtendedDay();

        if (sleepMode)
        {
            UpdateSleepModeUI(now);
            return;
        }

        if (IsPaused && now >= pauseEndTime.Value)
        {
            pauseEndTime = null;
            UserData.PauseEndTime = null;
            UserData.PauseStartTime = null;

            if (UserData.IsInSleepWindow(nextPuffTime))
                nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

            RefreshNotifications();
            Debug.Log("Pause expired. Resuming.");
        }

        if (IsPaused)
        {
            waitingPanel.SetActive(false);
            smokePromptPanel.SetActive(false);
            finishedPanel.SetActive(false);
            menuPanel.SetActive(false);
            activePausePanel.SetActive(true);
            if (sleepModePanel != null) sleepModePanel.SetActive(false);

            TimeSpan left = pauseEndTime.Value - now;
            if (left.TotalHours >= 1)
                pauseStatusText.text = string.Format("Paused — {0:D2}:{1:D2}:{2:D2} left",
                    left.Hours, left.Minutes, left.Seconds);
            else
                pauseStatusText.text = string.Format("Paused — {0:D2}:{1:D2} left",
                    left.Minutes, left.Seconds);
            return;
        }

        activePausePanel.SetActive(false);
        menuPanel.SetActive(true);

        if (now >= nextPuffTime && smokePromptShownAt.HasValue)
        {
            TimeSpan promptAge = now - smokePromptShownAt.Value;
            if (promptAge.TotalMinutes >= PUFF_EXPIRY_MINUTES)
            {
                Debug.Log($"[MainApp] Puff expired after {promptAge.TotalMinutes:F0} min. Counting as declined.");
                UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;
                consecutiveMisses++;
                smokePromptShownAt = null;
                AdvanceToNextPuff(false);
                CheckAutoSleepTrigger();
                return;
            }
        }

        if (now >= nextPuffTime)
        {
            waitingPanel.SetActive(false);
            smokePromptPanel.SetActive(true);
            finishedPanel.SetActive(false);
            statusText.text = "Time to smoke!";
            timerText.text = "00:00";

            if (!smokePromptShownAt.HasValue)
                smokePromptShownAt = now;

            if (notificationSound != null && !notificationSound.isPlaying)
                notificationSound.Play();
        }
        else
        {
            waitingPanel.SetActive(true);
            smokePromptPanel.SetActive(false);
            finishedPanel.SetActive(false);
            smokePromptShownAt = null;

            TimeSpan timeLeft = nextPuffTime - now;
            statusText.text = "Next puff in:";
            timerText.text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                timeLeft.Hours, timeLeft.Minutes, timeLeft.Seconds);
        }
    }

    // ================================================================
    // NOTIFICATION ACTION HANDLING
    // ================================================================

#if UNITY_IOS
    void ProcessPendingNotificationAction()
    {
        StartCoroutine(HandleNotificationActionCoroutine());
    }

    private IEnumerator HandleNotificationActionCoroutine()
    {
        // Give Unity's notification system a moment to deliver the response.
        yield return null;

        var notification = iOSNotificationCenter.GetLastRespondedNotification();
        if (notification == null) yield break;

        // Only handle puff notifications.
        if (string.IsNullOrEmpty(notification.Identifier) ||
            !notification.Identifier.StartsWith("puff_")) yield break;

        // Prevent double-processing of the same notification.
        if (lastProcessedNotificationId == notification.Identifier) yield break;

        // The action identifier comes through the Data field via GameCI's wrapper.
        // iOSNotificationCenter delivers the last action via a separate API on some
        // versions; we read it defensively.
        string actionId = null;
#if UNITY_IOS
        actionId = iOSNotificationCenter.GetLastRespondedNotificationActionId();
#endif

        Debug.Log($"[Notif] Response — id={notification.Identifier}, action={actionId}");

        lastProcessedNotificationId = notification.Identifier;

        if (actionId == NotificationScheduler.ActionSmokedId)
        {
            if (currentPuffIndex < schedule.Count)
            {
                Debug.Log("[Notif] Background action: I Smoked.");
                UserData.LogPuffToday();
                consecutiveMisses = 0;
                smokePromptShownAt = null;
                AdvanceToNextPuff(true);
            }
        }
        else if (actionId == NotificationScheduler.ActionSkipId)
        {
            if (currentPuffIndex < schedule.Count)
            {
                Debug.Log("[Notif] Background action: Skip.");
                UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;
                consecutiveMisses++;
                smokePromptShownAt = null;
                AdvanceToNextPuff(false);
                CheckAutoSleepTrigger();
            }
        }
        else
        {
            // Body tap — no action.
            Debug.Log("[Notif] User tapped the notification body (no action).");
        }
    }
#endif

    // ================================================================
    // SLEEP MODE
    // ================================================================

    void UpdateSleepModeUI(DateTime now)
    {
        waitingPanel.SetActive(false);
        smokePromptPanel.SetActive(false);
        finishedPanel.SetActive(false);
        menuPanel.SetActive(false);
        activePausePanel.SetActive(false);
        pausePanel.SetActive(false);

        if (sleepModePanel != null) sleepModePanel.SetActive(true);
        if (sleepStatusText != null)
        {
            TimeSpan sleepFor = now - (UserData.SleepModeStartedAt ?? now);
            string startInfo = UserData.SleepModeStartedAt.HasValue
                ? $"Sleep mode since {UserData.SleepModeStartedAt.Value:HH:mm}"
                : "Sleep mode active";
            sleepStatusText.text = $"{startInfo}\n" +
                                   $"({sleepFor.Hours:D2}:{sleepFor.Minutes:D2} ago)\n\n" +
                                   "Tap 'I'm up now' to start your day,\n" +
                                   "or 'Cancel sleep' to undo.";
        }
    }

    void EnterSleepMode()
    {
        if (sleepMode) return;

        sleepMode = true;
        UserData.SleepModeActive = true;
        UserData.SleepModeStartedAt = DateTime.Now;
        UserData.WakeNotificationFiredAt = null;
        UserData.Save();

        consecutiveMisses = 0;
        smokePromptShownAt = null;

        NotificationScheduler.CancelAll();
        Debug.Log($"[Sleep] Entered sleep mode at {DateTime.Now}.");

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

        consecutiveMisses = 0;
        smokePromptShownAt = null;

        Debug.Log($"[Sleep] Exited sleep mode at {DateTime.Now}.");
    }

    void CheckAutoSleepTrigger()
    {
        if (sleepMode) return;

        bool inSleepWindow = UserData.IsInSleepWindow(DateTime.Now);

        bool shouldEnter =
            (consecutiveMisses >= CONSECUTIVE_MISSES_FOR_AUTO_SLEEP && inSleepWindow) ||
            (consecutiveMisses >= CONSECUTIVE_MISSES_HARD_OVERRIDE);

        if (shouldEnter)
        {
            Debug.Log($"[Sleep] Auto-trigger fired (misses={consecutiveMisses}, inSleepWindow={inSleepWindow}).");
            EnterSleepMode();
        }
    }

    public void OnGoingToSleepClicked()
    {
        Debug.Log("[Sleep] User tapped 'Going to sleep'.");
        EnterSleepMode();
    }

    public void OnImUpNowClicked()
    {
        DateTime now = DateTime.Now;
        Debug.Log($"[Sleep] User tapped 'I'm up now' (inSleepWindow={UserData.IsInSleepWindow(now)}).");

        if (UserData.IsInSleepWindow(now))
        {
            if (confirmEarlyWakePanel != null)
            {
                if (sleepModePanel != null) sleepModePanel.SetActive(false);
                confirmEarlyWakePanel.SetActive(true);
                Debug.Log("[Sleep] Showing early-wake confirmation.");
                return;
            }
            else
            {
                Debug.LogWarning("[Sleep] confirmEarlyWakePanel missing; proceeding as if yes.");
                DoEarlyWakeYes();
            }
            return;
        }

        RedistributeRemainingPuffsToday();
        ExitSleepMode();

        if (currentPuffIndex < schedule.Count)
            nextPuffTime = new DateTime(schedule[currentPuffIndex]);

        RefreshNotifications();
        UpdateUI();
    }

    public void OnEarlyWakeYesClicked()
    {
        Debug.Log("[Sleep] User confirmed early wake.");
        if (confirmEarlyWakePanel != null) confirmEarlyWakePanel.SetActive(false);
        DoEarlyWakeYes();
    }

    public void OnEarlyWakeNoClicked()
    {
        Debug.Log("[Sleep] User declined early wake — staying in sleep mode.");
        if (confirmEarlyWakePanel != null) confirmEarlyWakePanel.SetActive(false);
        if (sleepModePanel != null) sleepModePanel.SetActive(true);
    }

    void DoEarlyWakeYes()
    {
        StartExtendedDay();
        ExitSleepMode();
        RefreshNotifications();
        UpdateUI();
    }

    public void OnCancelSleepClicked()
    {
        Debug.Log("[Sleep] User tapped 'Cancel sleep'.");
        ExitSleepMode();

        DateTime now = DateTime.Now;
        while (currentPuffIndex < schedule.Count &&
               new DateTime(schedule[currentPuffIndex]) <= now)
        {
            Debug.Log($"[CancelSleep] Skipping past-due puff {currentPuffIndex}.");
            currentPuffIndex++;
        }
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        if (currentPuffIndex >= schedule.Count)
        {
            ShowFinishedState();
            return;
        }

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);
        if (UserData.IsInSleepWindow(nextPuffTime))
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

        RefreshNotifications();
        UpdateUI();
    }

    // ================================================================
    // EXTENDED DAY
    // ================================================================

    void StartExtendedDay()
    {
        DateTime now = DateTime.Now;
        int wakeMin = UserData.WakeHour * 60 + UserData.WakeMinute;
        int A = UserData.AwakeMinutes;

        DateTime today = now.Date;
        DateTime todayWake = today.AddMinutes(wakeMin);
        DateTime todaySleep = todayWake.AddMinutes(A);

        if (now >= todaySleep)
        {
            Debug.LogWarning("[ExtDay] now already past today's sleep. Bailing.");
            return;
        }

        int startIdx = currentPuffIndex;
        int endIdx = startIdx - 1;
        for (int i = currentPuffIndex; i < schedule.Count; i++)
        {
            DateTime t = new DateTime(schedule[i]);
            if (t >= todaySleep) break;
            endIdx = i;
        }

        if (endIdx < startIdx)
        {
            Debug.Log("[ExtDay] No puffs to redistribute for today.");
            return;
        }

        int budget = endIdx - startIdx + 1;
        float interval = (float)A / budget;
        double extWindowMin = (todaySleep - now).TotalMinutes;
        int extendedCount = Mathf.Max(1, Mathf.FloorToInt((float)(extWindowMin / interval)));

        var newTicks = new List<long>(extendedCount);
        DateTime cursor = now;
        for (int i = 0; i < extendedCount; i++)
        {
            cursor = cursor.AddMinutes(interval);
            newTicks.Add(cursor.Ticks);
        }

        var rebuilt = new List<long>(schedule.Count - (endIdx - startIdx + 1) + extendedCount);
        for (int i = 0; i < startIdx; i++) rebuilt.Add(schedule[i]);
        rebuilt.AddRange(newTicks);
        for (int i = endIdx + 1; i < schedule.Count; i++) rebuilt.Add(schedule[i]);

        schedule = rebuilt;
        UserData.SaveSchedulePreserveProgress(schedule);

        currentPuffIndex = startIdx;
        UserData.CurrentPuffIndex = currentPuffIndex;

        UserData.ExtendedDayDate = today.ToString("yyyy-MM-dd");
        UserData.ExtendedDayBudget = startIdx + budget;
        UserData.ExtendedDaySleepTicks = todaySleep.Ticks;
        UserData.OverBudgetDecision = 0;
        UserData.Save();

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);

        Debug.Log($"[ExtDay] Budget {budget}, interval {interval:F2} min, " +
                  $"extended count {extendedCount}, trigger index {startIdx + budget}.");
    }

    void ValidateExtendedDay()
    {
        if (string.IsNullOrEmpty(UserData.ExtendedDayDate)) return;

        long sleepTicks = UserData.ExtendedDaySleepTicks;
        if (sleepTicks == 0) return;

        if (DateTime.Now.Ticks >= sleepTicks)
        {
            Debug.Log("[ExtDay] Extended day ended — clearing state.");
            UserData.ExtendedDayDate = "";
            UserData.ExtendedDayBudget = -1;
            UserData.ExtendedDaySleepTicks = 0;
            UserData.OverBudgetDecision = 0;
            UserData.Save();
        }
    }

    void CheckOverBudget()
    {
        if (UserData.OverBudgetDecision != 0) return;
        if (string.IsNullOrEmpty(UserData.ExtendedDayDate)) return;

        if (currentPuffIndex >= UserData.ExtendedDayBudget)
        {
            Debug.Log("[OverBudget] Triggered.");
            if (overBudgetPanel != null)
                overBudgetPanel.SetActive(true);
        }
    }

    public void OnOverBudgetContinueClicked()
    {
        Debug.Log("[OverBudget] User chose to continue.");
        UserData.OverBudgetDecision = 1;
        UserData.Save();
        if (overBudgetPanel != null) overBudgetPanel.SetActive(false);
        UpdateUI();
    }

    public void OnOverBudgetStopClicked()
    {
        Debug.Log("[OverBudget] User chose to stop until sleep.");
        UserData.OverBudgetDecision = 2;
        UserData.Save();
        if (overBudgetPanel != null) overBudgetPanel.SetActive(false);

        long sleepTicks = UserData.ExtendedDaySleepTicks;
        while (currentPuffIndex < schedule.Count &&
               (sleepTicks == 0 || schedule[currentPuffIndex] < sleepTicks))
        {
            currentPuffIndex++;
        }
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        if (currentPuffIndex >= schedule.Count)
        {
            ShowFinishedState();
            return;
        }

        nextPuffTime = new DateTime(schedule[currentPuffIndex]);
        RefreshNotifications();
        UpdateUI();
    }

    // ================================================================
    // RECOVERY
    // ================================================================

    void CheckRecovery()
    {
        if (UserData.LastDeclinedIndex < 0) return;
        if (UserData.LastDeclinedAtTicks == 0) return;

        DateTime declinedAt = new DateTime(UserData.LastDeclinedAtTicks);
        TimeSpan since = DateTime.Now - declinedAt;

        if (since.TotalMinutes > 30)
        {
            UserData.LastDeclinedIndex = -1;
            UserData.Save();
            return;
        }

        if (recoveryPanel != null)
        {
            recoveryPanel.SetActive(true);
            Debug.Log($"[Recovery] Recent decline at {declinedAt:HH:mm} — showing recovery panel.");
        }
    }

    public void OnRecoveryYesClicked()
    {
        Debug.Log("[Recovery] User says they DID smoke — correcting.");
        UserData.DeclinedPuffs = Mathf.Max(0, UserData.DeclinedPuffs - 1);
        UserData.LogPuffToday();
        UserData.LastDeclinedIndex = -1;
        UserData.Save();

        if (recoveryPanel != null) recoveryPanel.SetActive(false);
    }

    public void OnRecoveryNoClicked()
    {
        Debug.Log("[Recovery] User confirms they did NOT smoke.");
        UserData.LastDeclinedIndex = -1;
        UserData.Save();
        if (recoveryPanel != null) recoveryPanel.SetActive(false);
    }

    // ================================================================
    // REDISTRIBUTE
    // ================================================================

    void RedistributeRemainingPuffsToday()
    {
        DateTime now = DateTime.Now;
        int wakeMin = UserData.WakeHour * 60 + UserData.WakeMinute;
        int A = UserData.AwakeMinutes;

        DateTime today = now.Date;
        DateTime todayWake = today.AddMinutes(wakeMin);
        DateTime windowStart, windowEnd;

        if (now >= todayWake)
        {
            windowStart = now;
            windowEnd = todayWake.AddMinutes(A);
        }
        else
        {
            DateTime yesterdayWake = todayWake.AddDays(-1);
            DateTime yesterdaySleep = yesterdayWake.AddMinutes(A);
            if (now < yesterdaySleep)
            {
                windowStart = now;
                windowEnd = yesterdaySleep;
            }
            else
            {
                windowStart = now;
                windowEnd = todayWake.AddMinutes(A);
            }
        }

        if (windowEnd <= windowStart) return;

        int startIdx = -1, endIdx = -1;
        for (int i = currentPuffIndex; i < schedule.Count; i++)
        {
            DateTime t = new DateTime(schedule[i]);
            if (t >= windowEnd) break;
            if (t >= now)
            {
                if (startIdx == -1) startIdx = i;
                endIdx = i;
            }
        }

        if (startIdx == -1) return;

        int puffCount = endIdx - startIdx + 1;
        double remainingMin = (windowEnd - now).TotalMinutes;
        float interval = (float)(remainingMin / puffCount);

        var newTicks = new List<long>(puffCount);
        DateTime cursor = now;
        for (int i = 0; i < puffCount; i++)
        {
            cursor = cursor.AddMinutes(interval);
            newTicks.Add(cursor.Ticks);
        }

        var rebuilt = new List<long>(schedule.Count);
        for (int i = 0; i < startIdx; i++) rebuilt.Add(schedule[i]);
        rebuilt.AddRange(newTicks);
        for (int i = endIdx + 1; i < schedule.Count; i++) rebuilt.Add(schedule[i]);

        schedule = rebuilt;
        UserData.SaveSchedulePreserveProgress(schedule);

        currentPuffIndex = startIdx;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        Debug.Log($"[Redistribute] {puffCount} puffs over {remainingMin:F0} min (interval {interval:F2} min).");
    }

    // ================================================================
    // PUFF BUTTONS
    // ================================================================

    void OnSmokeButtonClicked()
    {
        Debug.Log("Puff logged at: " + DateTime.Now);
        UserData.LogPuffToday();
        consecutiveMisses = 0;
        smokePromptShownAt = null;
        AdvanceToNextPuff(true);
    }

    void OnSkipButtonClicked()
    {
        Debug.Log("Puff declined at: " + DateTime.Now);
        UserData.DeclinedPuffs = UserData.DeclinedPuffs + 1;
        consecutiveMisses++;
        smokePromptShownAt = null;
        AdvanceToNextPuff(false);
        CheckAutoSleepTrigger();
    }

    void AdvanceToNextPuff(bool wasSmoke)
    {
        currentPuffIndex++;
        UserData.CurrentPuffIndex = currentPuffIndex;
        UserData.Save();

        if (currentPuffIndex < schedule.Count)
        {
            nextPuffTime = new DateTime(schedule[currentPuffIndex]);

            while (currentPuffIndex < schedule.Count &&
                   UserData.IsInSleepWindow(nextPuffTime))
            {
                Debug.Log($"[MainApp] Skipping puff {currentPuffIndex} inside sleep window.");
                currentPuffIndex++;
                UserData.CurrentPuffIndex = currentPuffIndex;
                if (currentPuffIndex < schedule.Count)
                    nextPuffTime = new DateTime(schedule[currentPuffIndex]);
            }

            UserData.Save();

            if (currentPuffIndex >= schedule.Count)
            {
                ShowFinishedState();
                return;
            }

            CheckOverBudget();

            RefreshNotifications();
            UpdateUI();
        }
        else
        {
            ShowFinishedState();
        }
    }

    // ================================================================
    // NOTIFICATIONS / UI
    // ================================================================

    void RefreshNotifications()
    {
        if (!isInitialized) return;
        if (IsPaused || sleepMode) { NotificationScheduler.CancelAll(); return; }

        NotificationScheduler.SchedulePuffs(
            schedule,
            currentPuffIndex,
            pauseEndTime,
            0,
            UserData.PuffsPerDay);
    }

    void UpdateUI()
    {
        smokePromptPanel.SetActive(false);
        waitingPanel.SetActive(true);
        finishedPanel.SetActive(false);
        activePausePanel.SetActive(false);
        if (sleepModePanel != null) sleepModePanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    void ShowFinishedState()
    {
        waitingPanel.SetActive(false);
        smokePromptPanel.SetActive(false);
        finishedPanel.SetActive(true);
        activePausePanel.SetActive(false);
        if (sleepModePanel != null) sleepModePanel.SetActive(false);
        statusText.text = "You have completed your taper!";
        timerText.text = "";
        NotificationScheduler.CancelAll();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) RefreshNotifications();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) return;

        RefreshNotifications();

#if UNITY_IOS
        ProcessPendingNotificationAction();
#endif
    }

    // ================================================================
    // PAUSE
    // ================================================================

    void OpenPauseMenu()
    {
        menuPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    void ConfirmPause()
    {
        if (!float.TryParse(pauseHoursInput.text.Trim(), out float hours) || hours <= 0)
        {
            Debug.LogWarning("[Pause] Invalid input.");
            return;
        }

        if (hours > 48f)
        {
            Debug.LogWarning($"[Pause] Capped from {hours} to 48 hours.");
            hours = 48f;
        }

        DateTime now = DateTime.Now;
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
        pauseEndTime = null;
        UserData.PauseEndTime = null;
        UserData.PauseStartTime = null;
        UserData.Save();

        if (UserData.IsInSleepWindow(nextPuffTime))
            nextPuffTime = UserData.NextWakeAfter(nextPuffTime);

        RefreshNotifications();
        Debug.Log("Pause ended early by user.");
    }

    // ================================================================
    // SETTINGS / RESET
    // ================================================================

    void OpenSettings() { menuPanel.SetActive(false); settingsPanel.SetActive(true); }
    void CloseSettings() { settingsPanel.SetActive(false); menuPanel.SetActive(true); }
    void ShowResetConfirmation() { settingsPanel.SetActive(false); confirmResetPanel.SetActive(true); }
    void CancelReset() { confirmResetPanel.SetActive(false); settingsPanel.SetActive(true); }
    void ShowResetProgressConfirmation()
    {
        settingsPanel.SetActive(false);
        confirmResetProgressPanel.SetActive(true);
    }

    void CancelResetProgress()
    {
        confirmResetProgressPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    void ConfirmResetProgress()
    {
        Debug.Log("[Reset] Progress reset — regenerating schedule with current parameters.");
        NotificationScheduler.CancelAll();
        UserData.ResetProgress();
        TaperEngine.GenerateAndSaveSchedule();
        SceneManager.LoadScene("MainApp");
    }

    void ConfirmReset()
    {
        Debug.Log("Resetting app — wiping all data.");
        NotificationScheduler.CancelAll();
        UserData.ResetAll();
        SceneManager.LoadScene("Onboarding");
    }
}