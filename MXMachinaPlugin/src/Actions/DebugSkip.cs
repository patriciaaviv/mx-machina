namespace Loupedeck.MXMachinaPlugin
{
    using System;

    public class DebugSkipCommand : PluginDynamicCommand
    {
        private PomodoroTimer Timer => PomodoroService.Timer;

        public DebugSkipCommand()
            : base(displayName: "Skip Phase", description: "Skip Current Phase", groupName: "Pomodoro###Debug")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            this.Timer.Skip();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return "Skip";
        }

    }
}