namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class CalendarEvent
    {
        public String Title { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public Boolean IsFocusTime { get; set; }
    }

    public class GoogleCalendarService
    {
        private readonly HttpClient _httpClient;
        private String _accessToken;
        private String _refreshToken;
        private DateTime _tokenExpiry;

        // OAuth 2.0 configuration - loaded from secrets.json
        private String _clientId;
        private String _clientSecret;
        private const String RedirectUri = "http://localhost:8080/callback";
        private const String Scope = "https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/calendar.events";

        private static String TokenFilePath => Path.Combine(Utils.GetDataDirectory(), "tokens.json");
        private static String SecretsFilePath => Path.Combine(Utils.GetDataDirectory(), "secrets.json");

        public Boolean IsAuthenticated => !String.IsNullOrEmpty(this._accessToken) && DateTime.Now < this._tokenExpiry;

        public GoogleCalendarService()
        {
            this._httpClient = new HttpClient();
            this.LoadSecrets();
            this.LoadTokens();
        }

        private void SaveTokens()
        {
            try
            {
                var tokenData = new
                {
                    access_token = this._accessToken,
                    refresh_token = this._refreshToken,
                    token_expiry = this._tokenExpiry.ToString("o")
                };

                var json = JsonSerializer.Serialize(tokenData);
                File.WriteAllText(TokenFilePath, json);
                PluginLog.Info("OAuth tokens saved to file");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to save OAuth tokens");
            }
        }

        private void LoadTokens()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                {
                    var json = File.ReadAllText(TokenFilePath);
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

                    this._accessToken = tokenData.GetProperty("access_token").GetString();
                    this._refreshToken = tokenData.GetProperty("refresh_token").GetString();
                    this._tokenExpiry = DateTime.Parse(tokenData.GetProperty("token_expiry").GetString());

                    PluginLog.Info($"OAuth tokens loaded. Expiry: {this._tokenExpiry}");

                    // Refresh if expired or about to expire
                    if (DateTime.Now >= this._tokenExpiry && !String.IsNullOrEmpty(this._refreshToken))
                    {
                        PluginLog.Info("Token expired, refreshing...");
                        _ = this.RefreshTokenAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load OAuth tokens");
            }
        }

        private async Task RefreshTokenAsync()
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<String, String>
                {
                    ["client_id"] = this._clientId,
                    ["client_secret"] = this._clientSecret,
                    ["refresh_token"] = this._refreshToken,
                    ["grant_type"] = "refresh_token"
                });

                var response = await this._httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

                    this._accessToken = tokenData.GetProperty("access_token").GetString();
                    var expiresIn = tokenData.GetProperty("expires_in").GetInt32();
                    this._tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                    this.SaveTokens();
                    PluginLog.Info("OAuth token refreshed successfully");
                }
                else
                {
                    PluginLog.Error($"Failed to refresh token: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to refresh OAuth token");
            }
        }

        private void LoadSecrets()
        {
            PluginLog.Info("LoadSecrets: Starting to load secrets");

            try
            {
                // Check the data directory
                var secretsPath = SecretsFilePath;
                PluginLog.Info($"LoadSecrets: Checking {secretsPath}");

                if (File.Exists(secretsPath))
                {
                    var json = File.ReadAllText(secretsPath);
                    var secrets = JsonSerializer.Deserialize<JsonElement>(json);

                    if (secrets.TryGetProperty("GoogleCalendar", out var googleCalendar))
                    {
                        this._clientId = googleCalendar.GetProperty("ClientId").GetString();
                        this._clientSecret = googleCalendar.GetProperty("ClientSecret").GetString();
                        PluginLog.Info($"LoadSecrets: Loaded ClientId successfully");
                        return;
                    }
                    else
                    {
                        PluginLog.Error("LoadSecrets: GoogleCalendar property not found in JSON");
                    }
                }
                else
                {
                    PluginLog.Warning($"LoadSecrets: secrets.json not found at {secretsPath}");
                    PluginLog.Warning($"Please copy your secrets.json to: {Utils.GetDataDirectory()}");
                }

                // Fallback: check assembly directory
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var pluginDirectory = Path.GetDirectoryName(assemblyLocation);
                var searchPaths = new System.Collections.Generic.List<String>();
                searchPaths.Add(Path.Combine(pluginDirectory, "secrets.json"));

                // Also check parent directories (for development)
                var currentDir = pluginDirectory;
                for (var i = 0; i < 5 && currentDir != null; i++)
                {
                    searchPaths.Add(Path.Combine(currentDir, "secrets.json"));
                    currentDir = Path.GetDirectoryName(currentDir);
                }

                // Find first existing file
                String foundPath = null;
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath != null)
                {
                    var json = File.ReadAllText(foundPath);
                    var secrets = JsonSerializer.Deserialize<JsonElement>(json);

                    if (secrets.TryGetProperty("GoogleCalendar", out var googleCalendar))
                    {
                        this._clientId = googleCalendar.GetProperty("ClientId").GetString();
                        this._clientSecret = googleCalendar.GetProperty("ClientSecret").GetString();
                        PluginLog.Info($"Google Calendar credentials loaded from: {foundPath}");
                    }
                }
                else
                {
                    PluginLog.Warning("secrets.json not found. Searched locations:");
                    foreach (var path in searchPaths)
                    {
                        PluginLog.Warning($"  - {path}");
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load Google Calendar secrets");
            }
        }

        public String GetAuthorizationUrl()
        {
            if (String.IsNullOrEmpty(this._clientId))
            {
                PluginLog.Error("Client ID is not configured. Check secrets.json location.");
                return null;
            }

            return $"https://accounts.google.com/o/oauth2/v2/auth?" +
                   $"client_id={this._clientId}&" +
                   $"redirect_uri={Uri.EscapeDataString(RedirectUri)}&" +
                   $"response_type=code&" +
                   $"scope={Uri.EscapeDataString(Scope)}&" +
                   $"access_type=offline&" +
                   $"prompt=consent";
        }

        public async Task<Boolean> ExchangeCodeForTokenAsync(String authCode)
        {
            try
            {
                var content = new FormUrlEncodedContent(new Dictionary<String, String>
                {
                    ["code"] = authCode,
                    ["client_id"] = this._clientId,
                    ["client_secret"] = this._clientSecret,
                    ["redirect_uri"] = RedirectUri,
                    ["grant_type"] = "authorization_code"
                });

                var response = await this._httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

                    this._accessToken = tokenData.GetProperty("access_token").GetString();
                    this._refreshToken = tokenData.GetProperty("refresh_token").GetString();
                    var expiresIn = tokenData.GetProperty("expires_in").GetInt32();
                    this._tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                    this.SaveTokens();
                    return true;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to exchange auth code for token");
            }

            return false;
        }

        public async Task<List<CalendarEvent>> GetUpcomingEventsAsync(Int32 maxResults = 10)
        {
            var events = new List<CalendarEvent>();

            if (!this.IsAuthenticated)
            {
                PluginLog.Warning("Not authenticated with Google Calendar");
                return events;
            }

            try
            {
                var timeMin = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var timeMax = DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-ddTHH:mm:ssZ");

                var url = $"https://www.googleapis.com/calendar/v3/calendars/primary/events?" +
                          $"timeMin={timeMin}&timeMax={timeMax}&maxResults={maxResults}&" +
                          $"singleEvents=true&orderBy=startTime";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {this._accessToken}");

                var response = await this._httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(json);

                    if (data.TryGetProperty("items", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            var calEvent = new CalendarEvent
                            {
                                Title = item.TryGetProperty("summary", out var summary)
                                    ? summary.GetString() : "Untitled"
                            };

                            // Parse start time
                            if (item.TryGetProperty("start", out var start))
                            {
                                if (start.TryGetProperty("dateTime", out var dateTime))
                                {
                                    calEvent.Start = DateTime.Parse(dateTime.GetString());
                                }
                            }

                            // Parse end time
                            if (item.TryGetProperty("end", out var end))
                            {
                                if (end.TryGetProperty("dateTime", out var dateTime))
                                {
                                    calEvent.End = DateTime.Parse(dateTime.GetString());
                                }
                            }

                            // Check if this is a focus time event
                            calEvent.IsFocusTime = calEvent.Title.ToLower().Contains("focus") ||
                                                   calEvent.Title.ToLower().Contains("pomodoro") ||
                                                   calEvent.Title.ToLower().Contains("deep work");

                            events.Add(calEvent);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to fetch calendar events");
            }

            return events;
        }

        public async Task<Boolean> CreateFocusBlockAsync(DateTime start, Int32 durationMinutes, String title = "Focus Time")
        {
            if (!this.IsAuthenticated)
            {
                return false;
            }

            try
            {
                var eventData = new
                {
                    summary = title,
                    description = "Pomodoro focus block created by MX-Machina Plugin",
                    start = new
                    {
                        dateTime = start.ToString("yyyy-MM-ddTHH:mm:ss"),
                        timeZone = TimeZoneInfo.Local.Id
                    },
                    end = new
                    {
                        dateTime = start.AddMinutes(durationMinutes).ToString("yyyy-MM-ddTHH:mm:ss"),
                        timeZone = TimeZoneInfo.Local.Id
                    },
                    colorId = "11" // Red color for focus time
                };

                var json = JsonSerializer.Serialize(eventData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://www.googleapis.com/calendar/v3/calendars/primary/events");
                request.Headers.Add("Authorization", $"Bearer {this._accessToken}");
                request.Content = content;

                var response = await this._httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to create focus block");
                return false;
            }
        }

        public async Task<CalendarEvent> GetNextEventAsync()
        {
            var events = await this.GetUpcomingEventsAsync(1);
            return events.Count > 0 ? events[0] : null;
        }

        public async Task<Boolean> HasConflictAsync(DateTime start, Int32 durationMinutes)
        {
            var events = await this.GetUpcomingEventsAsync(20);
            var end = start.AddMinutes(durationMinutes);

            foreach (var evt in events)
            {
                // Check for overlap
                if (start < evt.End && end > evt.Start)
                {
                    return true;
                }
            }

            return false;
        }
    }
}