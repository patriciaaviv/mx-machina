namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    public class FocusModeService
    {
        // Apps to close when focus mode is enabled
        private readonly List<String> _distractingApps = new List<String>
        {
            "Discord",
            "Steam",
            "Slack",
            "Telegram",
            "WhatsApp",
            "Messages"
        };

        public Boolean IsEnabled { get; private set; }

        public event Action<Boolean> OnFocusModeChanged;

        public void Enable()
        {
            if (this.IsEnabled)
            {
                return;
            }

            this.IsEnabled = true;
            PluginLog.Info("Focus mode enabled");

            // Close distracting apps
            this.CloseDistractingApps();

            OnFocusModeChanged?.Invoke(true);
        }

        public void Disable()
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.IsEnabled = false;
            PluginLog.Info("Focus mode disabled");

            OnFocusModeChanged?.Invoke(false);
        }

        public void Toggle()
        {
            if (this.IsEnabled)
            {
                this.Disable();
            }
            else
            {
                this.Enable();
            }
        }

        private void CloseDistractingApps()
        {
            foreach (var appName in this._distractingApps)
            {
                try
                {
                    // Use killall to close the app
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "killall",
                            Arguments = appName,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    process.WaitForExit(1000);

                    if (process.ExitCode == 0)
                    {
                        PluginLog.Info($"Closed {appName}");
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Verbose($"Could not close {appName}: {ex.Message}");
                }
            }
        }

        public void AddDistractingApp(String appName)
        {
            if (!this._distractingApps.Contains(appName))
            {
                this._distractingApps.Add(appName);
                PluginLog.Info($"Added {appName} to distracting apps list");
            }
        }

        public void RemoveDistractingApp(String appName)
        {
            if (this._distractingApps.Remove(appName))
            {
                PluginLog.Info($"Removed {appName} from distracting apps list");
            }
        }

        public List<String> GetDistractingApps()
        {
            return new List<String>(this._distractingApps);
        }
    }
}
