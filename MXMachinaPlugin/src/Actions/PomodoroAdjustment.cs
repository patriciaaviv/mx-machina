namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class PomodoroAdjustment : PluginDynamicAdjustment
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        private bool _eventsSubscribed = false;

        private void EnsureEventsSubscribed()
        {
            if (!_eventsSubscribed)
            {
                Timer.OnTick += () => this.AdjustmentValueChanged();
                Timer.OnStateChanged += () => this.AdjustmentValueChanged();
                _eventsSubscribed = true;
            }
        }

        public PomodoroAdjustment()
            : base(displayName: "Work Duration", description: "Adjust work session duration", groupName: "Pomodoro", hasReset: true)
        {
            // Add parameters for different duration adjustments
            this.AddParameter("work", "Work Duration", "Pomodoro");
            this.AddParameter("shortBreak", "Short Break", "Pomodoro");
            this.AddParameter("longBreak", "Long Break", "Pomodoro");
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            EnsureEventsSubscribed();

            // Only allow adjustment when timer is stopped
            if (Timer.IsRunning)
            {
                PluginLog.Info("Cannot adjust duration while timer is running");
                return;
            }

            switch (actionParameter)
            {
                case "work":
                    Timer.WorkMinutes = Math.Clamp(Timer.WorkMinutes + (diff * 5), 1, 60);
                    if (Timer.CurrentState == PomodoroState.Stopped || Timer.CurrentState == PomodoroState.Work)
                    {
                        Timer.Reset(); // Reset to apply new duration
                    }
                    PluginLog.Info($"Work duration: {Timer.WorkMinutes} min");
                    break;

                case "shortBreak":
                    Timer.ShortBreakMinutes = Math.Clamp(Timer.ShortBreakMinutes + (diff * 5), 1, 30);
                    PluginLog.Info($"Short break duration: {Timer.ShortBreakMinutes} min");
                    break;

                case "longBreak":
                    Timer.LongBreakMinutes = Math.Clamp(Timer.LongBreakMinutes + (diff * 5), 1, 60);
                    PluginLog.Info($"Long break duration: {Timer.LongBreakMinutes} min");
                    break;

                default:
                    // Default to work duration
                    Timer.WorkMinutes = Math.Clamp(Timer.WorkMinutes + (diff * 5), 1, 60);
                    if (Timer.CurrentState == PomodoroState.Stopped)
                    {
                        Timer.Reset();
                    }
                    break;
            }

            this.AdjustmentValueChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            // Reset to default values when dial is pressed
            switch (actionParameter)
            {
                case "work":
                    Timer.WorkMinutes = PomodoroTimer.DefaultWorkMinutes;
                    if (Timer.CurrentState == PomodoroState.Stopped)
                    {
                        Timer.Reset();
                    }
                    PluginLog.Info($"Work duration reset to {Timer.WorkMinutes} min");
                    break;

                case "shortBreak":
                    Timer.ShortBreakMinutes = PomodoroTimer.DefaultShortBreakMinutes;
                    PluginLog.Info($"Short break reset to {Timer.ShortBreakMinutes} min");
                    break;

                case "longBreak":
                    Timer.LongBreakMinutes = PomodoroTimer.DefaultLongBreakMinutes;
                    PluginLog.Info($"Long break reset to {Timer.LongBreakMinutes} min");
                    break;

                default:
                    Timer.WorkMinutes = PomodoroTimer.DefaultWorkMinutes;
                    Timer.ShortBreakMinutes = PomodoroTimer.DefaultShortBreakMinutes;
                    Timer.LongBreakMinutes = PomodoroTimer.DefaultLongBreakMinutes;
                    Timer.Reset();
                    PluginLog.Info("All durations reset to defaults");
                    break;
            }

            this.AdjustmentValueChanged();
        }

        protected override String GetAdjustmentValue(String actionParameter)
        {
            return actionParameter switch
            {
                "work" => $"{Timer.WorkMinutes} min",
                "shortBreak" => $"{Timer.ShortBreakMinutes} min",
                "longBreak" => $"{Timer.LongBreakMinutes} min",
                _ => $"{Timer.WorkMinutes} min"
            };
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var label = actionParameter switch
            {
                "work" => "Work",
                "shortBreak" => "Break",
                "longBreak" => "Long",
                _ => "Work"
            };

            return $"{label}{Environment.NewLine}{GetAdjustmentValue(actionParameter)}";
        }
    }
}
