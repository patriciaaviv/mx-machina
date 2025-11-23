namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Command to review and manage captured thoughts after Pomodoro sessions
    /// </summary>
    public class ThoughtReviewCommand : PluginDynamicCommand
    {
        private ThoughtCaptureService ThoughtService => PomodoroService.ThoughtCapture;
        private static HttpListener _listener;
        private static Boolean _isRunning;

        public ThoughtReviewCommand()
            : base(displayName: "Review Thoughts", description: "Review captured thoughts from your session", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            // Open review dashboard in browser
            Task.Run(() => this.StartReviewServer());

            // Open browser
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:8082/review",
                UseShellExecute = true
            });

            PluginLog.Info("Opened thought review dashboard in browser");
        }

        private async Task StartReviewServer()
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:8082/review/");
                _listener.Start();
                _isRunning = true;

                PluginLog.Info("Thought review server started on http://localhost:8082/review/");

                // Handle requests for 5 minutes
                var endTime = DateTime.Now.AddMinutes(5);
                while (DateTime.Now < endTime && _isRunning)
                {
                    var context = await _listener.GetContextAsync();
                    var response = context.Response;
                    var request = context.Request;

                    // Handle mark as reviewed
                    if (request.HttpMethod == "POST" && request.Url.AbsolutePath.Contains("/mark-reviewed"))
                    {
                        var body = await ReadRequestBodyAsync(request);
                        var thoughtIds = System.Text.Json.JsonSerializer.Deserialize<List<String>>(body);
                        this.ThoughtService.MarkAsReviewed(thoughtIds);
                        
                        response.StatusCode = 200;
                        response.Close();
                        continue;
                    }

                    var html = this.GenerateReviewHtml();
                    var buffer = Encoding.UTF8.GetBytes(html);

                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html; charset=utf-8";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Thought review server error");
            }
            finally
            {
                _isRunning = false;
                _listener?.Stop();
                _listener?.Close();
                _listener = null;
            }
        }

        private async Task<String> ReadRequestBodyAsync(HttpListenerRequest request)
        {
            using (var reader = new System.IO.StreamReader(request.InputStream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private String GenerateReviewHtml()
        {
            var categorized = this.ThoughtService.GetCategorizedThoughts();
            var allThoughts = this.ThoughtService.GetUnreviewedThoughts();

            var categoriesHtml = new StringBuilder();
            foreach (var category in categorized)
            {
                categoriesHtml.AppendLine($@"
                    <div class='category-section'>
                        <h2 class='category-title'>{category.Key}</h2>
                        <ul class='thought-list'>");
                
                foreach (var thought in category.Value)
                {
                    categoriesHtml.AppendLine($@"
                            <li class='thought-item' data-id='{thought.Id}'>
                                <span class='thought-text'>{EscapeHtml(thought.Text)}</span>
                                <span class='thought-time'>{thought.CapturedAt:HH:mm}</span>
                            </li>");
                }
                
                categoriesHtml.AppendLine(@"
                        </ul>
                    </div>");
            }

            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Post-Session Review</title>
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
        .category-section {{
            background: white;
            border-radius: 16px;
            padding: 24px;
            margin-bottom: 24px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }}
        .category-title {{
            color: #0065BD;
            margin-bottom: 16px;
            font-size: 1.5em;
            border-bottom: 2px solid #0065BD;
            padding-bottom: 8px;
        }}
        .thought-list {{
            list-style: none;
        }}
        .thought-item {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 12px;
            margin-bottom: 8px;
            background: #f8f9fa;
            border-radius: 8px;
            border-left: 4px solid #0065BD;
            transition: all 0.2s;
        }}
        .thought-item:hover {{
            background: #e9ecef;
            transform: translateX(4px);
        }}
        .thought-text {{
            flex: 1;
            color: #333;
        }}
        .thought-time {{
            color: #666;
            font-size: 0.9em;
            margin-left: 16px;
        }}
        .actions {{
            text-align: center;
            margin-top: 40px;
        }}
        .btn {{
            background: white;
            color: #0065BD;
            border: 2px solid #0065BD;
            padding: 12px 32px;
            border-radius: 8px;
            font-size: 1.1em;
            cursor: pointer;
            transition: all 0.2s;
        }}
        .btn:hover {{
            background: #0065BD;
            color: white;
        }}
        .empty-state {{
            text-align: center;
            color: white;
            padding: 60px 20px;
        }}
        .empty-state h2 {{
            font-size: 2em;
            margin-bottom: 16px;
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
        <h1>Post-Session Review</h1>
        {(allThoughts.Count == 0 ? @"
        <div class='empty-state'>
            <h2>No thoughts to review</h2>
            <p>Great focus! You didn't capture any distracting thoughts.</p>
        </div>" : $@"
        {categoriesHtml}
        <div class='actions'>
            <button class='btn' onclick='markAllReviewed()'>Mark All as Reviewed</button>
        </div>")}
        <div class='footer'>
            <p>MX-Machina</p>
        </div>
    </div>
    <script>
        function markAllReviewed() {{
            var thoughtIds = Array.from(document.querySelectorAll('.thought-item')).map(item => item.dataset.id);
            
            fetch('/review/mark-reviewed', {{
                method: 'POST',
                headers: {{ 'Content-Type': 'application/json' }},
                body: JSON.stringify(thoughtIds)
            }}).then(() => {{
                alert('All thoughts marked as reviewed!');
                location.reload();
            }});
        }}
    </script>
</body>
</html>";
        }

        private String EscapeHtml(String input)
        {
            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var unreviewedCount = this.ThoughtService.GetUnreviewedThoughts().Count;
            if (unreviewedCount > 0)
            {
                return $"Review\n({unreviewedCount})";
            }
            return "Review\nThoughts";
        }
    }
}

