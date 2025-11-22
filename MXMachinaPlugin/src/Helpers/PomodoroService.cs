namespace Loupedeck.MXMachinaPlugin
{
    using System;

    // Singleton service to share the PomodoroTimer and related services
    internal static class PomodoroService
    {
        private static readonly Object _lock = new Object();

        public static PomodoroTimer Timer
        {
            get
            {
                lock (_lock)
                {
                    if (field == null)
                    {
                        field = new PomodoroTimer();
                        InitializeSmartFeatures();
                    }
                    return field;
                }
            }
        }

        public static GoogleCalendarService Calendar
        {
            get
            {
                lock (_lock)
                {
                    field ??= new GoogleCalendarService();
                    return field;
                }
            }
        }

        public static ActivityMonitorService ActivityMonitor
        {
            get
            {
                lock (_lock)
                {
                    field ??= new ActivityMonitorService();
                    return field;
                }
            }
        }

        private static Boolean _wasRunning = false;

        private static void InitializeSmartFeatures()
        {
            // Connect activity monitor to timer
            ActivityMonitor.OnBreakSuggested += (suggestion) =>
            {
                if (!Timer.IsRunning && Timer.CurrentState == PomodoroState.Inactive)
                {
                    PluginLog.Info($"Smart break suggestion: {suggestion.Reason}");
                    NotificationService.ShowNotification("💡 Smart Suggestion", suggestion.Reason, "Purr");
                }
            };

            // Show notifications on state changes
            Timer.OnStateChanged += () =>
            {
                if (Timer.CurrentState == PomodoroState.Work && Timer.IsRunning)
                {
                    ActivityMonitor.StartMonitoring();
                }
                else if (Timer.CurrentState == PomodoroState.Inactive)
                {
                    ActivityMonitor.StopMonitoring();
                }
            };

            // Show notification when timer starts or pauses
            Timer.OnTick += () =>
            {
                if (Timer.IsRunning && !_wasRunning)
                {
                    // Timer just started
                    var minutes = Timer.CurrentState switch
                    {
                        PomodoroState.Work => Timer.WorkMinutes,
                        PomodoroState.ShortBreak => Timer.ShortBreakMinutes,
                        PomodoroState.LongBreak => Timer.LongBreakMinutes,
                        _ => 25
                    };
                    NotificationService.NotifyTimerStarted(minutes, Timer.CurrentState);
                }
                else if (!Timer.IsRunning && _wasRunning)
                {
                    // Timer just paused
                    NotificationService.NotifyTimerPaused(Timer.GetDisplayTime());
                }
                _wasRunning = Timer.IsRunning;
            };

            // Use smart work duration suggestions and show completion notifications
            Timer.OnSessionComplete += async (state) =>
            {
                // Show completion notification
                NotificationService.NotifySessionComplete(state, Timer.CompletedPomodoros);

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