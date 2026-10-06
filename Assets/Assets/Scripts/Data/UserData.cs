using UnityEngine;
using System;
using System.Collections.Generic;

public static class UserData
{
    private const string KEY_WAKE_HOUR = "pz_wake_hour";
    private const string KEY_WAKE_MIN = "pz_wake_min";
    private const string KEY_SLEEP_HOUR = "pz_sleep_hour";
    private const string KEY_SLEEP_MIN = "pz_sleep_min";
    private const string KEY_PUFFS_PER_DAY = "pz_puffs_per_day";
    private const string KEY_TAPER_DAYS = "pz_taper_days";
    private const string KEY_SCHEDULE = "pz_schedule";
    private const string KEY_ONBOARDED = "pz_onboarded";
    private const string KEY_PUFF_INDEX = "pz_puff_index";
    private const string KEY_DECLINED_TOTAL = "pz_declined_total";
    private const string KEY_PAUSE_END_TICKS = "pz_pause_end_ticks";
    private const string KEY_PAUSE_START_TICKS = "pz_pause_start_ticks";
    private const string KEY_GEN_OFFSET = "pz_gen_offset_minutes";
    private const string KEY_SLEEP_MODE_ACTIVE = "pz_sleep_mode_active";
    private const string KEY_SLEEP_MODE_START_TICKS = "pz_sleep_mode_start_ticks";
    private const string KEY_WAKE_NOTIF_FIRED_TICKS = "pz_wake_notif_fired_ticks";
    private const string KEY_EXT_BUDGET = "pz_ext_budget";
    private const string KEY_EXT_DATE = "pz_ext_date";
    private const string KEY_OB_DECISION = "pz_ob_decision";
    private const string KEY_EXT_SLEEP_TICKS = "pz_ext_sleep_ticks";
    private const string KEY_LAST_DECLINED_TICKS = "pz_last_declined_ticks";
    private const string KEY_LAST_DECLINED_INDEX = "pz_last_declined_index";
    private const string LOG_KEY_PREFIX = "pz_log_";

    // --- Wake / Sleep ---
    public static int WakeHour { get => PlayerPrefs.GetInt(KEY_WAKE_HOUR, 7); set => PlayerPrefs.SetInt(KEY_WAKE_HOUR, value); }
    public static int WakeMinute { get => PlayerPrefs.GetInt(KEY_WAKE_MIN, 0); set => PlayerPrefs.SetInt(KEY_WAKE_MIN, value); }
    public static int SleepHour { get => PlayerPrefs.GetInt(KEY_SLEEP_HOUR, 23); set => PlayerPrefs.SetInt(KEY_SLEEP_HOUR, value); }
    public static int SleepMinute { get => PlayerPrefs.GetInt(KEY_SLEEP_MIN, 0); set => PlayerPrefs.SetInt(KEY_SLEEP_MIN, value); }

    // --- Puffs per day ---
    public static int PuffsPerDay
    {
        get => PlayerPrefs.GetInt(KEY_PUFFS_PER_DAY, 20);
        set => PlayerPrefs.SetInt(KEY_PUFFS_PER_DAY, value);
    }

    // --- Taper duration ---
    public static float TaperDays
    {
        get => PlayerPrefs.GetFloat(KEY_TAPER_DAYS, 90f);
        set => PlayerPrefs.SetFloat(KEY_TAPER_DAYS, value);
    }

    // --- Onboarding flag ---
    public static bool Onboarded
    {
        get => PlayerPrefs.GetInt(KEY_ONBOARDED, 0) == 1;
        set => PlayerPrefs.SetInt(KEY_ONBOARDED, value ? 1 : 0);
    }

    // --- Schedule ---
    public static void SaveSchedule(List<long> ticks)
    {
        PlayerPrefs.SetString(KEY_SCHEDULE, string.Join(",", ticks));
        CurrentPuffIndex = 0;
        DeclinedPuffs = 0;
    }

    public static void SaveSchedulePreserveProgress(List<long> ticks)
    {
        PlayerPrefs.SetString(KEY_SCHEDULE, string.Join(",", ticks));
    }

    public static List<long> LoadSchedule()
    {
        var result = new List<long>();
        string raw = PlayerPrefs.GetString(KEY_SCHEDULE, "");
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var s in raw.Split(','))
            if (long.TryParse(s, out long t)) result.Add(t);
        return result;
    }

    // --- Schedule progress ---
    public static int CurrentPuffIndex
    {
        get => PlayerPrefs.GetInt(KEY_PUFF_INDEX, 0);
        set => PlayerPrefs.SetInt(KEY_PUFF_INDEX, value);
    }

    public static int DeclinedPuffs
    {
        get => PlayerPrefs.GetInt(KEY_DECLINED_TOTAL, 0);
        set => PlayerPrefs.SetInt(KEY_DECLINED_TOTAL, value);
    }

    // --- Timezone offset at generation time ---
    public static int ScheduleGenOffsetMinutes
    {
        get => PlayerPrefs.GetInt(KEY_GEN_OFFSET, 0);
        set => PlayerPrefs.SetInt(KEY_GEN_OFFSET, value);
    }

    // --- Pause state ---
    public static DateTime? PauseEndTime
    {
        get
        {
            long ticks = long.Parse(PlayerPrefs.GetString(KEY_PAUSE_END_TICKS, "0"));
            if (ticks == 0) return null;
            return new DateTime(ticks);
        }
        set
        {
            if (value.HasValue)
                PlayerPrefs.SetString(KEY_PAUSE_END_TICKS, value.Value.Ticks.ToString());
            else
                PlayerPrefs.SetString(KEY_PAUSE_END_TICKS, "0");
        }
    }

    public static DateTime? PauseStartTime
    {
        get
        {
            long ticks = long.Parse(PlayerPrefs.GetString(KEY_PAUSE_START_TICKS, "0"));
            if (ticks == 0) return null;
            return new DateTime(ticks);
        }
        set
        {
            if (value.HasValue)
                PlayerPrefs.SetString(KEY_PAUSE_START_TICKS, value.Value.Ticks.ToString());
            else
                PlayerPrefs.SetString(KEY_PAUSE_START_TICKS, "0");
        }
    }

    // --- Sleep mode state ---
    public static bool SleepModeActive
    {
        get => PlayerPrefs.GetInt(KEY_SLEEP_MODE_ACTIVE, 0) == 1;
        set => PlayerPrefs.SetInt(KEY_SLEEP_MODE_ACTIVE, value ? 1 : 0);
    }

    public static DateTime? SleepModeStartedAt
    {
        get
        {
            long ticks = long.Parse(PlayerPrefs.GetString(KEY_SLEEP_MODE_START_TICKS, "0"));
            if (ticks == 0) return null;
            return new DateTime(ticks);
        }
        set
        {
            if (value.HasValue)
                PlayerPrefs.SetString(KEY_SLEEP_MODE_START_TICKS, value.Value.Ticks.ToString());
            else
                PlayerPrefs.SetString(KEY_SLEEP_MODE_START_TICKS, "0");
        }
    }

    public static DateTime? WakeNotificationFiredAt
    {
        get
        {
            long ticks = long.Parse(PlayerPrefs.GetString(KEY_WAKE_NOTIF_FIRED_TICKS, "0"));
            if (ticks == 0) return null;
            return new DateTime(ticks);
        }
        set
        {
            if (value.HasValue)
                PlayerPrefs.SetString(KEY_WAKE_NOTIF_FIRED_TICKS, value.Value.Ticks.ToString());
            else
                PlayerPrefs.SetString(KEY_WAKE_NOTIF_FIRED_TICKS, "0");
        }
    }

    // --- Extended-day / over-budget state ---
    public static int ExtendedDayBudget
    {
        get => PlayerPrefs.GetInt(KEY_EXT_BUDGET, -1);
        set => PlayerPrefs.SetInt(KEY_EXT_BUDGET, value);
    }

    public static string ExtendedDayDate
    {
        get => PlayerPrefs.GetString(KEY_EXT_DATE, "");
        set => PlayerPrefs.SetString(KEY_EXT_DATE, value);
    }

    public static int OverBudgetDecision
    {
        get => PlayerPrefs.GetInt(KEY_OB_DECISION, 0);
        set => PlayerPrefs.SetInt(KEY_OB_DECISION, value);
    }

    public static long ExtendedDaySleepTicks
    {
        get => long.Parse(PlayerPrefs.GetString(KEY_EXT_SLEEP_TICKS, "0"));
        set => PlayerPrefs.SetString(KEY_EXT_SLEEP_TICKS, value.ToString());
    }

    // --- Recent decline recovery ---
    public static long LastDeclinedAtTicks
    {
        get => long.Parse(PlayerPrefs.GetString(KEY_LAST_DECLINED_TICKS, "0"));
        set => PlayerPrefs.SetString(KEY_LAST_DECLINED_TICKS, value.ToString());
    }

    // -1 = nothing to recover
    public static int LastDeclinedIndex
    {
        get => PlayerPrefs.GetInt(KEY_LAST_DECLINED_INDEX, -1);
        set => PlayerPrefs.SetInt(KEY_LAST_DECLINED_INDEX, value);
    }

    // --- Puff log (per day) ---
    private static string LogKey(DateTime date) => LOG_KEY_PREFIX + date.ToString("yyyy-MM-dd");

    public static void LogPuffToday()
    {
        string key = LogKey(DateTime.Now);
        int current = PlayerPrefs.GetInt(key, 0);
        PlayerPrefs.SetInt(key, current + 1);
        PlayerPrefs.Save();
    }

    public static int GetPuffsForDate(DateTime date)
    {
        return PlayerPrefs.GetInt(LogKey(date), 0);
    }

    public static void Save() => PlayerPrefs.Save();

    // --- Full wipe ---
    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[UserData] Full wipe — all data cleared.");
    }

    // --- Progress reset: clears schedule state, preserves calendar + parameters ---
    public static void ResetProgress()
    {
        PlayerPrefs.DeleteKey(KEY_SCHEDULE);
        PlayerPrefs.DeleteKey(KEY_PUFF_INDEX);
        PlayerPrefs.DeleteKey(KEY_DECLINED_TOTAL);
        PlayerPrefs.DeleteKey(KEY_PAUSE_END_TICKS);
        PlayerPrefs.DeleteKey(KEY_PAUSE_START_TICKS);
        PlayerPrefs.DeleteKey(KEY_SLEEP_MODE_ACTIVE);
        PlayerPrefs.DeleteKey(KEY_SLEEP_MODE_START_TICKS);
        PlayerPrefs.DeleteKey(KEY_WAKE_NOTIF_FIRED_TICKS);
        PlayerPrefs.DeleteKey(KEY_EXT_BUDGET);
        PlayerPrefs.DeleteKey(KEY_EXT_DATE);
        PlayerPrefs.DeleteKey(KEY_OB_DECISION);
        PlayerPrefs.DeleteKey(KEY_EXT_SLEEP_TICKS);
        PlayerPrefs.DeleteKey(KEY_LAST_DECLINED_TICKS);
        PlayerPrefs.DeleteKey(KEY_LAST_DECLINED_INDEX);
        PlayerPrefs.Save();
        Debug.Log("[UserData] Progress reset — calendar and onboarding parameters preserved.");
    }

    // --- Helpers ---
    public static int AwakeMinutes
    {
        get
        {
            int wake = WakeHour * 60 + WakeMinute;
            int sleep = SleepHour * 60 + SleepMinute;
            int diff = sleep - wake;
            if (diff <= 0) diff += 24 * 60;
            return diff;
        }
    }

    public static bool IsInSleepWindow(DateTime dt)
    {
        int wakeMin = WakeHour * 60 + WakeMinute;
        int sleepMin = SleepHour * 60 + SleepMinute;
        int nowMin = dt.Hour * 60 + dt.Minute;

        if (wakeMin == sleepMin) return false;

        if (sleepMin > wakeMin)
            return nowMin >= sleepMin || nowMin < wakeMin;
        else
            return nowMin >= sleepMin && nowMin < wakeMin;
    }

    public static DateTime NextWakeAfter(DateTime dt)
    {
        int wakeMin = WakeHour * 60 + WakeMinute;
        DateTime wake = dt.Date.AddMinutes(wakeMin);
        if (wake <= dt) wake = wake.AddDays(1);
        return wake;
    }
}