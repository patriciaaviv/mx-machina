namespace Loupedeck.MXMachinaPlugin
{
    // Singleton service to share the PomodoroTimer across all commands and adjustments
    internal static class PomodoroService
    {
        private static PomodoroTimer _timer;
        private static readonly object _lock = new object();

        public static PomodoroTimer Timer
        {
            get
            {
                lock (_lock)
                {
                    _timer ??= new PomodoroTimer();
                    return _timer;
                }
            }
        }
    }
}
