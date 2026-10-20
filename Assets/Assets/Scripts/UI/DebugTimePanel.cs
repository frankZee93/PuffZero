#if UNITY_EDITOR
using System;
using UnityEngine;
using TMPro;

public class DebugTimePanel : MonoBehaviour
{
    public TMP_Text label;

    public void AddMinutes(int m) { TimeService.DebugOffset += TimeSpan.FromMinutes(m); Refresh(); }
    public void AddHours(int h) { TimeService.DebugOffset += TimeSpan.FromHours(h); Refresh(); }
    public void Reset() { TimeService.Reset(); Refresh(); }

    void Refresh()
    {
        if (label != null)
            label.text = $"Offset: {TimeService.DebugOffset.TotalHours:F1} h\n(Now = {TimeService.Now:HH:mm})";
    }
}
#endif