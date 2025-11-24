namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    public class SessionRecord
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Int32 DurationMinutes { get; set; }
        public String SessionType { get; set; } // "Work", "ShortBreak", "LongBreak"
    }

    public class StatisticsData
    {
        public List<SessionRecord> Sessions { get; set; } = new List<SessionRecord>();
        public Int32 BestStreak { get; set; }
    }

    public class StatisticsService
    {
        private static String DataFilePath => Path.Combine(Utils.GetDataDirectory(), "statistics.json");
        private StatisticsData _data;

        public StatisticsService()
        {
            this.LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(DataFilePath))
                {
                    var json = File.ReadAllText(DataFilePath);
                    this._data = JsonSerializer.Deserialize<StatisticsData>(json) ?? new StatisticsData();
                    PluginLog.Info($"Statistics loaded: {this._data.Sessions.Count} sessions");
                }
                else
                {
                    this._data = new StatisticsData();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load statistics");
                this._data = new StatisticsData();
            }
        }

        private void SaveData()
        {
            try
            {
                var json = JsonSerializer.Serialize(this._data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DataFilePath, json);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to save statistics");
            }
        }

        public void RecordSession(PomodoroPhase phase, Int32 durationMinutes)
        {
            var session = new SessionRecord
            {
                StartTime = DateTime.Now.AddMinutes(-durationMinutes),
                EndTime = DateTime.Now,
                DurationMinutes = durationMinutes,
                SessionType = phase.ToString()
            };

            this._data.Sessions.Add(session);

            // Update best streak if current is higher
            var currentStreak = this.GetCurrentStreak();
            if (currentStreak > this._data.BestStreak)
            {
                this._data.BestStreak = currentStreak;
            }

            this.SaveData();
            PluginLog.Info($"Recorded {phase} session: {durationMinutes} min");
        }

        public Int32 GetTotalPomodoros()
        {
            return this._data.Sessions.Count(s => s.SessionType == "Work");
        }

        public Int32 GetTodayPomodoros()
        {
            var today = DateTime.Today;
            return this._data.Sessions.Count(s =>
                s.SessionType == "Work" &&
                s.EndTime.Date == today);
        }

        public Int32 GetThisWeekPomodoros()
        {
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            return this._data.Sessions.Count(s =>
                s.SessionType == "Work" &&
                s.EndTime.Date >= weekStart);
        }

        public Int32 GetTotalFocusMinutes()
        {
            return this._data.Sessions
                .Where(s => s.SessionType == "Work")
                .Sum(s => s.DurationMinutes);
        }

        public String GetTotalFocusTimeFormatted()
        {
            var totalMinutes = this.GetTotalFocusMinutes();
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0)
            {
                return $"{hours}h {minutes}m";
            }
            return $"{minutes}m";
        }

        public Int32 GetCurrentStreak()
        {
            var workSessions = this._data.Sessions
                .Where(s => s.SessionType == "Work")
                .OrderByDescending(s => s.EndTime)
                .ToList();

            if (workSessions.Count == 0)
            {
                return 0;
            }

            var streak = 0;
            var checkDate = DateTime.Today;

            // Check if there's a session today or yesterday to start the streak
            var lastSessionDate = workSessions.First().EndTime.Date;
            if (lastSessionDate < DateTime.Today.AddDays(-1))
            {
                return 0; // Streak broken
            }

            // Count consecutive days
            while (true)
            {
                var hasSessionOnDate = workSessions.Any(s => s.EndTime.Date == checkDate);
                if (hasSessionOnDate)
                {
                    streak++;
                    checkDate = checkDate.AddDays(-1);
                }
                else if (checkDate == DateTime.Today)
                {
                    // Today doesn't have a session yet, check yesterday
                    checkDate = checkDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        public Int32 GetBestStreak()
        {
            return Math.Max(this._data.BestStreak, this.GetCurrentStreak());
        }

        public Double GetAverageSessionsPerDay()
        {
            var workSessions = this._data.Sessions.Where(s => s.SessionType == "Work").ToList();
            if (workSessions.Count == 0)
            {
                return 0;
            }

            var firstSession = workSessions.Min(s => s.EndTime.Date);
            var daysSinceFirst = (DateTime.Today - firstSession).Days + 1;

            return Math.Round((Double)workSessions.Count / daysSinceFirst, 1);
        }

        public String GetStatsSummary()
        {
            return $"Today: {this.GetTodayPomodoros()} | Week: {this.GetThisWeekPomodoros()} | Total: {this.GetTotalPomodoros()}";
        }

        public String GetDetailedStats()
        {
            return $"Pomodoros: {this.GetTotalPomodoros()}\n" +
                   $"Focus Time: {this.GetTotalFocusTimeFormatted()}\n" +
                   $"Streak: {this.GetCurrentStreak()} days\n" +
                   $"Best: {this.GetBestStreak()} days";
        }
    }
}