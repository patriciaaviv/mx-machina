namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class ResetCommand : PluginDynamicCommand
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        public ResetCommand()
            : base(displayName: "Reset", description: "Reset Pomodoro Timer", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            this.Timer.Reset();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return "Reset";
        }

    }
}