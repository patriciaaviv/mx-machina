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
        private static HapticService _hapticService;
        private static FrequencySoundService _frequencySoundService;
        private static ThoughtCaptureService _thoughtCaptureService;

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

        public static void InitializeHaptics(HapticService haptics)
        {
            _hapticService = haptics;

            // Hook up haptics to timer events
            Timer.OnPause += () =>
            {
                // Pause
                _hapticService?.TriggerTimerPause();
            };

            Timer.OnWorkBegin += () =>
            {
                // Work Begin = Resume
                _hapticService?.TriggerTimerStart();
            };

            // Hook up haptics to focus mode
            FocusMode.OnFocusModeChanged += (enabled) =>
            {
                if (enabled)
                {
                    _hapticService?.TriggerFocusModeOn();
                }
                else
                {
                    _hapticService?.TriggerFocusModeOff();
                }
            };

            PluginLog.Info("Haptic feedback initialized");
        }

        private static void InitializeSmartFeatures()
        {
            // Use smart work duration suggestions and show completion notifications
            Timer.OnWorkSessionComplete += async (phase) =>
            {
                // Record session in statistics
                var duration = phase switch
                {
                    PomodoroPhase.Work => Timer.WorkMinutes,
                    PomodoroPhase.ShortBreak => Timer.ShortBreakMinutes,
                    PomodoroPhase.LongBreak => Timer.LongBreakMinutes,
                    _ => 0
                };
                Statistics.RecordSession(phase, duration);

                // Create calendar event for completed work sessions only
                if (phase == PomodoroPhase.Work && Calendar.IsAuthenticated)
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

                // Categorize captured thoughts after work session completes
                if (phase == PomodoroPhase.Work)
                {
                    try
                    {
                        var unreviewedCount = ThoughtCapture.GetUnreviewedThoughts().Count;
                        if (unreviewedCount > 0)
                        {
                            PluginLog.Info($"Categorizing {unreviewedCount} captured thoughts...");
                            await ThoughtCapture.CategorizeThoughtsAsync();
                            
                            PomodoroService.Notification.ShowNotification(
                                "💭 Thoughts Ready",
                                $"You have {unreviewedCount} thought(s) to review",
                                "Purr"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, "Failed to categorize thoughts");
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

        public static FrequencySoundService FrequencySound
        {
            get
            {
                lock (_lock)
                {
                    _frequencySoundService ??= new FrequencySoundService();
                    return _frequencySoundService;
                }
            }
        }

        public static ThoughtCaptureService ThoughtCapture
        {
            get
            {
                lock (_lock)
                {
                    _thoughtCaptureService ??= new ThoughtCaptureService();
                    return _thoughtCaptureService;
                }
            }
        }
    }
}