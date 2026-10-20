using System;

public static class TimeService
{
#if UNITY_EDITOR
    public static TimeSpan DebugOffset = TimeSpan.Zero;
    public static void Reset() { DebugOffset = TimeSpan.Zero; }
    public static DateTime Now => DateTime.Now + DebugOffset;
    public static DateTime UtcNow => DateTime.UtcNow + DebugOffset;
#else
    public static DateTime Now => DateTime.Now;
    public static DateTime UtcNow => DateTime.UtcNow;
#endif
}