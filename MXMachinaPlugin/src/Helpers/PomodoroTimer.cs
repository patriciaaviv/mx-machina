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
        public const Int32 DefaultWorkMinutes = 25;
        public const Int32 DefaultShortBreakMinutes = 5;
        public const Int32 DefaultLongBreakMinutes = 15;
        public const Int32 PomodorosBeforeLongBreak = 4;

        private readonly Timer _timer;
        private DateTime _endTime;
        private TimeSpan _remainingTime;

        public event Action OnTick;
        public event Action OnStateChanged;
        public event Action<PomodoroState> OnSessionComplete;

        public PomodoroState CurrentState { get; private set; } = PomodoroState.Stopped;
        public Int32 WorkMinutes { get; set; } = DefaultWorkMinutes;
        public Int32 ShortBreakMinutes { get; set; } = DefaultShortBreakMinutes;
        public Int32 LongBreakMinutes { get; set; } = DefaultLongBreakMinutes;
        public Int32 CompletedPomodoros { get; private set; }

        public Boolean IsRunning { get; private set; }

        public TimeSpan RemainingTime
        {
            get
            {
                if (this.IsRunning)
                {
                    var remaining = this._endTime - DateTime.Now;
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
                return this._remainingTime;
            }
        }

        public PomodoroTimer()
        {
            this._timer = new Timer(1000); // Update every second
            this._timer.Elapsed += this.OnTimerElapsed;
            this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
        }

        private void OnTimerElapsed(Object sender, ElapsedEventArgs e)
        {
            var remaining = this.RemainingTime;

            if (remaining <= TimeSpan.Zero)
            {
                this.CompleteCurrentSession();
            }
            else
            {
                OnTick?.Invoke();
            }
        }

        public void Start()
        {
            if (this.CurrentState == PomodoroState.Stopped)
            {
                // Start a new work session
                this.CurrentState = PomodoroState.Work;
                this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
                OnStateChanged?.Invoke();
            }

            if (!this.IsRunning)
            {
                this._endTime = DateTime.Now + this._remainingTime;
                this.IsRunning = true;
                this._timer.Start();
                OnTick?.Invoke();
            }
        }

        public void Pause()
        {
            if (this.IsRunning)
            {
                this._remainingTime = this.RemainingTime;
                this.IsRunning = false;
                this._timer.Stop();
                OnTick?.Invoke();
            }
        }

        public void Toggle()
        {
            if (this.IsRunning)
            {
                this.Pause();
            }
            else
            {
                this.Start();
            }
        }

        public void Reset()
        {
            this._timer.Stop();
            this.IsRunning = false;
            this.CurrentState = PomodoroState.Stopped;
            this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
            this.CompletedPomodoros = 0;
            OnStateChanged?.Invoke();
            OnTick?.Invoke();
        }

        public void Skip()
        {
            if (this.CurrentState != PomodoroState.Stopped)
            {
                this.CompleteCurrentSession();
            }
        }

        private void CompleteCurrentSession()
        {
            this._timer.Stop();
            this.IsRunning = false;

            var completedState = this.CurrentState;
            OnSessionComplete?.Invoke(completedState);

            // Determine next state
            if (this.CurrentState == PomodoroState.Work)
            {
                this.CompletedPomodoros++;

                if (this.CompletedPomodoros % PomodorosBeforeLongBreak == 0)
                {
                    this.CurrentState = PomodoroState.LongBreak;
                    this._remainingTime = TimeSpan.FromMinutes(this.LongBreakMinutes);
                }
                else
                {
                    this.CurrentState = PomodoroState.ShortBreak;
                    this._remainingTime = TimeSpan.FromMinutes(this.ShortBreakMinutes);
                }
            }
            else
            {
                // Break completed, start new work session
                this.CurrentState = PomodoroState.Work;
                this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
            }

            OnStateChanged?.Invoke();
            OnTick?.Invoke();

            // Auto-start the next session
            this._endTime = DateTime.Now + this._remainingTime;
            this.IsRunning = true;
            this._timer.Start();
        }

        public String GetDisplayTime()
        {
            var time = this.RemainingTime;
            return $"{(Int32)time.TotalMinutes:D2}:{time.Seconds:D2}";
        }

        public String GetStateLabel()
        {
            return this.CurrentState switch
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
            this._timer.Stop();
            this._timer.Dispose();
        }
    }
}