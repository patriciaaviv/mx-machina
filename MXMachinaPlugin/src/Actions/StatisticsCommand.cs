namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    public class StatisticsCommand : PluginDynamicCommand
    {
        private StatisticsService Statistics => PomodoroService.Statistics;
        private static HttpListener _listener;
        private static Boolean _isRunning;

        public StatisticsCommand()
            : base(displayName: "Statistics", description: "View pomodoro statistics", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            // Open stats dashboard in browser
            Task.Run(() => this.StartDashboardServer());

            // Open browser
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:8081/stats",
                UseShellExecute = true
            });

            PluginLog.Info("Opened statistics dashboard in browser");
        }

        private async Task StartDashboardServer()
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:8081/stats/");
                _listener.Start();
                _isRunning = true;

                PluginLog.Info("Statistics dashboard server started on http://localhost:8081/stats/");

                // Handle one request then stop (or keep running for a while)
                var context = await _listener.GetContextAsync();
                var response = context.Response;

                var html = this.GenerateDashboardHtml();
                var buffer = Encoding.UTF8.GetBytes(html);

                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html; charset=utf-8";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();

                // Keep server running for 30 seconds for refreshes
                await Task.Delay(30000);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Statistics dashboard server error");
            }
            finally
            {
                _isRunning = false;
                _listener?.Stop();
                _listener?.Close();
                _listener = null;
            }
        }

        private String GenerateDashboardHtml()
        {
            var stats = this.Statistics;

            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Pomodoro Statistics ⏱</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            background: linear-gradient(135deg, #0065BD 0%, #003359 100%);
            min-height: 100vh;
            padding: 40px 20px;
        }}
        .container {{
            max-width: 800px;
            margin: 0 auto;
        }}
        h1 {{
            color: white;
            text-align: center;
            margin-bottom: 40px;
            font-size: 2.5em;
        }}
        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 40px;
        }}
        .stat-card {{
            background: white;
            border-radius: 16px;
            padding: 24px;
            text-align: center;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }}
        .stat-value {{
            font-size: 3em;
            font-weight: bold;
            color: #0065BD;
            margin-bottom: 8px;
        }}
        .stat-label {{
            color: #666;
            font-size: 0.9em;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        .streak-card {{
            background: linear-gradient(135deg, #0065BD 0%, #64A0C8 100%);
        }}
        .streak-card .stat-value {{
            color: white;
        }}
        .streak-card .stat-label {{
            color: rgba(255,255,255,0.9);
        }}
        .time-card {{
            background: linear-gradient(135deg, #003359 0%, #0065BD 100%);
        }}
        .time-card .stat-value {{
            color: white;
            font-size: 2em;
        }}
        .time-card .stat-label {{
            color: rgba(255,255,255,0.9);
        }}
        .footer {{
            text-align: center;
            color: rgba(255,255,255,0.7);
            margin-top: 40px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>Pomodoro Statistics</h1>

        <div class='stats-grid'>
            <div class='stat-card'>
                <div class='stat-value'>{stats.GetTodayPomodoros()}</div>
                <div class='stat-label'>Today</div>
            </div>

            <div class='stat-card'>
                <div class='stat-value'>{stats.GetThisWeekPomodoros()}</div>
                <div class='stat-label'>This Week</div>
            </div>

            <div class='stat-card'>
                <div class='stat-value'>{stats.GetTotalPomodoros()}</div>
                <div class='stat-label'>Total Pomodoros</div>
            </div>

            <div class='stat-card time-card'>
                <div class='stat-value'>{stats.GetTotalFocusTimeFormatted()}</div>
                <div class='stat-label'>Total Focus Time</div>
            </div>

            <div class='stat-card streak-card'>
                <div class='stat-value'>{stats.GetCurrentStreak()}</div>
                <div class='stat-label'>Current Streak (Days) 🔥</div>
            </div>

            <div class='stat-card'>
                <div class='stat-value'>{stats.GetBestStreak()}</div>
                <div class='stat-label'>Best Streak (Days)</div>
            </div>
        </div>

        <div class='footer'>
            <p>MX-Machina</p>
        </div>
    </div>
</body>
</html>";
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var stats = this.Statistics;
            return $"Stats";
        }
    }
}
