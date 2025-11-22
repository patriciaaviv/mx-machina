namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Timers;

    public enum TimerState
    {
        Stopped,
        WorkRunning,
        WorkPaused,
        ShortBreak,
        LongBreak
    }

    public enum PomodoroPhase
    {
        Stopped,
        Work,
        ShortBreak,
        LongBreak
    }

    public class PomodoroTimer : IDisposable
    {
        public const Int32 PomodorosBeforeLongBreak = 4;

        private const Int32 DefaultWorkMinutes = 25;
        private const Int32 DefaultShortBreakMinutes = 5;
        private const Int32 DefaultLongBreakMinutes = 15;

        private readonly Timer _timer;
        private DateTime _endTime;
        private TimeSpan _remainingTime;

        public event Action OnTick;
        public event Action OnStateChanged;
        public event Action<PomodoroPhase> OnSessionComplete;

        public TimerState State { get; private set; } = TimerState.Stopped;

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

        public void ResetWorkMinutes() => this._workMinutes = DefaultWorkMinutes;
        public void ResetShortBreakMinutes() => this._shortBreakMinutes = DefaultShortBreakMinutes;
        public void ResetLongBreakMinutes() => this._longBreakMinutes = DefaultLongBreakMinutes;

        public void ResetAllTimeSettings()
        {
            this.ResetWorkMinutes();
            this.ResetShortBreakMinutes();
            this.ResetLongBreakMinutes();
        }

        public Int32 CompletedPomodoros { get; private set; }

        // Derived properties from State
        public Boolean IsRunning => this.State is TimerState.WorkRunning
                                                or TimerState.ShortBreak
                                                or TimerState.LongBreak;

        public Boolean IsPaused => this.State == TimerState.WorkPaused;

        public Boolean IsActive => this.State != TimerState.Stopped;

        public PomodoroPhase Phase => this.State switch
        {
            TimerState.Stopped => PomodoroPhase.Stopped,
            TimerState.WorkRunning or TimerState.WorkPaused => PomodoroPhase.Work,
            TimerState.ShortBreak => PomodoroPhase.ShortBreak,
            TimerState.LongBreak => PomodoroPhase.LongBreak,
            _ => throw new InvalidOperationException($"Unknown state: {this.State}")
        };

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
            this._timer = new Timer(1000);
            this._timer.Elapsed += this.OnTimerElapsed;
            this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
        }

        private void OnTimerElapsed(Object sender, ElapsedEventArgs e)
        {
            if (this.RemainingTime <= TimeSpan.Zero)
            {
                // Timer completed - fire event and advance to next phase
                var completedPhase = this.Phase;
                OnSessionComplete?.Invoke(completedPhase);
                this.Skip();
            }
            else
            {
                OnTick?.Invoke();
            }
        }

        private TimeSpan GetDurationForPhase(PomodoroPhase phase) => phase switch
        {
            PomodoroPhase.Work => TimeSpan.FromMinutes(this.WorkMinutes),
            PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(this.ShortBreakMinutes),
            PomodoroPhase.LongBreak => TimeSpan.FromMinutes(this.LongBreakMinutes),
            _ => TimeSpan.FromMinutes(this.WorkMinutes)
        };

        private PomodoroPhase GetPhaseForState(TimerState state) => state switch
        {
            TimerState.Stopped => PomodoroPhase.Stopped,
            TimerState.WorkRunning or TimerState.WorkPaused => PomodoroPhase.Work,
            TimerState.ShortBreak => PomodoroPhase.ShortBreak,
            TimerState.LongBreak => PomodoroPhase.LongBreak,
            _ => throw new InvalidOperationException($"Unknown state: {state}")
        };

        private void TransitionToState(TimerState newState)
        {
            if (this.State == newState)
            {
                return;
            }

            var oldPhase = this.Phase;
            var oldState = this.State;
            var newPhase = this.GetPhaseForState(newState);

            this.State = newState;

            switch (newState)
            {
                // Entering a Running state
                case TimerState.WorkRunning:
                case TimerState.ShortBreak:
                case TimerState.LongBreak:
                    // If coming from paused state of same phase, resume with remaining time
                    // Otherwise, set timer to full duration of new phase
                    if (oldPhase != newPhase)
                    {
                        this._remainingTime = this.GetDurationForPhase(newPhase);
                    }
                    this._endTime = DateTime.Now + this._remainingTime;
                    this._timer.Start();

                    // Resume
                    if (oldState == TimerState.WorkPaused && newState == TimerState.WorkRunning)
                    {
                        PomodoroService.Notification.ShowNotification("▶️ Resuming Timer", $"Keep going for {GetDisplayTime()} min!!!", "Blow");
                    }
                    else if (newState == TimerState.ShortBreak)
                    {
                        PomodoroService.Notification.ShowNotification("🥳 Short Break", $"Enjoy a short {this.ShortBreakMinutes} min break!", "Blow");
                    }
                    else if (newState == TimerState.LongBreak)
                    {
                        PomodoroService.Notification.ShowNotification("🥳 Long Break", $"Enjoy a long {this.ShortBreakMinutes} min break!", "Blow");
                    }
                    // Starting Work
                    else if (newState == TimerState.WorkRunning)
                    {

                        PomodoroService.Notification.ShowNotification("🍅 Started Timer", $"Focus for {this.WorkMinutes} min!!!", "Blow");
                    }
                    break;

                // Entering WorkPaused state
                case TimerState.WorkPaused:
                    // Calculate directly since IsRunning is already false at this point
                    var remaining = this._endTime - DateTime.Now;
                    this._remainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                    this._timer.Stop();
                    PomodoroService.Notification.ShowNotification("⏸️ Paused Timer", $"{GetDisplayTime()} min remaining...", "Blow");
                    break;

                // Entering Stopped state
                case TimerState.Stopped:
                    this._timer.Stop();
                    this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
                    this.CompletedPomodoros = 0;
                    PomodoroService.Notification.ShowNotification("❌ Stopped Timer", "Good work!", "Blow");
                    break;
            }

            OnStateChanged?.Invoke();
            OnTick?.Invoke();
        }

        // === Public Actions ===

        /// <summary>
        /// Start a new pomodoro session. Only valid from Stopped state.
        /// </summary>
        public void Start()
        {
            if (this.State == TimerState.Stopped)
            {
                this.TransitionToState(TimerState.WorkRunning);
            }
        }

        /// <summary>
        /// Pause the current timer. Only valid from WorkRunning state.
        /// </summary>
        public void Pause()
        {
            if (this.State == TimerState.WorkRunning)
            {
                this.TransitionToState(TimerState.WorkPaused);
            }
        }

        /// <summary>
        /// Resume the paused timer. Only valid from WorkPaused state.
        /// </summary>
        public void Resume()
        {
            if (this.State == TimerState.WorkPaused)
            {
                this.TransitionToState(TimerState.WorkRunning);
            }
        }

        /// <summary>
        /// Skip to the next phase. Work -> Break, Break -> Work.
        /// </summary>
        public void Skip()
        {
            if (this.State == TimerState.Stopped)
            {
                return;
            }

            TimerState newState;

            if (this.Phase == PomodoroPhase.Work)
            {
                this.CompletedPomodoros++;
                newState = (this.CompletedPomodoros % PomodorosBeforeLongBreak == 0)
                    ? TimerState.LongBreak
                    : TimerState.ShortBreak;
            }
            else
            {
                // Break completed, start new work session
                newState = TimerState.WorkRunning;
            }

            this.TransitionToState(newState);
        }

        /// <summary>
        /// Stop the timer and reset to initial state.
        /// </summary>
        public void Stop()
        {
            this.TransitionToState(TimerState.Stopped);
        }

        /// <summary>
        /// Convenience method: Toggle between running and paused/stopped states.
        /// </summary>
        public void Toggle()
        {
            if (this.State == TimerState.WorkRunning)
            {
                this.Pause();
            }
            else if (this.State == TimerState.WorkPaused)
            {
                this.Resume();
            }
            // ShortBreak and LongBreak cannot be paused - no-op
        }

        // === Display Helpers ===
        public String GetDisplayTime()
        {
            var time = this.RemainingTime;
            return $"{(Int32)time.TotalMinutes:D2}:{time.Seconds:D2}";
        }

        public String GetStateLabel() => this.Phase switch
        {
            PomodoroPhase.Stopped => "Ready",
            PomodoroPhase.Work => "Focus",
            PomodoroPhase.ShortBreak => "Break",
            PomodoroPhase.LongBreak => "Long Break",
            _ => throw new InvalidOperationException($"Unknown phase: {this.Phase}")
        };

        public void Dispose()
        {
            this._timer.Stop();
            this._timer.Dispose();
        }
    }
}