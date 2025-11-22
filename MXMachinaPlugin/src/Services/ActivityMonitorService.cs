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
        public TimeSpan SessionDuration => DateTime.Now - _sessionStartTime;
        public Double CurrentActivityLevel { get; private set; }

        public ActivityMonitorService()
        {
            _activityHistory = new List<ActivityData>();
            _samplingTimer = new Timer(60000); // Sample every minute
            _samplingTimer.Elapsed += OnSamplingTimerElapsed;
            _lastActivityTime = DateTime.Now;
            _sessionStartTime = DateTime.Now;
        }

        public void StartMonitoring()
        {
            if (IsMonitoring) return;

            IsMonitoring = true;
            _sessionStartTime = DateTime.Now;
            _samplingTimer.Start();
            PluginLog.Info("Activity monitoring started");
        }

        public void StopMonitoring()
        {
            IsMonitoring = false;
            _samplingTimer.Stop();
            PluginLog.Info("Activity monitoring stopped");
        }

        public void RecordMouseMovement()
        {
            lock (_lock)
            {
                _currentMouseMovements++;
                _lastActivityTime = DateTime.Now;
            }
            OnActivityDetected?.Invoke();
        }

        public void RecordKeyPress()
        {
            lock (_lock)
            {
                _currentKeyPresses++;
                _lastActivityTime = DateTime.Now;
            }
            OnActivityDetected?.Invoke();
        }

        private void OnSamplingTimerElapsed(Object sender, ElapsedEventArgs e)
        {
            lock (_lock)
            {
                // Calculate activity score (0-100)
                var activityScore = Math.Min(100, (_currentMouseMovements + _currentKeyPresses * 2) / 2.0);
                CurrentActivityLevel = activityScore;

                var data = new ActivityData
                {
                    Timestamp = DateTime.Now,
                    MouseMovements = _currentMouseMovements,
                    KeyPresses = _currentKeyPresses,
                    ActivityScore = activityScore
                };

                _activityHistory.Add(data);

                // Keep only last 2 hours of data
                var cutoff = DateTime.Now.AddHours(-2);
                _activityHistory.RemoveAll(d => d.Timestamp < cutoff);

                // Analyze activity patterns
                AnalyzeActivityPatterns(activityScore);

                // Reset counters
                _currentMouseMovements = 0;
                _currentKeyPresses = 0;
            }
        }

        private void AnalyzeActivityPatterns(Double currentScore)
        {
            // Track consecutive low/high activity
            if (currentScore < LowActivityThreshold)
            {
                _consecutiveLowActivityMinutes++;
                _consecutiveHighActivityMinutes = 0;
            }
            else if (currentScore > HighActivityThreshold)
            {
                _consecutiveHighActivityMinutes++;
                _consecutiveLowActivityMinutes = 0;
            }
            else
            {
                _consecutiveLowActivityMinutes = 0;
                _consecutiveHighActivityMinutes = 0;
            }

            // Generate smart break suggestions
            var suggestion = GenerateBreakSuggestion();
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
            var idleSeconds = (DateTime.Now - _lastActivityTime).TotalSeconds;
            if (idleSeconds > IdleThresholdSeconds && IsMonitoring)
            {
                suggestion.ShouldTakeBreak = true;
                suggestion.Reason = "You seem to be away. Consider taking a proper break or refocusing.";
                suggestion.SuggestedBreakMinutes = 5;
                return suggestion;
            }

            // Check for too much continuous high activity
            if (_consecutiveHighActivityMinutes >= MaxContinuousWorkMinutes)
            {
                suggestion.ShouldTakeBreak = true;
                suggestion.Reason = $"You've been intensely focused for {_consecutiveHighActivityMinutes} minutes. Take a longer break to avoid burnout.";
                suggestion.SuggestedBreakMinutes = 15;
                _consecutiveHighActivityMinutes = 0;
                return suggestion;
            }

            // Analyze recent trend - if activity is declining, might be getting tired
            if (_activityHistory.Count >= 5)
            {
                var recent = _activityHistory.TakeLast(5).ToList();
                var trend = CalculateTrend(recent.Select(a => a.ActivityScore).ToList());

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
            if (values.Count < 2) return 0;

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
            if (_activityHistory.Count < 10)
            {
                return 25; // Default
            }

            // Find average duration of high-activity periods
            var highActivityPeriods = new List<Int32>();
            var currentPeriod = 0;

            foreach (var data in _activityHistory)
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
            if (avgPeriod < 15) return 15;
            if (avgPeriod > 45) return 45;
            return (Int32)Math.Round(avgPeriod / 5.0) * 5; // Round to nearest 5
        }

        public String GetProductivityInsight()
        {
            if (_activityHistory.Count < 5)
            {
                return "Keep working to gather activity insights.";
            }

            var avgActivity = _activityHistory.Average(a => a.ActivityScore);
            var sessionMinutes = (Int32)SessionDuration.TotalMinutes;

            if (avgActivity > 70)
            {
                return $"Excellent focus! {sessionMinutes} min session with high productivity.";
            }
            else if (avgActivity > 40)
            {
                return $"Good session. {sessionMinutes} min with moderate activity.";
            }
            else
            {
                return $"Low activity detected. Consider eliminating distractions.";
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            _samplingTimer.Dispose();
        }
    }
}
