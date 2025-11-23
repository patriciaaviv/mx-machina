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
        public event Action OnPause;
        public event Action OnWorkBegin;

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
                // Timer completed - advance to next phase
                CompleteWorkSession();
                this.Skip();
            }
            else
            {
                OnTick?.Invoke();
            }
        }

        private void CompleteWorkSession()
        {
            this.CompletedPomodoros++;
            OnSessionComplete?.Invoke(PomodoroPhase.Work);
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

            var oldState = this.State;
            var isNewPhase = this.Phase != this.GetPhaseForState(newState);

            this.State = newState;

            switch (newState)
            {
                case TimerState.WorkRunning:
                    this.StartTimer(isNewPhase, PomodoroPhase.Work);
                    this.OnWorkBegin?.Invoke();
                    if (oldState == TimerState.WorkPaused)
                    {
                        PomodoroService.Notification.ShowNotification("▶️ Resuming Timer", $"Keep going for {GetDisplayTime()}!", "Blow");
                    }
                    else
                    {
                        PomodoroService.Notification.ShowNotification("🍅 Started Timer", $"Focus for {this.WorkMinutes} min!", "Blow");
                    }
                    break;

                case TimerState.ShortBreak:
                    this.StartTimer(isNewPhase, PomodoroPhase.ShortBreak);
                    PomodoroService.Notification.ShowNotification("🥳 Short Break", $"Enjoy a {this.ShortBreakMinutes} min break!", "Blow");
                    break;

                case TimerState.LongBreak:
                    this.StartTimer(isNewPhase, PomodoroPhase.LongBreak);
                    PomodoroService.Notification.ShowNotification("🥳 Long Break", $"Enjoy a {this.LongBreakMinutes} min break!", "Blow");
                    break;

                case TimerState.WorkPaused:
                    this.PauseTimer();
                    PomodoroService.Notification.ShowNotification("⏸️ Paused Timer", $"{GetDisplayTime()} remaining...", "Blow");
                    break;

                case TimerState.Stopped:
                    this.StopTimer();
                    PomodoroService.Notification.ShowNotification("❌ Stopped Timer", "Good work!", "Blow");
                    break;
            }

            OnStateChanged?.Invoke();
            OnTick?.Invoke();
        }

        private void StartTimer(Boolean resetDuration, PomodoroPhase phase)
        {
            if (resetDuration)
            {
                this._remainingTime = this.GetDurationForPhase(phase);
            }
            this._endTime = DateTime.Now + this._remainingTime;
            this._timer.Start();
        }

        private void PauseTimer()
        {
            // Calculate directly since IsRunning is already false at this point
            var remaining = this._endTime - DateTime.Now;
            this._remainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            this._timer.Stop();
        }

        private void StopTimer()
        {
            this._timer.Stop();
            this._remainingTime = TimeSpan.FromMinutes(this.WorkMinutes);
            this.CompletedPomodoros = 0;
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
                this.OnPause?.Invoke();
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
                this.CompleteWorkSession();
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