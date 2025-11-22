namespace Loupedeck.MXMachinaPlugin
{
    using System;

    // Singleton service to share the PomodoroTimer and related services
    internal static class PomodoroService
    {
        private static PomodoroTimer _timer;
        private static GoogleCalendarService _calendarService;
        private static ActivityMonitorService _activityMonitor;
        private static readonly Object _lock = new Object();

        public static PomodoroTimer Timer
        {
            get
            {
                lock (_lock)
                {
                    if (_timer == null)
                    {
                        _timer = new PomodoroTimer();
                        InitializeSmartFeatures();
                    }
                    return _timer;
                }
            }
        }

        public static GoogleCalendarService Calendar
        {
            get
            {
                lock (_lock)
                {
                    _calendarService ??= new GoogleCalendarService();
                    return _calendarService;
                }
            }
        }

        public static ActivityMonitorService ActivityMonitor
        {
            get
            {
                lock (_lock)
                {
                    _activityMonitor ??= new ActivityMonitorService();
                    return _activityMonitor;
                }
            }
        }

        private static void InitializeSmartFeatures()
        {
            // Connect activity monitor to timer
            ActivityMonitor.OnBreakSuggested += (suggestion) =>
            {
                if (!Timer.IsRunning && Timer.CurrentState == PomodoroState.Stopped)
                {
                    PluginLog.Info($"Smart break suggestion: {suggestion.Reason}");
                }
            };

            // Start activity monitoring when timer starts
            Timer.OnStateChanged += () =>
            {
                if (Timer.CurrentState == PomodoroState.Work && Timer.IsRunning)
                {
                    ActivityMonitor.StartMonitoring();
                }
                else if (Timer.CurrentState == PomodoroState.Stopped)
                {
                    ActivityMonitor.StopMonitoring();
                }
            };

            // Use smart work duration suggestions
            Timer.OnSessionComplete += async (state) =>
            {
                if (state == PomodoroState.Work)
                {
                    // Optionally create a calendar event for completed focus block
                    if (Calendar.IsAuthenticated)
                    {
                        try
                        {
                            var focusEnd = DateTime.Now;
                            var focusStart = focusEnd.AddMinutes(-Timer.WorkMinutes);
                            await Calendar.CreateFocusBlockAsync(focusStart, Timer.WorkMinutes,
                                $"Focus Session #{Timer.CompletedPomodoros}");
                            PluginLog.Info("Created calendar event for completed focus session");
                        }
                        catch (Exception ex)
                        {
                            PluginLog.Error(ex, "Failed to create calendar event");
                        }
                    }
                }
            };

            PluginLog.Info("Smart Pomodoro features initialized");
        }

        public static void UpdateWorkDurationFromActivity()
        {
            var suggestedDuration = ActivityMonitor.GetOptimalWorkDuration();
            if (suggestedDuration != Timer.WorkMinutes)
            {
                PluginLog.Info($"Suggesting work duration: {suggestedDuration} min based on activity patterns");
            }
        }
    }
}
