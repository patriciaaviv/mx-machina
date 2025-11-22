namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Diagnostics;

    public class NotificationService
    {
        /// <summary>
        /// Shows a macOS notification with sound
        /// </summary>
        public void ShowNotification(String title, String message, String sound = "default")
        {
            try
            {
                var script = $@"display notification ""{EscapeString(message)}"" with title ""{EscapeString(title)}"" sound name ""{sound}""";
                RunAppleScript(script);
                PluginLog.Info($"Notification: {title} - {message}");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to show notification");
            }
        }

        /// <summary>
        /// Shows a floating notification overlay (using osascript)
        /// </summary>
        public void ShowOverlay(String message, PomodoroState state)
        {
            try
            {
                var emoji = state switch
                {
                    PomodoroState.Work => "🍅",
                    PomodoroState.ShortBreak => "☕",
                    PomodoroState.LongBreak => "🌴",
                    _ => "⏱️"
                };

                var title = state switch
                {
                    PomodoroState.Work => "Focus Time Started",
                    PomodoroState.ShortBreak => "Short Break",
                    PomodoroState.LongBreak => "Long Break",
                    _ => "Pomodoro"
                };

                ShowNotification($"{emoji} {title}", message, "Blow");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to show overlay");
            }
        }

        /// <summary>
        /// Plays a sound alert
        /// </summary>
        public void PlaySound(String soundName = "Glass")
        {
            try
            {
                var script = $@"do shell script ""afplay /System/Library/Sounds/{soundName}.aiff""";
                RunAppleScriptAsync(script);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to play sound");
            }
        }

        // /// <summary>
        // /// Shows timer started notification
        // /// </summary>
        // public void NotifyTimerStarted(Int32 minutes, PomodoroState state)
        // {
        //     var stateText = state switch
        //     {
        //         PomodoroState.Work => "Focus session",
        //         PomodoroState.ShortBreak => "Short break",
        //         PomodoroState.LongBreak => "Long break",
        //         _ => "Timer"
        //     };
        //
        //     ShowOverlay($"{stateText} for {minutes} minutes. Stay focused!", state);
        // }

        /// <summary>
        /// Shows timer paused notification
        /// </summary>
        public void NotifyTimerPaused(String remainingTime) => ShowNotification("⏸️ Timer Paused", $"Remaining: {remainingTime}. Press again to resume.", "Tink");

        /// <summary>
        /// Shows session complete notification
        /// </summary>
        public void NotifySessionComplete(PomodoroState completedState, Int32 completedCount)
        {
            switch (completedState)
            {
                case PomodoroState.Work:
                    ShowNotification(
                        "🎉 Focus Session Complete!",
                        $"Great work! You've completed {completedCount} pomodoro(s). Time for a break!",
                        "Hero"
                    );
                    break;
                case PomodoroState.ShortBreak:
                case PomodoroState.LongBreak:
                    ShowNotification(
                        "⚡ Break Over",
                        "Ready to focus? Your next work session is starting.",
                        "Ping"
                    );
                    break;
            }
        }

        private void RunAppleScript(String script)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardInput.WriteLine(script);
                process.StandardInput.Close();
                process.WaitForExit(5000);

                var error = process.StandardError.ReadToEnd();
                if (!String.IsNullOrEmpty(error))
                {
                    PluginLog.Warning($"AppleScript error: {error}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to run AppleScript");
            }
        }

        private void RunAppleScriptAsync(String script)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardInput.WriteLine(script);
                process.StandardInput.Close();
                // Don't wait - let it run asynchronously
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to run AppleScript async");
            }
        }

        private String EscapeString(String input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", " ");
        }
    }
}