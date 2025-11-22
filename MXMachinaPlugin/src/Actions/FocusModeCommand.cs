namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class FocusModeCommand : PluginDynamicCommand
    {
        private FocusModeService FocusMode => PomodoroService.FocusMode;

        public FocusModeCommand()
            : base(displayName: "Focus Mode", description: "Toggle focus mode (closes distracting apps)", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            this.FocusMode.Toggle();

            if (this.FocusMode.IsEnabled)
            {
                PomodoroService.Notification.ShowNotification(
                    "Focus Mode ON",
                    "Distracting apps closed.",
                    "Glass"
                );
            }
            else
            {
                PomodoroService.Notification.ShowNotification(
                    "Focus Mode OFF",
                    "Focus mode disabled.",
                    "Purr"
                );
            }

            this.ActionImageChanged();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return this.FocusMode.IsEnabled ? "Focus\nON" : "Focus\nOFF";
        }
    }
}