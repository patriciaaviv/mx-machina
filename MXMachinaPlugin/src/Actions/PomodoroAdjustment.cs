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
            : base(displayName: "Timer Settings", description: "Adjust duration, press to reset", groupName: "Pomodoro", hasReset: true)
        {
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

            switch (this.Timer.CurrentState)
            {
                case PomodoroState.Inactive:
                    this.Timer.WorkMinutes = Math.Clamp(this.Timer.WorkMinutes + diff * 5, 5, 60);
                    if (this.Timer.CurrentState == PomodoroState.Inactive || this.Timer.CurrentState == PomodoroState.Work)
                    {
                        this.Timer.Reset(); // Reset to apply new duration
                    }
                    PluginLog.Info($"Work duration: {this.Timer.WorkMinutes} min");
                    break;

                case PomodoroState.ShortBreak:
                    this.Timer.ShortBreakMinutes = Math.Clamp(this.Timer.ShortBreakMinutes + diff * 5, 5, 30);
                    PluginLog.Info($"Short break duration: {this.Timer.ShortBreakMinutes} min");
                    break;

                case PomodoroState.LongBreak:
                    this.Timer.LongBreakMinutes = Math.Clamp(this.Timer.LongBreakMinutes + diff * 5, 5, 60);
                    PluginLog.Info($"Long break duration: {this.Timer.LongBreakMinutes} min");
                    break;

                default:
                    break;
            }

            // Play subtle sound for successful adjustment
            NotificationService.PlaySound("Tink");

            this.AdjustmentValueChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            PluginLog.Info("HS");
            // Reset to default values when dial is pressed
            switch (actionParameter)
            {
                case "Work":
                    this.Timer.WorkMinutes = PomodoroTimer.DefaultWorkMinutes;
                    if (this.Timer.CurrentState == PomodoroState.Inactive)
                    {
                        this.Timer.Reset();
                    }
                    PluginLog.Info($"Work duration reset to {this.Timer.WorkMinutes} min");
                    break;

                case "ShortBreak":
                    this.Timer.ShortBreakMinutes = PomodoroTimer.DefaultShortBreakMinutes;
                    PluginLog.Info($"Short break reset to {this.Timer.ShortBreakMinutes} min");
                    break;

                case "LongBreak":
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
                "Work" => $"{this.Timer.WorkMinutes} min",
                "ShortBreak" => $"{this.Timer.ShortBreakMinutes} min",
                "LongBreak" => $"{this.Timer.LongBreakMinutes} min",
                _ => $"{this.Timer.WorkMinutes} min"
            };
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var label = actionParameter switch
            {
                "Work" => "Work",
                "ShortBreak" => "Break",
                "LongBreak" => "Long",
                _ => "Work"
            };

            return $"{label}{Environment.NewLine}{this.GetAdjustmentValue(actionParameter)}";
        }
    }
}