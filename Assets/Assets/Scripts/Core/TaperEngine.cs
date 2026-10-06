using System;
using System.Collections.Generic;
using UnityEngine;

public static class TaperEngine
{
    public static void GenerateAndSaveSchedule()
    {
        int N0 = Mathf.Max(1, UserData.PuffsPerDay);
        int T = Mathf.Max(1, Mathf.RoundToInt(UserData.TaperDays));
        int A = UserData.AwakeMinutes;
        int wakeMin = UserData.WakeHour * 60 + UserData.WakeMinute;

        DateTime now = DateTime.Now;
        DateTime baseDate = ComputeScheduleBaseDate(now, wakeMin, A);

        // Save timezone offset at generation time (#10)
        UserData.ScheduleGenOffsetMinutes =
            (int)TimeZoneInfo.Local.GetUtcOffset(now).TotalMinutes;

        Debug.Log($"[TaperEngine] N0={N0}, T={T} days, A={A} min, " +
                  $"wake={UserData.WakeHour:D2}:{UserData.WakeMinute:D2}, " +
                  $"now={now}, baseDate={baseDate:yyyy-MM-dd}, " +
                  $"offsetMin={UserData.ScheduleGenOffsetMinutes}");

        var ticks = new List<long>();
        const int MAX_PUFFS = 200000;

        for (int day = 0; day < T; day++)
        {
            if (ticks.Count >= MAX_PUFFS) break;

            // Per-day puff allowance (tapers linearly to 0 at day T).
            float ratio = 1f - (float)day / T;
            int puffsToday = Mathf.CeilToInt(N0 * ratio);
            if (puffsToday < 1) puffsToday = 1;
            if (puffsToday > A) puffsToday = A;

            DateTime wakeTime = baseDate.AddDays(day).AddMinutes(wakeMin);
            DateTime sleepTime = wakeTime.AddMinutes(A);

            // On day 0, if we're past wake time, we only have the remainder of the day.
            DateTime effectiveStart = (day == 0 && now > wakeTime) ? now : wakeTime;

            double availMin = (sleepTime - effectiveStart).TotalMinutes;
            if (availMin <= 1) continue;

            // The user's natural rhythm for the day: A / puffsToday.
            float idealInterval = (float)A / puffsToday;

            // How many puffs fit in the remaining time at that rhythm.
            int actualPuffs = Mathf.FloorToInt((float)availMin / idealInterval);
            if (actualPuffs < 1) actualPuffs = 1;
            if (actualPuffs > puffsToday) actualPuffs = puffsToday;

            // Preserve the exact rhythm by dividing the available time evenly.
            float actualInterval = (float)availMin / actualPuffs;

            DateTime cursor = effectiveStart;
            for (int i = 0; i < actualPuffs; i++)
            {
                cursor = cursor.AddMinutes(actualInterval);
                ticks.Add(cursor.Ticks);
            }
        }

        if (ticks.Count == 0)
        {
            Debug.LogError("[TaperEngine] No puffs scheduled. Check inputs.");
            return;
        }

        UserData.SaveSchedule(ticks);
        UserData.Save();

        Debug.Log($"[TaperEngine] Scheduled {ticks.Count} puffs total. " +
                  $"First: {new DateTime(ticks[0])}, " +
                  $"Last: {new DateTime(ticks[ticks.Count - 1])}");

        for (int i = 1; i < Mathf.Min(5, ticks.Count); i++)
        {
            double gap = (new DateTime(ticks[i]) - new DateTime(ticks[i - 1])).TotalMinutes;
            Debug.Log($"[TaperEngine] Interval {i}: {gap:F2} min");
        }
    }

    /// Figure out which calendar date the *current* awake window belongs to.
    /// If the awake window crosses midnight and we're currently inside it,
    /// the base date is yesterday. Otherwise it's today.
    private static DateTime ComputeScheduleBaseDate(DateTime now, int wakeMin, int awakeMinutes)
    {
        DateTime today = now.Date;
        DateTime todayWake = today.AddMinutes(wakeMin);

        if (now >= todayWake) return today;

        DateTime yesterdayWake = todayWake.AddDays(-1);
        DateTime yesterdaySleep = yesterdayWake.AddMinutes(awakeMinutes);

        if (now < yesterdaySleep)
            return today.AddDays(-1);

        return today;
    }
}