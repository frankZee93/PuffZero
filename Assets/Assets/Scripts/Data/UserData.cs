using UnityEngine;
using System;
using System.Collections.Generic;

public enum PuffStatus { Smoked = 0, Skipped = 1, Erased = 2 }

public struct PuffRecord
{
    public long ScheduledTicks;
    public PuffStatus Status;
    public long ResolvedTicks;
}

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
    private const string KEY_WOKE_EARLY_TODAY = "pz_woke_early_today";
    private const string KEY_IDEAL_PANEL_SHOWN_TODAY = "pz_ideal_panel_shown_today";
    private const string KEY_PRE_END_DAY_INDEX = "pz_pre_end_day_index";
    private const string KEY_PRE_END_DAY_TICKS = "pz_pre_end_day_ticks";
    private const string LOG_KEY_PREFIX = "pz_log_";
    private const string PUFF_RECORDS_PREFIX = "pz_puffs_";

    private static List<long> _scheduleCache = null;
    private static readonly Dictionary<DateTime, List<PuffRecord>> _recordsCache = new Dictionary<DateTime, List<PuffRecord>>();

    public static int WakeHour { get => PlayerPrefs.GetInt(KEY_WAKE_HOUR, 7); set => PlayerPrefs.SetInt(KEY_WAKE_HOUR, value); }
    public static int WakeMinute { get => PlayerPrefs.GetInt(KEY_WAKE_MIN, 0); set => PlayerPrefs.SetInt(KEY_WAKE_MIN, value); }
    public static int SleepHour { get => PlayerPrefs.GetInt(KEY_SLEEP_HOUR, 23); set => PlayerPrefs.SetInt(KEY_SLEEP_HOUR, value); }
    public static int SleepMinute { get => PlayerPrefs.GetInt(KEY_SLEEP_MIN, 0); set => PlayerPrefs.SetInt(KEY_SLEEP_MIN, value); }

    public static int PuffsPerDay
    {
        get => PlayerPrefs.GetInt(KEY_PUFFS_PER_DAY, 20);
        set => PlayerPrefs.SetInt(KEY_PUFFS_PER_DAY, value);
    }

    public static float TaperDays
    {
        get => PlayerPrefs.GetFloat(KEY_TAPER_DAYS, 90f);
        set => PlayerPrefs.SetFloat(KEY_TAPER_DAYS, value);
    }

    public static bool Onboarded
    {
        get => PlayerPrefs.GetInt(KEY_ONBOARDED, 0) == 1;
        set => PlayerPrefs.SetInt(KEY_ONBOARDED, value ? 1 : 0);
    }

    public static void SaveSchedule(List<long> ticks)
    {
        PlayerPrefs.SetString(KEY_SCHEDULE, string.Join(",", ticks));
        _scheduleCache = ticks;
        CurrentPuffIndex = 0;
        DeclinedPuffs = 0;
    }

    public static void SaveSchedulePreserveProgress(List<long> ticks)
    {
        PlayerPrefs.SetString(KEY_SCHEDULE, string.Join(",", ticks));
        _scheduleCache = ticks;
    }

    public static List<long> LoadSchedule()
    {
        if (_scheduleCache != null) return _scheduleCache;

        var result = new List<long>();
        string raw = PlayerPrefs.GetString(KEY_SCHEDULE, "");
        if (string.IsNullOrEmpty(raw))
        {
            _scheduleCache = result;
            return result;
        }
        foreach (var s in raw.Split(','))
            if (long.TryParse(s, out long t)) result.Add(t);

        _scheduleCache = result;
        return result;
    }

    public static void InvalidateScheduleCache()
    {
        _scheduleCache = null;
    }

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

    public static int ScheduleGenOffsetMinutes
    {
        get => PlayerPrefs.GetInt(KEY_GEN_OFFSET, 0);
        set => PlayerPrefs.SetInt(KEY_GEN_OFFSET, value);
    }

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

    public static long LastDeclinedAtTicks
    {
        get => long.Parse(PlayerPrefs.GetString(KEY_LAST_DECLINED_TICKS, "0"));
        set => PlayerPrefs.SetString(KEY_LAST_DECLINED_TICKS, value.ToString());
    }

    public static int LastDeclinedIndex
    {
        get => PlayerPrefs.GetInt(KEY_LAST_DECLINED_INDEX, -1);
        set => PlayerPrefs.SetInt(KEY_LAST_DECLINED_INDEX, value);
    }

    public static bool WokeEarlyToday
    {
        get => PlayerPrefs.GetInt(KEY_WOKE_EARLY_TODAY, 0) == 1;
        set => PlayerPrefs.SetInt(KEY_WOKE_EARLY_TODAY, value ? 1 : 0);
    }

    public static bool IdealPanelShownToday
    {
        get => PlayerPrefs.GetInt(KEY_IDEAL_PANEL_SHOWN_TODAY, 0) == 1;
        set => PlayerPrefs.SetInt(KEY_IDEAL_PANEL_SHOWN_TODAY, value ? 1 : 0);
    }

    public static int PreEndDayIndex
    {
        get => PlayerPrefs.GetInt(KEY_PRE_END_DAY_INDEX, -1);
        set => PlayerPrefs.SetInt(KEY_PRE_END_DAY_INDEX, value);
    }

    public static long PreEndDayTicks
    {
        get => long.Parse(PlayerPrefs.GetString(KEY_PRE_END_DAY_TICKS, "0"));
        set => PlayerPrefs.SetString(KEY_PRE_END_DAY_TICKS, value.ToString());
    }

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

    public static void SetPuffsForDate(DateTime date, int count)
    {
        if (count < 0) count = 0;
        string key = LogKey(date);
        PlayerPrefs.SetInt(key, count);
        PlayerPrefs.Save();
    }

    private static string PuffRecordsKey(DateTime date) => PUFF_RECORDS_PREFIX + date.ToString("yyyy-MM-dd");

    public static void RecordPuffResolution(long scheduledTicks, PuffStatus status, long resolvedTicks = 0)
    {
        if (resolvedTicks == 0) resolvedTicks = DateTime.Now.Ticks;

        DateTime date = new DateTime(scheduledTicks).Date;
        string key = PuffRecordsKey(date);
        string raw = PlayerPrefs.GetString(key, "");

        var entries = new List<PuffRecord>();
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var part in raw.Split('|'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                var fields = part.Split(':');
                if (fields.Length != 3) continue;
                if (!long.TryParse(fields[0], out long st)) continue;
                if (!int.TryParse(fields[1], out int stt)) continue;
                if (!long.TryParse(fields[2], out long rt)) continue;
                entries.Add(new PuffRecord { ScheduledTicks = st, Status = (PuffStatus)stt, ResolvedTicks = rt });
            }
        }

        bool replaced = false;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].ScheduledTicks == scheduledTicks)
            {
                entries[i] = new PuffRecord { ScheduledTicks = scheduledTicks, Status = status, ResolvedTicks = resolvedTicks };
                replaced = true;
                break;
            }
        }
        if (!replaced)
            entries.Add(new PuffRecord { ScheduledTicks = scheduledTicks, Status = status, ResolvedTicks = resolvedTicks });

        var parts = new List<string>(entries.Count);
        foreach (var e in entries)
            parts.Add($"{e.ScheduledTicks}:{(int)e.Status}:{e.ResolvedTicks}");

        PlayerPrefs.SetString(key, string.Join("|", parts));
        PlayerPrefs.Save();

        _recordsCache[date] = entries;
    }

    public static List<PuffRecord> GetPuffRecordsForDate(DateTime date)
    {
        date = date.Date;
        if (_recordsCache.TryGetValue(date, out var cached)) return cached;

        var result = new List<PuffRecord>();
        string raw = PlayerPrefs.GetString(PuffRecordsKey(date), "");
        if (!string.IsNullOrEmpty(raw))
        {
            foreach (var part in raw.Split('|'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                var fields = part.Split(':');
                if (fields.Length != 3) continue;
                if (!long.TryParse(fields[0], out long st)) continue;
                if (!int.TryParse(fields[1], out int stt)) continue;
                if (!long.TryParse(fields[2], out long rt)) continue;
                result.Add(new PuffRecord { ScheduledTicks = st, Status = (PuffStatus)stt, ResolvedTicks = rt });
            }
        }

        _recordsCache[date] = result;
        return result;
    }

    public static PuffRecord? GetPuffRecord(long scheduledTicks)
    {
        var all = GetPuffRecordsForDate(new DateTime(scheduledTicks).Date);
        foreach (var r in all)
            if (r.ScheduledTicks == scheduledTicks) return r;
        return null;
    }

    public static void DeletePuffRecord(long scheduledTicks)
    {
        DateTime date = new DateTime(scheduledTicks).Date;
        string key = PuffRecordsKey(date);
        string raw = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(raw)) return;

        var parts = new List<string>();
        foreach (var part in raw.Split('|'))
        {
            if (string.IsNullOrEmpty(part)) continue;
            var fields = part.Split(':');
            if (fields.Length != 3) continue;
            if (!long.TryParse(fields[0], out long st)) continue;
            if (st == scheduledTicks) continue;
            parts.Add(part);
        }
        PlayerPrefs.SetString(key, string.Join("|", parts));
        PlayerPrefs.Save();

        _recordsCache.Remove(date);
    }

    // Change the status of an existing puff record and adjust the counters
    // that back the calendar / ideal panel. No-op if the record doesn't
    // exist or the new status matches the current one.
    public static void ChangePuffStatus(long scheduledTicks, PuffStatus newStatus)
    {
        var existing = GetPuffRecord(scheduledTicks);
        if (!existing.HasValue) return;
        if (existing.Value.Status == newStatus) return;

        var oldStatus = existing.Value.Status;
        DateTime date = new DateTime(scheduledTicks).Date;

        if (oldStatus == PuffStatus.Smoked)
            SetPuffsForDate(date, GetPuffsForDate(date) - 1);
        else if (oldStatus == PuffStatus.Skipped)
            DeclinedPuffs = Mathf.Max(0, DeclinedPuffs - 1);

        if (newStatus == PuffStatus.Smoked)
            SetPuffsForDate(date, GetPuffsForDate(date) + 1);
        else if (newStatus == PuffStatus.Skipped)
            DeclinedPuffs = DeclinedPuffs + 1;

        RecordPuffResolution(scheduledTicks, newStatus);
    }

    public static void Save() => PlayerPrefs.Save();

    public static void ResetAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        _scheduleCache = null;
        _recordsCache.Clear();
        Debug.Log("[UserData] Full wipe - all data cleared.");
    }

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
        PlayerPrefs.DeleteKey(KEY_WOKE_EARLY_TODAY);
        PlayerPrefs.DeleteKey(KEY_IDEAL_PANEL_SHOWN_TODAY);
        PlayerPrefs.DeleteKey(KEY_PRE_END_DAY_INDEX);
        PlayerPrefs.DeleteKey(KEY_PRE_END_DAY_TICKS);
        PlayerPrefs.Save();
        _scheduleCache = null;
        Debug.Log("[UserData] Progress reset - calendar and onboarding parameters preserved.");
    }

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