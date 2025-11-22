namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Timers;

    public class ActivityData
    {
        public DateTime Timestamp { get; set; }
        public Int32 MouseMovements { get; set; }
        public Int32 KeyPresses { get; set; }
        public Double ActivityScore { get; set; }
    }

    public class SmartBreakSuggestion
    {
        public Boolean ShouldTakeBreak { get; set; }
        public String Reason { get; set; }
        public Int32 SuggestedBreakMinutes { get; set; }
    }

    public class ActivityMonitorService : IDisposable
    {
        private readonly Timer _samplingTimer;
        private readonly List<ActivityData> _activityHistory;
        private readonly Object _lock = new Object();

        private Int32 _currentMouseMovements;
        private Int32 _currentKeyPresses;
        private DateTime _lastActivityTime;
        private DateTime _sessionStartTime;
        private Int32 _consecutiveLowActivityMinutes;
        private Int32 _consecutiveHighActivityMinutes;

        // Thresholds for smart break detection
        private const Int32 LowActivityThreshold = 10;
        private const Int32 HighActivityThreshold = 100;
        private const Int32 MaxContinuousWorkMinutes = 90;
        private const Int32 IdleThresholdSeconds = 300; // 5 minutes

        public event Action<SmartBreakSuggestion> OnBreakSuggested;
        public event Action OnActivityDetected;

        public Boolean IsMonitoring { get; private set; }
        public TimeSpan SessionDuration => DateTime.Now - this._sessionStartTime;
        public Double CurrentActivityLevel { get; private set; }

        public ActivityMonitorService()
        {
            this._activityHistory = new List<ActivityData>();
            this._samplingTimer = new Timer(60000); // Sample every minute
            this._samplingTimer.Elapsed += this.OnSamplingTimerElapsed;
            this._lastActivityTime = DateTime.Now;
            this._sessionStartTime = DateTime.Now;
        }

        public void StartMonitoring()
        {
            if (this.IsMonitoring)
            {
                return;
            }

            this.IsMonitoring = true;
            this._sessionStartTime = DateTime.Now;
            this._samplingTimer.Start();
            PluginLog.Info("Activity monitoring started");
        }

        public void StopMonitoring()
        {
            this.IsMonitoring = false;
            this._samplingTimer.Stop();
            PluginLog.Info("Activity monitoring stopped");
        }

        public void RecordMouseMovement()
        {
            lock (this._lock)
            {
                this._currentMouseMovements++;
                this._lastActivityTime = DateTime.Now;
            }
            OnActivityDetected?.Invoke();
        }

        public void RecordKeyPress()
        {
            lock (this._lock)
            {
                this._currentKeyPresses++;
                this._lastActivityTime = DateTime.Now;
            }
            OnActivityDetected?.Invoke();
        }

        private void OnSamplingTimerElapsed(Object sender, ElapsedEventArgs e)
        {
            lock (this._lock)
            {
                // Calculate activity score (0-100)
                var activityScore = Math.Min(100, (this._currentMouseMovements + this._currentKeyPresses * 2) / 2.0);
                this.CurrentActivityLevel = activityScore;

                var data = new ActivityData
                {
                    Timestamp = DateTime.Now,
                    MouseMovements = this._currentMouseMovements,
                    KeyPresses = this._currentKeyPresses,
                    ActivityScore = activityScore
                };

                this._activityHistory.Add(data);

                // Keep only last 2 hours of data
                var cutoff = DateTime.Now.AddHours(-2);
                this._activityHistory.RemoveAll(d => d.Timestamp < cutoff);

                // Analyze activity patterns
                this.AnalyzeActivityPatterns(activityScore);

                // Reset counters
                this._currentMouseMovements = 0;
                this._currentKeyPresses = 0;
            }
        }

        private void AnalyzeActivityPatterns(Double currentScore)
        {
            // Track consecutive low/high activity
            if (currentScore < LowActivityThreshold)
            {
                this._consecutiveLowActivityMinutes++;
                this._consecutiveHighActivityMinutes = 0;
            }
            else if (currentScore > HighActivityThreshold)
            {
                this._consecutiveHighActivityMinutes++;
                this._consecutiveLowActivityMinutes = 0;
            }
            else
            {
                this._consecutiveLowActivityMinutes = 0;
                this._consecutiveHighActivityMinutes = 0;
            }

            // Generate smart break suggestions
            var suggestion = this.GenerateBreakSuggestion();
            if (suggestion.ShouldTakeBreak)
            {
                OnBreakSuggested?.Invoke(suggestion);
            }
        }

        private SmartBreakSuggestion GenerateBreakSuggestion()
        {
            var suggestion = new SmartBreakSuggestion
            {
                ShouldTakeBreak = false,
                SuggestedBreakMinutes = 5
            };

            // Check for prolonged inactivity (might indicate distraction)
            var idleSeconds = (DateTime.Now - this._lastActivityTime).TotalSeconds;
            if (idleSeconds > IdleThresholdSeconds && this.IsMonitoring)
            {
                suggestion.ShouldTakeBreak = true;
                suggestion.Reason = "You seem to be away. Consider taking a proper break or refocusing.";
                suggestion.SuggestedBreakMinutes = 5;
                return suggestion;
            }

            // Check for too much continuous high activity
            if (this._consecutiveHighActivityMinutes >= MaxContinuousWorkMinutes)
            {
                suggestion.ShouldTakeBreak = true;
                suggestion.Reason = $"You've been intensely focused for {this._consecutiveHighActivityMinutes} minutes. Take a longer break to avoid burnout.";
                suggestion.SuggestedBreakMinutes = 15;
                this._consecutiveHighActivityMinutes = 0;
                return suggestion;
            }

            // Analyze recent trend - if activity is declining, might be getting tired
            if (this._activityHistory.Count >= 5)
            {
                var recent = this._activityHistory.TakeLast(5).ToList();
                var trend = this.CalculateTrend(recent.Select(a => a.ActivityScore).ToList());

                if (trend < -10) // Significant declining activity
                {
                    suggestion.ShouldTakeBreak = true;
                    suggestion.Reason = "Your focus seems to be declining. A short break might help restore concentration.";
                    suggestion.SuggestedBreakMinutes = 5;
                    return suggestion;
                }
            }

            return suggestion;
        }

        private Double CalculateTrend(List<Double> values)
        {
            if (values.Count < 2)
            {
                return 0;
            }

            // Simple linear regression slope
            var n = values.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumX2 = 0.0;

            for (var i = 0; i < n; i++)
            {
                sumX += i;
                sumY += values[i];
                sumXY += i * values[i];
                sumX2 += i * i;
            }

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            return slope;
        }

        public Int32 GetOptimalWorkDuration()
        {
            // Based on historical data, suggest optimal pomodoro duration
            if (this._activityHistory.Count < 10)
            {
                return 25; // Default
            }

            // Find average duration of high-activity periods
            var highActivityPeriods = new List<Int32>();
            var currentPeriod = 0;

            foreach (var data in this._activityHistory)
            {
                if (data.ActivityScore > 50)
                {
                    currentPeriod++;
                }
                else if (currentPeriod > 0)
                {
                    highActivityPeriods.Add(currentPeriod);
                    currentPeriod = 0;
                }
            }

            if (highActivityPeriods.Count == 0)
            {
                return 25;
            }

            var avgPeriod = highActivityPeriods.Average();

            // Suggest work duration based on natural focus patterns
            if (avgPeriod < 15)
            {
                return 15;
            }

            if (avgPeriod > 45)
            {
                return 45;
            }

            return (Int32)Math.Round(avgPeriod / 5.0) * 5; // Round to nearest 5
        }

        public String GetProductivityInsight()
        {
            if (this._activityHistory.Count < 5)
            {
                return "Keep working to gather activity insights.";
            }

            var avgActivity = this._activityHistory.Average(a => a.ActivityScore);
            var sessionMinutes = (Int32)this.SessionDuration.TotalMinutes;

            if (avgActivity > 70)
            {
                return $"Excellent focus! {sessionMinutes} min session with high productivity.";
            }
            else
            {
                return avgActivity > 40
                    ? $"Good session. {sessionMinutes} min with moderate activity."
                    : $"Low activity detected. Consider eliminating distractions.";
            }
        }

        public void Dispose()
        {
            this.StopMonitoring();
            this._samplingTimer.Dispose();
        }
    }
}