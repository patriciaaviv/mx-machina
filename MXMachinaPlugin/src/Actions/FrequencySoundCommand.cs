namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Threading.Tasks;

    public class FrequencySoundCommand : PluginDynamicCommand
    {
        private FrequencySoundService FrequencySound => PomodoroService.FrequencySound;

        public FrequencySoundCommand()
            : base(displayName: "Frequency Sound", description: "Play context-aware frequency sounds for focus", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.FrequencySound.IsMacOS)
            {
                PomodoroService.Notification.ShowNotification(
                    "Frequency Sound",
                    "Only available on macOS",
                    "Basso"
                );
                return;
            }

            // Check if timer is running
            if (!PomodoroService.Timer.IsRunning)
            {
                PomodoroService.Notification.ShowNotification(
                    "Frequency Sound",
                    "Timer must be running to use frequency sounds",
                    "Basso"
                );
                return;
            }

            if (this.FrequencySound.IsPlaying)
            {
                this.FrequencySound.StopFrequency(useFadeOut: true);
                PomodoroService.Notification.ShowNotification(
                    "Frequency Sound",
                    "Fading out...",
                    "Purr"
                );
                this.ActionImageChanged();
                return;
            }

            // Get LLM-generated playlist based on context
            PomodoroService.Notification.ShowNotification(
                "Frequency Sound",
                "Generating playlist...",
                "Glass"
            );

            Task.Run(async () =>
            {
                try
                {
                    var playlist = await this.FrequencySound.GetRecommendedPlaylistAsync();
                    
                    if (playlist != null && playlist.Segments.Count > 0)
                    {
                        var segmentCount = playlist.Segments.Count;
                        var totalMinutes = playlist.TotalDurationSeconds / 60;
                        
                        PomodoroService.Notification.ShowNotification(
                            "Frequency Sound",
                            $"Playing {segmentCount} segments ({totalMinutes} min)",
                            "Glass"
                        );

                        // Update UI
                        this.ActionImageChanged();

                        // Play the playlist
                        this.FrequencySound.PlayPlaylist(playlist);
                    }
                    else
                    {
                        PomodoroService.Notification.ShowNotification(
                            "Frequency Sound",
                            "Failed to generate playlist",
                            "Basso"
                        );
                        this.ActionImageChanged();
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to play frequency sound playlist");
                    PomodoroService.Notification.ShowNotification(
                        "Frequency Sound",
                        "Error: Check OpenAI configuration",
                        "Basso"
                    );
                    this.ActionImageChanged();
                }
            });
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            if (!this.FrequencySound.IsMacOS)
            {
                return "Frequency\n(macOS only)";
            }

            if (!PomodoroService.Timer.IsRunning)
            {
                return "Frequency\n(Start timer)";
            }

            if (this.FrequencySound.IsPlaying)
            {
                return "Frequency\nON";
            }

            return "Frequency\nSound";
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            // Return a simple icon indicating frequency sound status
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (!this.FrequencySound.IsMacOS)
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("macOS\nonly", BitmapColor.White);
                }
                else if (!PomodoroService.Timer.IsRunning)
                {
                    // Timer not running - show disabled state
                    bitmapBuilder.Clear(new BitmapColor(60, 60, 60)); // Dark gray
                    bitmapBuilder.DrawText("🔊\nTimer\nneeded", BitmapColor.White);
                }
                else if (this.FrequencySound.IsPlaying)
                {
                    bitmapBuilder.Clear(BitmapColor.Green);
                    bitmapBuilder.DrawText("🔊\nON", BitmapColor.White);
                }
                else
                {
                    bitmapBuilder.Clear(BitmapColor.Blue);
                    bitmapBuilder.DrawText("🔊\nOFF", BitmapColor.White);
                }

                return bitmapBuilder.ToImage();
            }
        }
    }
}

