namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Timers;

    public enum PomodoroState
    {
        Inactive,
        Work,
        ShortBreak,
        LongBreak
    }

    public class PomodoroTimer : IDisposable
    {
        public const Int32 PomodorosBeforeLongBreak = 4;

        // Default durations and their limits in minutes
        private const Int32 DefaultWorkMinutes = 25;
        private const Int32 DefaultShortBreakMinutes = 5;
        private const Int32 DefaultLongBreakMinutes = 15;

        private readonly Timer _timer;
        private DateTime _endTime;
        private TimeSpan _remainingTime;

        public event Action OnTick;
        public event Action OnStateChanged;
        public event Action<PomodoroState> OnSessionComplete;

        public PomodoroState CurrentState { get; private set; } = PomodoroState.Inactive;

        private Int32 _workMinutes = DefaultWorkMinutes;
        private Int32 _shortBreakMinutes = DefaultShortBreakMinutes;
        private Int32 _longBreakMinutes = DefaultLongBreakMinutes;

        public Int32 WorkMinutes
        {
            get => this._workMinutes;
            set => this._workMinutes = Math.Clamp(value, 5, 60);
        }

        public Int32 ShortBreakMinutes
        {
            get => this._shortBreakMinutes;
            set => this._shortBreakMinutes = Math.Clamp(value, 5, 60);
        }

        public Int32 LongBreakMinutes
        {
            get => this._longBreakMinutes;
            set => this._longBreakMinutes = Math.Clamp(value, 5, 60);
        }

        public void ResetWorkMinutes()
        {
            this._workMinutes = DefaultWorkMinutes;
        }

        public void ResetLongBreakMinutes()
        {
            this._longBreakMinutes = DefaultLongBreakMinutes;
        }

        public void ResetShortBreakMinutes()
        {
            this._shortBreakMinutes = DefaultShortBreakMinutes;
        }

        public void ResetAllTimeSettings()
        {
            this.ResetWorkMinutes();
            this.ResetLongBreakMinutes();
            this.ResetShortBreakMinutes();
        }

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
            if (this.CurrentState == PomodoroState.Inactive)
            {
                this.CurrentState = PomodoroState.Work;
                this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
                PomodoroService.Notification.ShowNotification("🍅 Timer started", $"Focus for {GetDisplayTime()} minutes!", "Blow");
                OnStateChanged?.Invoke();
            }
            else {
                PomodoroService.Notification.ShowNotification("🍅 Timer resumed", $"Focus for {GetDisplayTime()} minutes!", "Blow");
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
                PomodoroService.Notification.ShowNotification("🍅 Timer paused", $"{GetDisplayTime()} minutes remaining...", "Blow");
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
            this.CurrentState = PomodoroState.Inactive;
            this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
            this.CompletedPomodoros = 0;
            PomodoroService.Notification.ShowNotification("🍅 Timer resetted", "Starting with fresh stats!", "Blow");
            OnStateChanged?.Invoke();
            OnTick?.Invoke();
        }

        public void Skip()
        {
            if (this.CurrentState != PomodoroState.Inactive)
            {
                this.CompleteCurrentSession();
                PomodoroService.Notification.ShowNotification($"🍅 {this.GetStateLabel()} skipped", "Are you cheating 👀?", "Blow");
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
                PomodoroState.Inactive => "Ready",
                PomodoroState.Work => "Focus",
                PomodoroState.ShortBreak => "Break",
                PomodoroState.LongBreak => "Long Break",
                // Impossibruh :-)
                _ => throw new ApplicationException()
            };
        }

        public void Dispose()
        {
            this._timer.Stop();
            this._timer.Dispose();
        }
    }
}