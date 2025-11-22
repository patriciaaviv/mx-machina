namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class PomodoroAdjustment : PluginDynamicAdjustment
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        public PomodoroAdjustment() : base(hasReset: false)
        {
            this.DisplayName = "Timer Settings";
            this.Description = "Adjustments for Timer Durations";
            String[] timeParams = new String[] { "Work", "Short Break", "Long Break" };
            foreach (String timeParam in timeParams)
            {
                this.AddParameter(timeParam, $"Adjust {timeParam} Time", "Pomodoro###Time Settings");
            }
        }

        private Int32 GetTimeFromActionParameter(String actionParameter) =>
            actionParameter switch
            {
                "Work" => this.Timer.WorkMinutes,
                "Short Break" => this.Timer.ShortBreakMinutes,
                "Long Break" => this.Timer.LongBreakMinutes,
                _ => throw new ApplicationException(),
            };


        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            switch (actionParameter)
            {
                case "Work":
                    this.Timer.WorkMinutes += diff * 5;
                    break;
                case "Short Break":
                    this.Timer.ShortBreakMinutes += diff * 5;
                    break;
                case "Long Break":
                    this.Timer.LongBreakMinutes += diff * 5;
                    break;
            }
            PluginLog.Info($"Changed {actionParameter} Duration: {GetTimeFromActionParameter(actionParameter)} min");

            // Play subtle sound for successful adjustment
            // TODO: Remove sound/replace with haptics
            NotificationService.PlaySound("Tink");
            this.AdjustmentValueChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            switch (actionParameter)
            {
                case "Work":
                    this.Timer.ResetWorkMinutes();
                    break;
                case "Short Break":
                    this.Timer.ResetShortBreakMinutes();
                    break;
                case "Long Break":
                    this.Timer.ResetLongBreakMinutes();
                    break;
            }
            PluginLog.Info($"Reset {actionParameter} duration to defaults");
            this.AdjustmentValueChanged();
        }

        protected override String GetAdjustmentValue(String actionParameter)
        {
            return $"{GetTimeFromActionParameter(actionParameter)} min";
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return $"{actionParameter}{Environment.NewLine}{GetTimeFromActionParameter(actionParameter)} min";
        }
    }
}