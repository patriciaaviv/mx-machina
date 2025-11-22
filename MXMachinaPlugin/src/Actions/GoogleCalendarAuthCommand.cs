namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;

    public class GoogleCalendarAuthCommand : PluginDynamicCommand
    {
        private static HttpListener _listener;
        private static Boolean _isListening;

        public GoogleCalendarAuthCommand()
            : base(displayName: "Calendar Auth", description: "Authenticate with Google Calendar", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            PluginLog.Info("GoogleCalendarAuthCommand: RunCommand started");

            try
            {
                var calendar = PomodoroService.Calendar;
                PluginLog.Info("GoogleCalendarAuthCommand: Got calendar service");

                if (calendar.IsAuthenticated)
                {
                    PomodoroService.Notification.ShowNotification(
                        "✅ Already Authenticated",
                        "Google Calendar is already connected.",
                        "Glass"
                    );
                    return;
                }

                // Start the OAuth flow
                Task.Run(async () =>
                {
                    try
                    {
                        // Get authorization URL and open in browser
                        var authUrl = calendar.GetAuthorizationUrl();
                        PluginLog.Info($"GoogleCalendarAuthCommand: Auth URL = {authUrl ?? "NULL"}");

                    if (String.IsNullOrEmpty(authUrl))
                    {
                        PomodoroService.Notification.ShowNotification(
                            "❌ Missing Credentials",
                            "secrets.json not found or invalid.",
                            "Basso"
                        );
                        return;
                    }

                    // Open browser on macOS
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = authUrl,
                        UseShellExecute = true
                    });

                    PomodoroService.Notification.ShowNotification(
                        "🔐 Authorization Required",
                        "Complete sign-in in your browser...",
                        "Purr"
                    );

                    // Start local callback server
                    var authCode = await this.WaitForCallbackAsync();

                    if (!String.IsNullOrEmpty(authCode))
                    {
                        // Exchange code for token
                        var success = await calendar.ExchangeCodeForTokenAsync(authCode);

                        if (success)
                        {
                            PomodoroService.Notification.ShowNotification(
                                "✅ Connected!",
                                "Google Calendar is now linked.",
                                "Hero"
                            );
                            PluginLog.Info("Google Calendar authentication successful");
                        }
                        else
                        {
                            PomodoroService.Notification.ShowNotification(
                                "❌ Auth Failed",
                                "Could not complete authentication.",
                                "Basso"
                            );
                            PluginLog.Error("Failed to exchange auth code for token");
                        }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Google Calendar authentication error");
                    PomodoroService.Notification.ShowNotification(
                        "❌ Error",
                        "Authentication failed. Check logs.",
                        "Basso"
                    );
                }
            });
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "GoogleCalendarAuthCommand: Failed to start auth flow");
                PomodoroService.Notification.ShowNotification(
                    "❌ Error",
                    "Failed to start authentication.",
                    "Basso"
                );
            }
        }

        private async Task<String> WaitForCallbackAsync()
        {
            if (_isListening)
            {
                PluginLog.Warning("Callback server already running");
                return null;
            }

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:8080/callback/");
                _listener.Start();
                _isListening = true;

                PluginLog.Info("Waiting for OAuth callback on http://localhost:8080/callback/");

                // Wait for the callback (with timeout)
                var contextTask = _listener.GetContextAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));

                var completedTask = await Task.WhenAny(contextTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    PluginLog.Warning("OAuth callback timed out");
                    return null;
                }

                var context = await contextTask;
                var request = context.Request;
                var response = context.Response;

                // Extract auth code from query string
                var authCode = request.QueryString["code"];
                var error = request.QueryString["error"];

                // Send response to browser
                String responseHtml;
                if (!String.IsNullOrEmpty(authCode))
                {
                    responseHtml = @"
                        <html>
                        <head><title>Success</title></head>
                        <body style='font-family: -apple-system, sans-serif; text-align: center; padding: 50px;'>
                            <h1>Authentication Successful!</h1>
                            <p>You can close this window now.</p>
                        </body>
                        </html>";
                }
                else
                {
                    responseHtml = $@"
                        <html>
                        <head><title>Error</title></head>
                        <body style='font-family: -apple-system, sans-serif; text-align: center; padding: 50px;'>
                            <h1>Authentication Failed</h1>
                            <p>Error: {error ?? "Unknown error"}</p>
                        </body>
                        </html>";
                }

                var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
                response.ContentLength64 = buffer.Length;
                response.ContentType = "text/html";
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();

                return authCode;
            }
            finally
            {
                _isListening = false;
                _listener?.Stop();
                _listener?.Close();
                _listener = null;
            }
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            var calendar = PomodoroService.Calendar;
            return calendar.IsAuthenticated ? "Calendar\n✓ Connected" : "Calendar\nAuth";
        }
    }
}