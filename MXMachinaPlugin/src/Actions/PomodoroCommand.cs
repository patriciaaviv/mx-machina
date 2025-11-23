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
            : base(displayName: "Timer Status", description: "Start/Pause Pomodoro Timer", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            this.EnsureEventsSubscribed();
            switch (this.Timer.Phase)
            {
                case PomodoroPhase.Stopped:
                    this.Timer.Start();
                    break;
                case PomodoroPhase.Work:
                    this.Timer.Toggle();
                    break;
                case PomodoroPhase.ShortBreak:
                    this.Timer.Skip();
                    break;
                case PomodoroPhase.LongBreak:
                    this.Timer.Skip();
                    break;
                default:
                    break;
            }
            this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return this.Timer.Phase switch
            {
                PomodoroPhase.Stopped => "Start Timer",
                PomodoroPhase.Work => this.Timer.IsRunning ? "Pause Timer" : "Resume Timer",
                PomodoroPhase.ShortBreak => "Skip Short Break",
                PomodoroPhase.LongBreak => "Skip Long Break",
                // Can never happen :-)
                _ => throw new ApplicationException()
            };
        }
        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            this.EnsureEventsSubscribed();

            var builder = new BitmapBuilder(imageSize);

            // Set background color based on state
            BitmapColor bgColor;
            BitmapColor textColor = BitmapColor.White;
            BitmapColor accentColor;

            switch (this.Timer.Phase)
            {
                case PomodoroPhase.Work:
                    bgColor = this.Timer.IsRunning ? new BitmapColor(200, 60, 60) : new BitmapColor(120, 40, 40);
                    accentColor = new BitmapColor(255, 100, 100);
                    break;
                case PomodoroPhase.ShortBreak:
                case PomodoroPhase.LongBreak:
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

    }
}