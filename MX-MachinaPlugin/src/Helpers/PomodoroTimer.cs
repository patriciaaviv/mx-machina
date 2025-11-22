namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Timers;

    public enum PomodoroState
    {
        Stopped,
        Work,
        ShortBreak,
        LongBreak
    }

    public class PomodoroTimer : IDisposable
    {
        // Default durations in minutes
        public const int DefaultWorkMinutes = 25;
        public const int DefaultShortBreakMinutes = 5;
        public const int DefaultLongBreakMinutes = 15;
        public const int PomodorosBeforeLongBreak = 4;

        private readonly Timer _timer;
        private DateTime _endTime;
        private TimeSpan _remainingTime;
        private bool _isRunning;
        private int _completedPomodoros;

        public event Action OnTick;
        public event Action OnStateChanged;
        public event Action<PomodoroState> OnSessionComplete;

        public PomodoroState CurrentState { get; private set; } = PomodoroState.Stopped;
        public int WorkMinutes { get; set; } = DefaultWorkMinutes;
        public int ShortBreakMinutes { get; set; } = DefaultShortBreakMinutes;
        public int LongBreakMinutes { get; set; } = DefaultLongBreakMinutes;
        public int CompletedPomodoros => _completedPomodoros;

        public bool IsRunning => _isRunning;

        public TimeSpan RemainingTime
        {
            get
            {
                if (_isRunning)
                {
                    var remaining = _endTime - DateTime.Now;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
                return _remainingTime;
            }
        }

        public PomodoroTimer()
        {
            _timer = new Timer(1000); // Update every second
            _timer.Elapsed += OnTimerElapsed;
            _remainingTime = TimeSpan.FromMinutes(WorkMinutes);
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            var remaining = RemainingTime;

            if (remaining <= TimeSpan.Zero)
            {
                CompleteCurrentSession();
            }
            else
            {
                OnTick?.Invoke();
            }
        }

        public void Start()
        {
            if (CurrentState == PomodoroState.Stopped)
            {
                // Start a new work session
                CurrentState = PomodoroState.Work;
                _remainingTime = TimeSpan.FromMinutes(WorkMinutes);
                OnStateChanged?.Invoke();
            }

            if (!_isRunning)
            {
                _endTime = DateTime.Now + _remainingTime;
                _isRunning = true;
                _timer.Start();
                OnTick?.Invoke();
            }
        }

        public void Pause()
        {
            if (_isRunning)
            {
                _remainingTime = RemainingTime;
                _isRunning = false;
                _timer.Stop();
                OnTick?.Invoke();
            }
        }

        public void Toggle()
        {
            if (_isRunning)
            {
                Pause();
            }
            else
            {
                Start();
            }
        }

        public void Reset()
        {
            _timer.Stop();
            _isRunning = false;
            CurrentState = PomodoroState.Stopped;
            _remainingTime = TimeSpan.FromMinutes(WorkMinutes);
            _completedPomodoros = 0;
            OnStateChanged?.Invoke();
            OnTick?.Invoke();
        }

        public void Skip()
        {
            if (CurrentState != PomodoroState.Stopped)
            {
                CompleteCurrentSession();
            }
        }

        private void CompleteCurrentSession()
        {
            _timer.Stop();
            _isRunning = false;

            var completedState = CurrentState;
            OnSessionComplete?.Invoke(completedState);

            // Determine next state
            if (CurrentState == PomodoroState.Work)
            {
                _completedPomodoros++;

                if (_completedPomodoros % PomodorosBeforeLongBreak == 0)
                {
                    CurrentState = PomodoroState.LongBreak;
                    _remainingTime = TimeSpan.FromMinutes(LongBreakMinutes);
                }
                else
                {
                    CurrentState = PomodoroState.ShortBreak;
                    _remainingTime = TimeSpan.FromMinutes(ShortBreakMinutes);
                }
            }
            else
            {
                // Break completed, start new work session
                CurrentState = PomodoroState.Work;
                _remainingTime = TimeSpan.FromMinutes(WorkMinutes);
            }

            OnStateChanged?.Invoke();
            OnTick?.Invoke();
        }

        public string GetDisplayTime()
        {
            var time = RemainingTime;
            return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
        }

        public string GetStateLabel()
        {
            return CurrentState switch
            {
                PomodoroState.Stopped => "Ready",
                PomodoroState.Work => "Focus",
                PomodoroState.ShortBreak => "Break",
                PomodoroState.LongBreak => "Long Break",
                _ => ""
            };
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
