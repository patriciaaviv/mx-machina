namespace Loupedeck.MXMachinaPlugin
{
    using System;

    // Singleton service to share the PomodoroTimer and related services
    internal static class PomodoroService
    {
        private static readonly Object _lock = new Object();
        private static PomodoroTimer _timer;
        private static GoogleCalendarService _calendarService;
        private static ActivityMonitorService _activityMonitor;
        private static StatisticsService _statisticsService;
        private static FocusModeService _focusModeService;

        public static PomodoroTimer Timer
        {
            get
            {
                lock (_lock)
                {
                    if (_timer == null)
                    {
                        _timer = new PomodoroTimer();
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