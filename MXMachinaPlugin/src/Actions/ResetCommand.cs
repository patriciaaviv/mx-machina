namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class StopCommand : PluginDynamicCommand
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        public StopCommand()
            : base(displayName: "Stop", description: "Stop and Reset Pomodoro Timer", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            this.Timer.Stop();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return "Stop";
        }

    }
}