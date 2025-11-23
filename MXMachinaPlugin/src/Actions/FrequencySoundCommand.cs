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

            // Play contextual noise based on Pomodoro timer state
            var timer = PomodoroService.Timer;
            var duration = 30; // Default 30 seconds

            // Adjust duration based on timer state - play for remaining time if timer is running
            if (timer.IsRunning && timer.CurrentState == PomodoroState.Work)
            {
                var remaining = (Int32)timer.RemainingTime.TotalMinutes;
                duration = Math.Min(remaining * 60, 300); // Up to 5 minutes, but not longer than remaining time
            }
            else if (timer.IsRunning && (timer.CurrentState == PomodoroState.ShortBreak || timer.CurrentState == PomodoroState.LongBreak))
            {
                var remaining = (Int32)timer.RemainingTime.TotalMinutes;
                duration = Math.Min(remaining * 60, 180); // Up to 3 minutes for breaks
            }

            // Determine noise type based on context
            var noiseType = this.FrequencySound.DetermineNoiseTypeForContext();
            var noiseName = noiseType switch
            {
                NoiseType.PinkNoise => "Pink Noise (Soft)",
                NoiseType.WhiteNoise => "White Noise (Sleep)",
                NoiseType.BrownNoise => "Brown Noise (Deep)",
                NoiseType.GreenNoise => "Green Noise (Calming)",
                NoiseType.BlueNoise => "Blue Noise (Lively)",
                NoiseType.VioletNoise => "Violet Noise (Uplifting)",
                NoiseType.GreyNoise => "Grey Noise (Balanced)",
                _ => "Noise"
            };

            this.FrequencySound.PlayNoise(noiseType, duration);

            // Update UI after sound starts
            this.ActionImageChanged();

            PomodoroService.Notification.ShowNotification(
                "Frequency Sound",
                $"Playing {noiseName}",
                "Glass"
            );
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            if (!this.FrequencySound.IsMacOS)
            {
                return "Frequency\n(macOS only)";
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

