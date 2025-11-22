namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class PomodoroAdjustment : PluginDynamicAdjustment
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        private Boolean _eventsSubscribed = false;

        private void EnsureEventsSubscribed()
        {
            if (!this._eventsSubscribed)
            {
                this.Timer.OnTick += () => this.AdjustmentValueChanged();
                this.Timer.OnStateChanged += () => this.AdjustmentValueChanged();
                this._eventsSubscribed = true;
            }
        }

        public PomodoroAdjustment()
            : base(displayName: "Reset Timer", description: "Adjust duration, press to reset", groupName: "Pomodoro", hasReset: true)
        {
            // Add parameters for different duration adjustments
            this.AddParameter("work", "Work Duration", "Pomodoro");
            this.AddParameter("shortBreak", "Short Break", "Pomodoro");
            this.AddParameter("longBreak", "Long Break", "Pomodoro");
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            this.EnsureEventsSubscribed();

            // Only allow adjustment when timer is stopped
            if (this.Timer.IsRunning)
            {
                PluginLog.Info("Cannot adjust duration while timer is running");

                // Show notification with error sound to indicate action is blocked
                NotificationService.ShowNotification(
                    "⚠️ Timer Running",
                    "Pause the timer first to adjust duration.",
                    "Basso"
                );
                return;
            }

            switch (actionParameter)
            {
                case "work":
                    this.Timer.WorkMinutes = Math.Clamp(this.Timer.WorkMinutes + diff * 5, 5, 60);
                    if (this.Timer.CurrentState == PomodoroState.Stopped || this.Timer.CurrentState == PomodoroState.Work)
                    {
                        this.Timer.Reset(); // Reset to apply new duration
                    }
                    PluginLog.Info($"Work duration: {this.Timer.WorkMinutes} min");
                    break;

                case "shortBreak":
                    this.Timer.ShortBreakMinutes = Math.Clamp(this.Timer.ShortBreakMinutes + diff * 5, 5, 30);
                    PluginLog.Info($"Short break duration: {this.Timer.ShortBreakMinutes} min");
                    break;

                case "longBreak":
                    this.Timer.LongBreakMinutes = Math.Clamp(this.Timer.LongBreakMinutes + diff * 5, 5, 60);
                    PluginLog.Info($"Long break duration: {this.Timer.LongBreakMinutes} min");
                    break;

                default:
                    // Default to work duration
                    this.Timer.WorkMinutes = Math.Clamp(this.Timer.WorkMinutes + diff * 5, 5, 60);
                    if (this.Timer.CurrentState == PomodoroState.Stopped)
                    {
                        this.Timer.Reset();
                    }
                    break;
            }

            // Play subtle sound for successful adjustment
            NotificationService.PlaySound("Tink");

            this.AdjustmentValueChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            // Reset to default values when dial is pressed
            switch (actionParameter)
            {
                case "work":
                    this.Timer.WorkMinutes = PomodoroTimer.DefaultWorkMinutes;
                    if (this.Timer.CurrentState == PomodoroState.Stopped)
                    {
                        this.Timer.Reset();
                    }
                    PluginLog.Info($"Work duration reset to {this.Timer.WorkMinutes} min");
                    break;

                case "shortBreak":
                    this.Timer.ShortBreakMinutes = PomodoroTimer.DefaultShortBreakMinutes;
                    PluginLog.Info($"Short break reset to {this.Timer.ShortBreakMinutes} min");
                    break;

                case "longBreak":
                    this.Timer.LongBreakMinutes = PomodoroTimer.DefaultLongBreakMinutes;
                    PluginLog.Info($"Long break reset to {this.Timer.LongBreakMinutes} min");
                    break;

                default:
                    this.Timer.WorkMinutes = PomodoroTimer.DefaultWorkMinutes;
                    this.Timer.ShortBreakMinutes = PomodoroTimer.DefaultShortBreakMinutes;
                    this.Timer.LongBreakMinutes = PomodoroTimer.DefaultLongBreakMinutes;
                    this.Timer.Reset();
                    PluginLog.Info("All durations reset to defaults");
                    break;
            }

            // Play sound for successful reset
            NotificationService.PlaySound("Hero");

            this.AdjustmentValueChanged();
        }

        protected override String GetAdjustmentValue(String actionParameter)
        {
            return actionParameter switch
            {
                "work" => $"{this.Timer.WorkMinutes} min",
                "shortBreak" => $"{this.Timer.ShortBreakMinutes} min",
                "longBreak" => $"{this.Timer.LongBreakMinutes} min",
                _ => $"{this.Timer.WorkMinutes} min"
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

            return $"{label}{Environment.NewLine}{this.GetAdjustmentValue(actionParameter)}";
        }
    }
}