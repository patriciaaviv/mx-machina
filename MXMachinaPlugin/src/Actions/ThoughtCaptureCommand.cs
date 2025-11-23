namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Command to capture distracting thoughts during Pomodoro sessions
    /// Triggered by gesture: Hold button + flick Down-Right
    /// </summary>
    public class ThoughtCaptureCommand : PluginDynamicCommand
    {
        private ThoughtCaptureService ThoughtService => PomodoroService.ThoughtCapture;

        public ThoughtCaptureCommand()
            : base(displayName: "Capture Thought", description: "Quickly capture a distracting thought", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            // Run on background thread to avoid blocking
            Task.Run(async () =>
            {
                try
                {
                    // Show minimal input at cursor position
                    var thought = MacTextInputHelper.ShowMinimalInputAtCursor("Capture thought...");

                    if (!String.IsNullOrWhiteSpace(thought))
                    {
                        // Capture the thought (async categorization happens automatically)
                        await this.ThoughtService.CaptureThoughtAsync(thought);

                        // Play satisfying "whoosh" sound
                        MacTextInputHelper.PlayWhooshSound();

                        // Show brief confirmation
                        PomodoroService.Notification.ShowNotification(
                            "💭 Thought Captured",
                            "Review after your session",
                            "Glass"
                        );

                        PluginLog.Info($"Thought captured and categorized: {thought}");
                    }
                    else
                    {
                        PluginLog.Info("Thought capture cancelled");
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to capture thought");
                    PomodoroService.Notification.ShowNotification(
                        "Error",
                        "Failed to capture thought",
                        "Basso"
                    );
                }
            });
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var unreviewedCount = this.ThoughtService.GetUnreviewedThoughts().Count;
            if (unreviewedCount > 0)
            {
                return $"Thought\n({unreviewedCount})";
            }
            return "Capture\nThought";
        }
    }
}

