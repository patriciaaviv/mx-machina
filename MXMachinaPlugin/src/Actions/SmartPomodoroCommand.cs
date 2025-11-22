namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class SmartPomodoroCommand : PluginDynamicCommand
    {
        private PomodoroTimer Timer => PomodoroService.Timer;
        private ActivityMonitorService ActivityMonitor => PomodoroService.ActivityMonitor;
        private GoogleCalendarService Calendar => PomodoroService.Calendar;

        private Boolean _eventsSubscribed = false;

        public SmartPomodoroCommand()
            : base(displayName: "Smart Pomodoro", description: "AI-powered pomodoro with smart features", groupName: "Smart Pomodoro")
        {
            this.AddParameter("smartStart", "Smart Start", "Smart Pomodoro");
            this.AddParameter("insights", "Activity Insights", "Smart Pomodoro");
            this.AddParameter("calendarSync", "Sync Calendar", "Smart Pomodoro");
            this.AddParameter("suggestDuration", "Suggest Duration", "Smart Pomodoro");
        }

        private void EnsureEventsSubscribed()
        {
            if (!this._eventsSubscribed)
            {
                this.Timer.OnTick += () => this.ActionImageChanged();
                this.Timer.OnStateChanged += () => this.ActionImageChanged();
                this.ActivityMonitor.OnBreakSuggested += (suggestion) => this.ActionImageChanged();
                this._eventsSubscribed = true;
            }
        }

        protected override void RunCommand(String actionParameter)
        {
            this.EnsureEventsSubscribed();

            switch (actionParameter)
            {
                case "smartStart":
                    this.SmartStart();
                    break;

                case "insights":
                    this.ShowInsights();
                    break;

                case "calendarSync":
                    this.SyncWithCalendar();
                    break;

                case "suggestDuration":
                    this.SuggestOptimalDuration();
                    break;

                default:
                    this.SmartStart();
                    break;
            }

            this.ActionImageChanged();
        }

        private void SmartStart()
        {
            if (this.Timer.IsRunning)
            {
                this.Timer.Pause();
                PluginLog.Info("Smart Pomodoro paused");
            }
            else
            {
                // Get optimal duration based on activity patterns
                var optimalDuration = this.ActivityMonitor.GetOptimalWorkDuration();

                if (this.Timer.CurrentState == PomodoroState.Stopped)
                {
                    // Apply smart duration suggestion
                    this.Timer.WorkMinutes = optimalDuration;
                    PluginLog.Info($"Starting with AI-suggested duration: {optimalDuration} min");
                }

                this.Timer.Start();
                this.ActivityMonitor.StartMonitoring();
                PluginLog.Info($"Smart Pomodoro started: {this.Timer.GetDisplayTime()}");
            }
        }

        private void ShowInsights()
        {
            var insight = this.ActivityMonitor.GetProductivityInsight();
            PluginLog.Info($"Productivity Insight: {insight}");

            // Could also trigger a notification or display update
        }

        private async void SyncWithCalendar()
        {
            if (!this.Calendar.IsAuthenticated)
            {
                PluginLog.Warning("Calendar not authenticated. Please set up Google Calendar integration.");
                return;
            }

            try
            {
                var nextEvent = await this.Calendar.GetNextEventAsync();
                if (nextEvent != null)
                {
                    var timeUntil = nextEvent.Start - DateTime.Now;
                    if (timeUntil.TotalMinutes > 0 && timeUntil.TotalMinutes < this.Timer.WorkMinutes)
                    {
                        // Adjust work duration to fit before next meeting
                        var adjustedMinutes = (Int32)timeUntil.TotalMinutes - 5; // 5 min buffer
                        if (adjustedMinutes >= 10)
                        {
                            this.Timer.WorkMinutes = adjustedMinutes;
                            PluginLog.Info($"Adjusted work duration to {adjustedMinutes} min to fit before '{nextEvent.Title}'");
                        }
                        else
                        {
                            PluginLog.Info($"Not enough time before '{nextEvent.Title}' in {timeUntil.TotalMinutes:F0} min");
                        }
                    }
                    else
                    {
                        PluginLog.Info($"Next event: '{nextEvent.Title}' in {timeUntil.TotalMinutes:F0} min");
                    }
                }
                else
                {
                    PluginLog.Info("No upcoming events found");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to sync with calendar");
            }
        }

        private void SuggestOptimalDuration()
        {
            var suggested = this.ActivityMonitor.GetOptimalWorkDuration();
            this.Timer.WorkMinutes = suggested;

            if (this.Timer.CurrentState == PomodoroState.Stopped)
            {
                this.Timer.Reset(); // Apply new duration
            }

            PluginLog.Info($"Applied AI-suggested work duration: {suggested} min");
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            this.EnsureEventsSubscribed();

            var builder = new BitmapBuilder(imageSize);

            // Smart purple/blue theme
            BitmapColor bgColor;
            BitmapColor textColor = BitmapColor.White;
            BitmapColor accentColor;

            switch (actionParameter)
            {
                case "smartStart":
                    bgColor = this.Timer.IsRunning ? new BitmapColor(100, 50, 180) : new BitmapColor(60, 30, 120);
                    accentColor = new BitmapColor(180, 130, 255);
                    break;
                case "insights":
                    bgColor = new BitmapColor(50, 100, 150);
                    accentColor = new BitmapColor(130, 200, 255);
                    break;
                case "calendarSync":
                    bgColor = new BitmapColor(50, 120, 80);
                    accentColor = new BitmapColor(100, 255, 150);
                    break;
                default:
                    bgColor = new BitmapColor(80, 50, 150);
                    accentColor = new BitmapColor(160, 120, 255);
                    break;
            }

            builder.Clear(bgColor);

            String label;
            String mainText;
            var subText = "";

            switch (actionParameter)
            {
                case "smartStart":
                    label = "Smart";
                    mainText = this.Timer.GetDisplayTime();
                    subText = this.Timer.IsRunning ? "Running" : "Ready";
                    break;
                case "insights":
                    label = "Insights";
                    var activityLevel = this.ActivityMonitor.CurrentActivityLevel;
                    mainText = activityLevel > 70 ? "High" : activityLevel > 40 ? "Med" : "Low";
                    subText = "Activity";
                    break;
                case "calendarSync":
                    label = "Calendar";
                    mainText = "Sync";
                    subText = this.Calendar.IsAuthenticated ? "Ready" : "Setup";
                    break;
                case "suggestDuration":
                    label = "AI";
                    mainText = $"{this.ActivityMonitor.GetOptimalWorkDuration()}m";
                    subText = "Suggest";
                    break;
                default:
                    label = "Smart";
                    mainText = this.Timer.GetDisplayTime();
                    subText = "";
                    break;
            }

            // Draw label
            builder.DrawText(label, 0, 8, builder.Width, 20, accentColor, 11);

            // Draw main text
            builder.DrawText(mainText, 0, 25, builder.Width, 30, textColor, 16);

            // Draw sub text
            if (!String.IsNullOrEmpty(subText))
            {
                builder.DrawText(subText, 0, 55, builder.Width, 20, accentColor, 10);
            }

            // AI indicator dot
            builder.FillRectangle(builder.Width - 10, 6, 6, 6, new BitmapColor(255, 200, 50));

            return builder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                "smartStart" => $"Smart{Environment.NewLine}{this.Timer.GetDisplayTime()}",
                "insights" => $"Insights{Environment.NewLine}{this.ActivityMonitor.GetProductivityInsight()}",
                "calendarSync" => "Calendar Sync",
                "suggestDuration" => $"AI Suggest{Environment.NewLine}{this.ActivityMonitor.GetOptimalWorkDuration()}m",
                _ => "Smart Pomodoro"
            };
        }
    }
}