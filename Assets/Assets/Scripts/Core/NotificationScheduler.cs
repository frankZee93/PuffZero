using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_IOS
using Unity.Notifications.iOS;
#elif UNITY_ANDROID
using Unity.Notifications.Android;
#endif

public static class NotificationScheduler
{
    private const int IOS_PENDING_LIMIT = 60;
    private const int ANDROID_PENDING_LIMIT = 400;
    private const string ChannelId = "puff_channel";

#if UNITY_ANDROID
    private static bool androidChannelRegistered = false;
#endif

    public static void Initialize()
    {
#if UNITY_IOS
        // Authorization is handled automatically by the "Request Authorization
        // on App Launch" checkbox in Project Settings -> Mobile Notifications.
        // No runtime code needed here.
        Debug.Log("[Notif] iOS initialization — authorization handled by Project Settings.");
#elif UNITY_ANDROID
        RegisterAndroidChannel();
#endif
    }

#if UNITY_ANDROID
    private static void RegisterAndroidChannel()
    {
        if (androidChannelRegistered) return;
        var channel = new AndroidNotificationChannel()
        {
            Id = ChannelId,
            Name = "Puff Reminders",
            Importance = Importance.High,
            Description = "Reminders for your scheduled puffs and wake-ups.",
        };
        AndroidNotificationCenter.RegisterNotificationChannel(channel);
        androidChannelRegistered = true;
    }
#endif

    public static void CancelAll()
    {
#if UNITY_IOS
        iOSNotificationCenter.RemoveAllScheduledNotifications();
#elif UNITY_ANDROID
        AndroidNotificationCenter.CancelAllNotifications();
#endif
    }

    public static void SchedulePuffs(
        List<long> schedule,
        int startIndex,
        DateTime? pauseEndTime,
        int dayPuffNumber,
        int dayPuffTotal)
    {
        CancelAll();

        if (schedule == null || schedule.Count == 0) return;
        if (startIndex < 0 || startIndex >= schedule.Count) return;

        DateTime now = DateTime.Now;

#if UNITY_IOS
        int limit = IOS_PENDING_LIMIT;
#elif UNITY_ANDROID
        int limit = ANDROID_PENDING_LIMIT;
#else
        int limit = 0;
#endif

        int scheduled = 0;
        for (int i = startIndex; i < schedule.Count && scheduled < limit; i++)
        {
            DateTime t = new DateTime(schedule[i]);
            if (t <= now.AddSeconds(30)) continue;
            if (pauseEndTime.HasValue && t <= pauseEndTime.Value) continue;

            string body = $"Puff {dayPuffNumber + scheduled} of {dayPuffTotal} today. Tap to log it.";
            ScheduleOne(t, $"puff_{i}", "Time to Smoke", body);
            scheduled++;
        }

#if UNITY_IOS || UNITY_ANDROID
        Debug.Log($"[Notif] Scheduled {scheduled} puff notifications (limit {limit}, starting index {startIndex})");
#endif
    }

    public static void ScheduleSleepEscalation(DateTime wakeTime)
    {
        CancelAll();

        DateTime now = DateTime.Now;

        var reminders = new List<(DateTime when, string title, string body)>
        {
            (wakeTime,                                  "Good morning",                  "Tap to start your day."),
            (wakeTime.AddHours(3),                      "Still time to start",           "Tap 'I'm up now' in the app."),
            (wakeTime.AddHours(6),                      "You haven't started today yet", "Tap 'I'm up now' in the app."),
            (wakeTime.AddHours(9),                      "Taper paused",                  "Open the app to continue your progress."),
        };

        for (int h = 11; h <= 72; h += 2)
            reminders.Add((wakeTime.AddHours(h), "Taper paused", "Open the app to continue your progress."));

#if UNITY_IOS
        int limit = IOS_PENDING_LIMIT;
#elif UNITY_ANDROID
        int limit = ANDROID_PENDING_LIMIT;
#else
        int limit = 0;
#endif

        int scheduled = 0;
        foreach (var r in reminders)
        {
            if (scheduled >= limit) break;
            if (r.when <= now.AddSeconds(30)) continue;

            ScheduleOne(r.when, $"sleep_{scheduled}", r.title, r.body);
            scheduled++;
        }

#if UNITY_IOS || UNITY_ANDROID
        Debug.Log($"[Notif] Scheduled {scheduled} sleep escalation notifications (wake at {wakeTime:HH:mm}).");
#endif
    }

    private static void ScheduleOne(DateTime fireTime, string identifier, string title, string body)
    {
#if UNITY_IOS
        ScheduleOneIOS(fireTime, identifier, title, body);
#elif UNITY_ANDROID
        ScheduleOneAndroid(fireTime, title, body);
#endif
    }

#if UNITY_IOS
    private static void ScheduleOneIOS(DateTime fireTime, string identifier, string title, string body)
    {
        var trigger = new iOSNotificationTimeIntervalTrigger()
        {
            TimeInterval = fireTime - DateTime.Now,
            Repeats = false,
        };

        var notification = new iOSNotification()
        {
            Identifier = identifier,
            Title = title,
            Body = body,
            ShowInForeground = true,
            ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
            Trigger = trigger,
        };

        iOSNotificationCenter.ScheduleNotification(notification);
    }
#endif

#if UNITY_ANDROID
    private static void ScheduleOneAndroid(DateTime fireTime, string title, string body)
    {
        var notification = new AndroidNotification();
        notification.Title = title;
        notification.Text = body;
        notification.FireTime = fireTime;

        AndroidNotificationCenter.SendNotification(notification, ChannelId);
    }
#endif
}