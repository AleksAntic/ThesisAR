using System;
using UnityEngine;

/// <summary>
/// Manages post-visit questionnaire reminders and local notifications.
/// Allows visitors to either fill out the survey immediately upon finishing their tour 
/// or schedule a deferred Android notification (e.g. Tonight at 8 PM, Tomorrow morning, or in 3 Days).
/// </summary>
public class SurveyReminderManager : MonoBehaviour
{
    public static SurveyReminderManager Instance { get; private set; }

    public enum ReminderDelay
    {
        Tonight8PM,
        Tomorrow10AM,
        In3Days
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Opens the post-visit evaluation survey immediately in the web browser.
    /// </summary>
    public void OpenSurveyImmediately()
    {
        if (ThesisManager.Instance != null)
        {
            ThesisManager.Instance.OpenPostVisitSurvey();
        }
        else
        {
            Application.OpenURL("https://docs.google.com/forms");
        }
    }

    /// <summary>
    /// Schedules a deferred local notification on Android/iOS to remind the user to complete the survey.
    /// </summary>
    public void ScheduleReminder(ReminderDelay delayOption)
    {
        DateTime targetTime = CalculateTargetTime(delayOption);
        TimeSpan delayTimeSpan = targetTime - DateTime.Now;
        if (delayTimeSpan.TotalSeconds < 60) delayTimeSpan = TimeSpan.FromMinutes(5); // Minimum 5 mins safety floor

        string surveyUrl = GetSurveyUrlWithParameters();

        Debug.Log($"[SurveyReminder] Scheduling local notification for {targetTime:yyyy-MM-dd HH:mm:ss} (In {delayTimeSpan.TotalHours:F1} hours).");

#if UNITY_ANDROID && !UNITY_EDITOR
        ScheduleAndroidNotification(targetTime, surveyUrl);
#else
        Debug.Log($"[SurveyReminder Simulator] Local notification scheduled for '{targetTime:g}'. Survey URL: {surveyUrl}");
#endif

        // Save preference in PlayerPrefs
        PlayerPrefs.SetString("Thesis_SurveyReminderScheduledTime", targetTime.ToString("o"));
        PlayerPrefs.Save();

        UIManager uiMgr = UnityEngine.Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        if (uiMgr != null)
        {
            uiMgr.ShowNotificationToast("Survey Reminder", $"Reminder set! We will remind you at {targetTime:HH:mm} to share your feedback.", 4f);
        }
    }

    private DateTime CalculateTargetTime(ReminderDelay option)
    {
        DateTime now = DateTime.Now;
        switch (option)
        {
            case ReminderDelay.Tonight8PM:
                DateTime tonight = new DateTime(now.Year, now.Month, now.Day, 20, 0, 0);
                if (now.Hour >= 20) tonight = tonight.AddDays(1); // If past 8 PM, schedule for tomorrow 8 PM
                return tonight;

            case ReminderDelay.Tomorrow10AM:
                DateTime tomorrow = now.Date.AddDays(1).AddHours(10);
                return tomorrow;

            case ReminderDelay.In3Days:
                return now.AddDays(3);

            default:
                return now.AddHours(2);
        }
    }

    private string GetSurveyUrlWithParameters()
    {
        if (ThesisManager.Instance != null)
        {
            string uid = ThesisManager.Instance.AnonymousUserID;
            string mode = ThesisManager.Instance.CurrentMode.ToString();
            return $"https://docs.google.com/forms/d/e/1FAIpQLSc.../viewform?uid={uid}&mode={mode}";
        }
        return "https://docs.google.com/forms";
    }

    private void ScheduleAndroidNotification(DateTime fireTime, string url)
    {
        try
        {
            // Native Android Intent Fallback / Local Alarm Toast via AndroidJavaClass
            using (AndroidJavaClass playerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                using (AndroidJavaObject currentActivity = playerClass.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    using (AndroidJavaObject toast = new AndroidJavaClass("android.widget.Toast"))
                    {
                        using (AndroidJavaObject context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
                        {
                            string msg = $"Reminder scheduled for {fireTime:HH:mm}. Thank you for visiting Bergen-Belsen!";
                            AndroidJavaObject toastObj = toast.CallStatic<AndroidJavaObject>("makeText", context, msg, 1);
                            toastObj.Call("show");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SurveyReminder] Android native toast/notification warning: {ex.Message}");
        }
    }
}
