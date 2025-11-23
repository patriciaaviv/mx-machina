namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Helper class for showing a minimal text input at cursor position on macOS
    /// </summary>
    public static class MacTextInputHelper
    {
        private static TaskCompletionSource<String> _inputCompletionSource;
        private static HttpListener _inputListener;
        private static Boolean _inputServerRunning = false;

        /// <summary>
        /// Shows a beautiful web-based input overlay at cursor position
        /// Returns the entered text, or null if cancelled
        /// </summary>
        public static String ShowInputAtCursor(String placeholder = "Capture thought...")
        {
            try
            {
                // Get cursor position
                var cursorPos = GetCursorPosition();
                
                // Start web server for the input UI
                _inputCompletionSource = new System.Threading.Tasks.TaskCompletionSource<String>();
                StartInputServer(placeholder, cursorPos.Item1, cursorPos.Item2);
                
                // Open browser window at cursor position
                OpenInputWindow(cursorPos.Item1, cursorPos.Item2);
                
                // Wait for input (with timeout)
                var task = _inputCompletionSource.Task;
                if (Task.WaitAny(new[] { task }, TimeSpan.FromSeconds(60)) == 0)
                {
                    return task.Result;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to show text input at cursor");
                return null;
            }
            finally
            {
                StopInputServer();
            }
        }

        /// <summary>
        /// Shows a minimal text input dialog - fallback method
        /// </summary>
        public static String ShowMinimalInputAtCursor(String placeholder = "Capture thought...")
        {
            return ShowInputAtCursor(placeholder);
        }

        /// <summary>
        /// Gets the current cursor position
        /// </summary>
        private static Tuple<Int32, Int32> GetCursorPosition()
        {
            try
            {
                var script = "tell application \"System Events\" to get mouse location";
                var result = RunAppleScript(script);
                if (!String.IsNullOrEmpty(result))
                {
                    var parts = result.Trim().Split(new[] { ", " }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        if (Int32.TryParse(parts[0], out var x) && Int32.TryParse(parts[1], out var y))
                        {
                            return new Tuple<Int32, Int32>(x, y);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to get cursor position");
            }

            // Default to center of screen
            return new Tuple<Int32, Int32>(500, 300);
        }

        /// <summary>
        /// Starts the HTTP server for the input UI
        /// </summary>
        private static void StartInputServer(String placeholder, Int32 x, Int32 y)
        {
            if (_inputServerRunning)
            {
                return;
            }

            try
            {
                _inputListener = new HttpListener();
                _inputListener.Prefixes.Add("http://localhost:8083/input/");
                _inputListener.Start();
                _inputServerRunning = true;

                // Handle requests asynchronously
                Task.Run(async () =>
                {
                    try
                    {
                        while (_inputServerRunning && _inputListener != null && _inputListener.IsListening)
                        {
                            var context = await _inputListener.GetContextAsync();
                            var request = context.Request;
                            var response = context.Response;

                            if (request.HttpMethod == "POST" && request.Url.AbsolutePath.Contains("/submit"))
                            {
                                // Read the submitted text
                                using (var reader = new System.IO.StreamReader(request.InputStream, Encoding.UTF8))
                                {
                                    var body = await reader.ReadToEndAsync();
                                    var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
                                    var text = data.GetProperty("text").GetString();
                                    
                                    _inputCompletionSource?.SetResult(text);
                                }

                                // Send success response
                                var html = "<html><body><script>window.close();</script></body></html>";
                                var buffer = Encoding.UTF8.GetBytes(html);
                                response.ContentLength64 = buffer.Length;
                                response.ContentType = "text/html";
                                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                response.Close();
                            }
                            else if (request.HttpMethod == "POST" && request.Url.AbsolutePath.Contains("/cancel"))
                            {
                                _inputCompletionSource?.SetResult(null);
                                
                                var html = "<html><body><script>window.close();</script></body></html>";
                                var buffer = Encoding.UTF8.GetBytes(html);
                                response.ContentLength64 = buffer.Length;
                                response.ContentType = "text/html";
                                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                response.Close();
                            }
                            else
                            {
                                // Serve the input UI
                                var html = GenerateInputHtml(placeholder, x, y);
                                var buffer = Encoding.UTF8.GetBytes(html);
                                response.ContentLength64 = buffer.Length;
                                response.ContentType = "text/html; charset=utf-8";
                                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                                response.Close();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, "Input server error");
                        _inputCompletionSource?.SetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to start input server");
                _inputServerRunning = false;
            }
        }

        /// <summary>
        /// Stops the input server
        /// </summary>
        private static void StopInputServer()
        {
            _inputServerRunning = false;
            try
            {
                _inputListener?.Stop();
                _inputListener?.Close();
                _inputListener = null;
            }
            catch { }
        }

        /// <summary>
        /// Opens the input window in the default browser
        /// </summary>
        private static void OpenInputWindow(Int32 x, Int32 y)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:8083/input",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to open input window");
            }
        }

        /// <summary>
        /// Generates the HTML for the beautiful input UI
        /// </summary>
        private static String GenerateInputHtml(String placeholder, Int32 x, Int32 y)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Capture Thought</title>
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
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        .input-container {{
            background: white;
            border-radius: 16px;
            padding: 32px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            width: 100%;
            max-width: 500px;
            animation: slideIn 0.3s ease-out;
        }}
        @keyframes slideIn {{
            from {{
                opacity: 0;
                transform: translateY(-20px);
            }}
            to {{
                opacity: 1;
                transform: translateY(0);
            }}
        }}
        h2 {{
            color: #0065BD;
            margin-bottom: 20px;
            font-size: 1.5em;
            font-weight: 600;
        }}
        .input-wrapper {{
            position: relative;
            margin-bottom: 20px;
        }}
        textarea {{
            width: 100%;
            min-height: 100px;
            padding: 16px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            font-size: 16px;
            font-family: inherit;
            resize: none;
            transition: all 0.2s;
            outline: none;
        }}
        textarea:focus {{
            border-color: #0065BD;
            box-shadow: 0 0 0 3px rgba(0, 101, 189, 0.1);
        }}
        .button-group {{
            display: flex;
            gap: 12px;
            justify-content: flex-end;
        }}
        .btn {{
            padding: 12px 24px;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s;
        }}
        .btn-cancel {{
            background: #f5f5f5;
            color: #666;
        }}
        .btn-cancel:hover {{
            background: #e0e0e0;
        }}
        .btn-submit {{
            background: #0065BD;
            color: white;
        }}
        .btn-submit:hover {{
            background: #0052a3;
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(0, 101, 189, 0.3);
        }}
        .btn-submit:active {{
            transform: translateY(0);
        }}
        .hint {{
            color: #999;
            font-size: 0.9em;
            margin-top: 8px;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='input-container'>
        <h2>💭 Capture Thought</h2>
        <div class='input-wrapper'>
            <textarea id='thoughtInput' placeholder='{EscapeHtml(placeholder)}' autofocus></textarea>
        </div>
        <div class='hint'>Press Enter to submit, Esc to cancel</div>
        <div class='button-group'>
            <button class='btn btn-cancel' onclick='cancel()'>Cancel</button>
            <button class='btn btn-submit' onclick='submit()'>Capture</button>
        </div>
    </div>
    <script>
        const input = document.getElementById('thoughtInput');
        
        input.addEventListener('keydown', (e) => {{
            if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {{
                submit();
            }} else if (e.key === 'Escape') {{
                cancel();
            }}
        }});
        
        function submit() {{
            const text = input.value.trim();
            if (text) {{
                fetch('/input/submit', {{
                    method: 'POST',
                    headers: {{ 'Content-Type': 'application/json' }},
                    body: JSON.stringify({{ text: text }})
                }}).then(() => {{
                    setTimeout(() => window.close(), 100);
                }});
            }}
        }}
        
        function cancel() {{
            fetch('/input/cancel', {{
                method: 'POST'
            }}).then(() => {{
                setTimeout(() => window.close(), 100);
            }});
        }}
        
        // Auto-focus
        input.focus();
    </script>
</body>
</html>";
        }

        private static String EscapeHtml(String input)
        {
            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        /// <summary>
        /// Runs AppleScript and returns the result
        /// </summary>
        private static String RunAppleScript(String script)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        Arguments = $"-e \"{EscapeStringForShell(script)}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit(65000); // 60s timeout + buffer

                if (!String.IsNullOrEmpty(error) && !error.Contains("User canceled") && !error.Contains("(-128)"))
                {
                    PluginLog.Warning($"AppleScript error: {error}");
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to run AppleScript");
                return String.Empty;
            }
        }

        private static String EscapeStringForShell(String input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("$", "\\$")
                .Replace("`", "\\`");
        }

        /// <summary>
        /// Plays a "whoosh" sound effect
        /// </summary>
        public static void PlayWhooshSound()
        {
            try
            {
                var script = @"do shell script ""afplay /System/Library/Sounds/Glass.aiff""";
                RunAppleScriptAsync(script);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to play whoosh sound");
            }
        }

        /// <summary>
        /// Runs AppleScript asynchronously
        /// </summary>
        private static void RunAppleScriptAsync(String script)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/osascript",
                        Arguments = "-e",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardInput.WriteLine(script);
                process.StandardInput.Close();
                // Don't wait - let it run asynchronously
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to run AppleScript async");
            }
        }

        private static String EscapeString(String input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", " ");
        }
    }
}

