namespace Loupedeck.MXMachinaPlugin
{
    using System;

    // Singleton service to share the PomodoroTimer and related services
    internal static class PomodoroService
    {
        private static readonly Object _lock = new Object();
        private static PomodoroTimer _timer;
        private static GoogleCalendarService _calendarService;
        private static StatisticsService _statisticsService;
        private static FocusModeService _focusModeService;
        private static NotificationService _notificationService;

        public static PomodoroTimer Timer
        {
            get
            {
                lock (_lock)
                {
                    if (_timer == null)
                    {
                        _timer = new PomodoroTimer();
                        // InitializeSmartFeatures();
                    }
                    return _timer;
                }
            }
        }
        private static Boolean _wasRunning = false;

        private static void InitializeSmartFeatures()
        {
            // Use smart work duration suggestions and show completion notifications
            Timer.OnSessionComplete += async (state) =>
            {
                // Show completion notification
                PomodoroService.Notification.NotifySessionComplete(state, Timer.CompletedPomodoros);

                // Record session in statistics
                var duration = state switch
                {
                    PomodoroState.Work => Timer.WorkMinutes,
                    PomodoroState.ShortBreak => Timer.ShortBreakMinutes,
                    PomodoroState.LongBreak => Timer.LongBreakMinutes,
                    _ => 0
                };
                Statistics.RecordSession(state, duration);

                // Create calendar event for completed work sessions only
                if (state == PomodoroState.Work && Calendar.IsAuthenticated)
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
            };

            PluginLog.Info("Smart Pomodoro features initialized");
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

        public static StatisticsService Statistics
        {
            get
            {
                lock (_lock)
                {
                    _statisticsService ??= new StatisticsService();
                    return _statisticsService;
                }
            }
        }

        public static NotificationService Notification
        {
            get
            {
                lock (_lock)
                {
                    _notificationService ??= new NotificationService();
                    return _notificationService;
                }
            }
        }

        public static FocusModeService FocusMode
        {
            get
            {
                lock (_lock)
                {
                    _focusModeService ??= new FocusModeService();
                    return _focusModeService;
                }
            }
        }
    }
}