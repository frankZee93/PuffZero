using UnityEngine;
using TMPro;

public class OnboardingFlow : MonoBehaviour
{
    [System.Serializable]
    public class Page
    {
        public GameObject root;
        public TMP_InputField input;
    }

    [Header("Pages in order")]
    public Page[] pages;

    [Header("Validation UI")]
    public TMP_Text validationText;

    // Validation constants
    private const int MIN_AWAKE_MINUTES = 240;     // 4 hours
    private const int MIN_TAPER_DAYS = 2;
    private const int MAX_TAPER_DAYS = 365;
    private const int MAX_TOTAL_PUFFS = 500000;

    private int current = 0;

    private void Start()
    {
        ShowPage(0);
        ClearValidation();
    }

    public void Next()
    {
        if (current >= pages.Length) return;

        ClearValidation();
        Page p = pages[current];

        if (p.input != null)
        {
            string raw = p.input.text.Trim();
            if (!int.TryParse(raw, out int v))
            {
                ShowError("Please enter a whole number.");
                return;
            }

            if (!ValidatePage(current, v))
                return;

            StoreForPage(current, v);
        }

        current++;

        if (current >= pages.Length) Finish();
        else ShowPage(current);
    }

    public void Back()
    {
        if (current <= 0) return;
        ClearValidation();
        current--;
        ShowPage(current);
    }

    private bool ValidatePage(int index, int v)
    {
        switch (index)
        {
            case 0: // Wake hour
                if (v < 0 || v > 23) { ShowError("Hour must be 0-23."); return false; }
                break;
            case 1: // Wake minute
                if (v < 0 || v > 59) { ShowError("Minute must be 0-59."); return false; }
                // After wake minute, we have both wake H and M - but sleep not yet set.
                break;
            case 2: // Sleep hour
                if (v < 0 || v > 23) { ShowError("Hour must be 0-23."); return false; }
                break;
            case 3: // Sleep minute - validate the whole awake window now
                if (v < 0 || v > 59) { ShowError("Minute must be 0-59."); return false; }
                if (!ValidateAwakeWindow()) return false;
                break;
            case 4: // Puffs per day
                if (v < 1) { ShowError("You must enter at least 1 puff per day."); return false; }
                int awake = UserData.AwakeMinutes;
                if (v > awake)
                {
                    ShowError($"Too many puffs for a {awake}-minute awake window. Maximum is {awake}.");
                    return false;
                }
                break;
            case 5: // Taper days
                if (v < MIN_TAPER_DAYS) { ShowError($"Taper must be at least {MIN_TAPER_DAYS} days."); return false; }
                if (v > MAX_TAPER_DAYS) { ShowError($"Taper cannot exceed {MAX_TAPER_DAYS} days."); return false; }
                long total = (long)UserData.PuffsPerDay * v;
                if (total > MAX_TOTAL_PUFFS)
                {
                    ShowError($"Too many total puffs ({total:N0}). Reduce puffs per day or taper days.");
                    return false;
                }
                break;
        }
        return true;
    }

    private bool ValidateAwakeWindow()
    {
        // Sleep/wake haven't been stored yet since this is running during ValidatePage before StoreForPage.
        // So we temporarily set them, check the window, and restore.
        int savedSleepH = UserData.SleepHour;
        int savedSleepM = UserData.SleepMinute;
        UserData.SleepHour = Mathf.RoundToInt(GetInputValue(2));
        UserData.SleepMinute = Mathf.RoundToInt(GetInputValue(3));

        int awake = UserData.AwakeMinutes;

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

    private float GetInputValue(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= pages.Length) return 0f;
        var input = pages[pageIndex].input;
        if (input == null) return 0f;
        if (int.TryParse(input.text.Trim(), out int v)) return v;
        return 0f;
    }

    private void StoreForPage(int index, int value)
    {
        // 0=Wake hour, 1=Wake minute, 2=Sleep hour, 3=Sleep minute, 4=Puffs per day, 5=Taper days
        switch (index)
        {
            case 0: UserData.WakeHour = value; break;
            case 1: UserData.WakeMinute = value; break;
            case 2: UserData.SleepHour = value; break;
            case 3: UserData.SleepMinute = value; break;
            case 4: UserData.PuffsPerDay = value; break;
            case 5: UserData.TaperDays = value; break;
        }
    }

    private void ShowPage(int i)
    {
        for (int p = 0; p < pages.Length; p++)
            if (pages[p]?.root != null) pages[p].root.SetActive(p == i);
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