namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class PomodoroCommand : PluginDynamicCommand
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        private bool _eventsSubscribed = false;

        private void EnsureEventsSubscribed()
        {
            if (!_eventsSubscribed)
            {
                Timer.OnTick += () => this.ActionImageChanged();
                Timer.OnStateChanged += () => this.ActionImageChanged();
                Timer.OnSessionComplete += (state) =>
                {
                    PluginLog.Info($"Pomodoro session completed: {state}");
                };
                _eventsSubscribed = true;
            }
        }

        public PomodoroCommand()
            : base(displayName: "Pomodoro Timer", description: "Start/Pause pomodoro timer", groupName: "Pomodoro")
        {
            // Add action parameters for different controls
            this.AddParameter("toggle", "Start/Pause", "Pomodoro");
            this.AddParameter("reset", "Reset Timer", "Pomodoro");
            this.AddParameter("skip", "Skip Session", "Pomodoro");
        }

        protected override void RunCommand(String actionParameter)
        {
            EnsureEventsSubscribed();

            switch (actionParameter)
            {
                case "toggle":
                    Timer.Toggle();
                    PluginLog.Info($"Pomodoro {(Timer.IsRunning ? "started" : "paused")}: {Timer.GetDisplayTime()}");
                    break;

                case "reset":
                    Timer.Reset();
                    PluginLog.Info("Pomodoro timer reset");
                    break;

                case "skip":
                    Timer.Skip();
                    PluginLog.Info($"Skipped to: {Timer.GetStateLabel()}");
                    break;

                default:
                    // Default action is toggle
                    Timer.Toggle();
                    break;
            }

            this.ActionImageChanged();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            EnsureEventsSubscribed();

            var builder = new BitmapBuilder(imageSize);

            // Set background color based on state
            BitmapColor bgColor;
            BitmapColor textColor = BitmapColor.White;
            BitmapColor accentColor;

            switch (Timer.CurrentState)
            {
                case PomodoroState.Work:
                    bgColor = Timer.IsRunning ? new BitmapColor(200, 60, 60) : new BitmapColor(120, 40, 40);
                    accentColor = new BitmapColor(255, 100, 100);
                    break;
                case PomodoroState.ShortBreak:
                case PomodoroState.LongBreak:
                    bgColor = Timer.IsRunning ? new BitmapColor(60, 160, 60) : new BitmapColor(40, 100, 40);
                    accentColor = new BitmapColor(100, 255, 100);
                    break;
                default:
                    bgColor = new BitmapColor(60, 60, 60);
                    accentColor = new BitmapColor(150, 150, 150);
                    break;
            }

            builder.Clear(bgColor);

            // Determine what to display
            string displayText;
            string labelText;

            switch (actionParameter)
            {
                case "reset":
                    displayText = "RST";
                    labelText = "Reset";
                    break;
                case "skip":
                    displayText = "SKP";
                    labelText = "Skip";
                    break;
                default: // toggle
                    displayText = Timer.GetDisplayTime();
                    labelText = Timer.GetStateLabel();
                    break;
            }

            // Draw label at top
            builder.DrawText(labelText, 0, 8, builder.Width, 20, accentColor, 12);

            // Draw main time/text in center
            builder.DrawText(displayText, 0, 25, builder.Width, 30, textColor, 18);

            // Draw pomodoro count at bottom for toggle button
            if (actionParameter == "toggle" || string.IsNullOrEmpty(actionParameter))
            {
                var pomoText = $"{Timer.CompletedPomodoros}/4";
                builder.DrawText(pomoText, 0, 55, builder.Width, 20, accentColor, 10);
            }

            // Draw running indicator
            if (Timer.IsRunning && (actionParameter == "toggle" || string.IsNullOrEmpty(actionParameter)))
            {
                builder.FillRectangle(builder.Width - 12, 4, 8, 8, accentColor);
            }

            return builder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                "reset" => "Reset",
                "skip" => "Skip",
                _ => $"{Timer.GetStateLabel()}{Environment.NewLine}{Timer.GetDisplayTime()}"
            };
        }
    }
}
