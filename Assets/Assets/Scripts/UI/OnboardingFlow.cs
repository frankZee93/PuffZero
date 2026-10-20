using UnityEngine;
using TMPro;

public class OnboardingFlow : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public GameObject root;
        public TMP_InputField input;
        public ScrollPicker picker;
    }

    [Header("Pages in order: Wake, Sleep, Puffs, Taper")]
    public Page[] pages;

    [Header("Validation UI")]
    public TMP_Text validationText;

    private const int MIN_AWAKE_MINUTES = 240;     // 4 hours
    private const int MIN_TAPER_DAYS = 2;
    private const int MAX_TAPER_DAYS = 365;
    private const int MAX_TOTAL_PUFFS = 500000;

    private const int MINUTE_STEP = 5;
    private const int MINUTE_STEPS = 12;           // 0, 5, 10, ..., 55

    private int current = 0;

    private void Start()
    {
        ShowPage(0);
        ClearValidation();
        if (pages.Length > 0 && pages[0].picker != null)
            SetupPickerPage(0);
    }

    public void Next()
    {
        if (current >= pages.Length) return;

        ClearValidation();
        Page p = pages[current];

        if (p.picker != null)
        {
            p.picker.GetSelection(out int hourIdx, out int minuteIdx);
            int hour = hourIdx;
            int minute = minuteIdx * MINUTE_STEP;

            if (!ValidatePickerPage(current, hour, minute))
                return;

            StorePickerPage(current, hour, minute);
        }
        else if (p.input != null)
        {
            string raw = p.input.text.Trim();
            if (!int.TryParse(raw, out int v))
            {
                ShowError("Please enter a whole number.");
                return;
            }

            if (!ValidateInputPage(current, v))
                return;

            StoreInputPage(current, v);
        }

        current++;

        if (current >= pages.Length)
        {
            Finish();
        }
        else
        {
            ShowPage(current);
            if (pages[current].picker != null)
                SetupPickerPage(current);
        }
    }

    public void Back()
    {
        if (current <= 0) return;
        ClearValidation();
        current--;
        ShowPage(current);
        if (pages[current].picker != null)
            SetupPickerPage(current);
    }

    private void ShowPage(int i)
    {
        for (int p = 0; p < pages.Length; p++)
            if (pages[p]?.root != null) pages[p].root.SetActive(p == i);
    }

    private void SetupPickerPage(int index)
    {
        var picker = pages[index].picker;
        if (picker == null) return;

        string[] hours = new string[24];
        for (int h = 0; h < 24; h++)
            hours[h] = $"{h:D2}";

        string[] mins = new string[MINUTE_STEPS];
        for (int i = 0; i < MINUTE_STEPS; i++)
            mins[i] = $"{(i * MINUTE_STEP):D2}";

        int hInit = index == 0 ? UserData.WakeHour : UserData.SleepHour;
        int mInit = (index == 0 ? UserData.WakeMinute : UserData.SleepMinute) / MINUTE_STEP;
        if (mInit < 0) mInit = 0;
        if (mInit >= MINUTE_STEPS) mInit = MINUTE_STEPS - 1;

        picker.Show(
            index == 0 ? "When do you wake up?" : "When do you go to sleep?",
            hours, mins, hInit, mInit,
            null, null,
            hideButtons: true);
    }

    private bool ValidatePickerPage(int index, int hour, int minute)
    {
        int savedWakeH = UserData.WakeHour;
        int savedWakeM = UserData.WakeMinute;
        int savedSleepH = UserData.SleepHour;
        int savedSleepM = UserData.SleepMinute;

        if (index == 0) { UserData.WakeHour = hour; UserData.WakeMinute = minute; }
        else if (index == 1) { UserData.SleepHour = hour; UserData.SleepMinute = minute; }

        int awake = UserData.AwakeMinutes;

        UserData.WakeHour = savedWakeH;
        UserData.WakeMinute = savedWakeM;
        UserData.SleepHour = savedSleepH;
        UserData.SleepMinute = savedSleepM;

        if (awake == 0)
        {
            ShowError("Wake time and sleep time cannot be identical.");
            return false;
        }
        if (awake < MIN_AWAKE_MINUTES)
        {
            ShowError($"Your awake window is only {awake} minutes. Minimum is {MIN_AWAKE_MINUTES} (4 hours).");
            return false;
        }
        return true;
    }

    private bool ValidateInputPage(int index, int v)
    {
        if (index == 2)
        {
            if (v < 1) { ShowError("You must enter at least 1 puff per day."); return false; }
            int awake = UserData.AwakeMinutes;
            if (v > awake)
            {
                ShowError($"Too many puffs for a {awake}-minute awake window. Maximum is {awake}.");
                return false;
            }
        }
        else if (index == 3)
        {
            if (v < MIN_TAPER_DAYS) { ShowError($"Taper must be at least {MIN_TAPER_DAYS} days."); return false; }
            if (v > MAX_TAPER_DAYS) { ShowError($"Taper cannot exceed {MAX_TAPER_DAYS} days."); return false; }
            long total = (long)UserData.PuffsPerDay * v;
            if (total > MAX_TOTAL_PUFFS)
            {
                ShowError($"Too many total puffs ({total:N0}). Reduce puffs per day or taper days.");
                return false;
            }
        }
        return true;
    }

    private void StorePickerPage(int index, int hour, int minute)
    {
        if (index == 0) { UserData.WakeHour = hour; UserData.WakeMinute = minute; }
        else if (index == 1) { UserData.SleepHour = hour; UserData.SleepMinute = minute; }
    }

    private void StoreInputPage(int index, int value)
    {
        if (index == 2) UserData.PuffsPerDay = value;
        else if (index == 3) UserData.TaperDays = value;
    }

    private void ShowError(string message)
    {
        if (validationText != null)
        {
            validationText.text = message;
            validationText.gameObject.SetActive(true);
        }
        Debug.LogWarning($"[Onboarding] {message}");
    }

    private void ClearValidation()
    {
        if (validationText != null)
        {
            validationText.text = "";
            validationText.gameObject.SetActive(false);
        }
    }

    private void Finish()
    {
        UserData.Onboarded = true;
        UserData.Save();

        TaperEngine.GenerateAndSaveSchedule();

        Debug.Log("Onboarding complete. Schedule generated.");

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainApp");
    }
}