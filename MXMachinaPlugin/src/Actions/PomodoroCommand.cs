namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class PomodoroCommand : PluginDynamicCommand
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        private Boolean _eventsSubscribed = false;

        private void EnsureEventsSubscribed()
        {
            if (!this._eventsSubscribed)
            {
                this.Timer.OnTick += () => this.ActionImageChanged();
                this.Timer.OnStateChanged += () => this.ActionImageChanged();
                this.Timer.OnSessionComplete += (state) =>
                {
                    PluginLog.Info($"Pomodoro session completed: {state}");
                };
                this._eventsSubscribed = true;
            }
        }

        public PomodoroCommand()
            : base(displayName: "Pomodoro Timer", description: "Start/Pause pomodoro timer", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            this.EnsureEventsSubscribed();
            this.Timer.Toggle();
            this.ActionImageChanged();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            this.EnsureEventsSubscribed();

            var builder = new BitmapBuilder(imageSize);

            // Set background color based on state
            BitmapColor bgColor;
            BitmapColor textColor = BitmapColor.White;
            BitmapColor accentColor;

            switch (this.Timer.CurrentState)
            {
                case PomodoroState.Work:
                    bgColor = this.Timer.IsRunning ? new BitmapColor(200, 60, 60) : new BitmapColor(120, 40, 40);
                    accentColor = new BitmapColor(255, 100, 100);
                    break;
                case PomodoroState.ShortBreak:
                case PomodoroState.LongBreak:
                    bgColor = this.Timer.IsRunning ? new BitmapColor(60, 160, 60) : new BitmapColor(40, 100, 40);
                    accentColor = new BitmapColor(100, 255, 100);
                    break;
                default:
                    bgColor = new BitmapColor(60, 60, 60);
                    accentColor = new BitmapColor(150, 150, 150);
                    break;
            }

            builder.Clear(bgColor);
            // Determine what to display
            String displayText = this.Timer.GetDisplayTime();
            String labelText = this.Timer.GetStateLabel();

            // Draw label at top
            builder.DrawText(labelText, 0, 8, builder.Width, 20, accentColor, 12);

            // Draw main time/text in center
            builder.DrawText(displayText, 0, 25, builder.Width, 30, textColor, 18);

            // Draw pomodoro count at bottom for toggle button
            if (actionParameter == "toggle" || String.IsNullOrEmpty(actionParameter))
            {
                var pomoText = $"{this.Timer.CompletedPomodoros}/4";
                builder.DrawText(pomoText, 0, 55, builder.Width, 20, accentColor, 10);
            }

            // Draw running indicator
            if (this.Timer.IsRunning)
            {
                builder.FillRectangle(builder.Width - 12, 4, 8, 8, accentColor);
            }

            return builder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return this.Timer.CurrentState switch
            {
                PomodoroState.Inactive => "Start Timer",
                PomodoroState.Work => "Stop Timer",
                PomodoroState.ShortBreak => "Skip Short Break",
                PomodoroState.LongBreak => "Skip Long Break",
                // Can never happen :-)
                _ => throw new ApplicationException()
            };
        }
    }
}