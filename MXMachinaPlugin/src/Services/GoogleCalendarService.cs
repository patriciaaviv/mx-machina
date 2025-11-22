namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
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

        // OAuth 2.0 configuration - users will need to set these
        private const String ClientId = "YOUR_CLIENT_ID";
        private const String ClientSecret = "YOUR_CLIENT_SECRET";
        private const String RedirectUri = "http://localhost:8080/callback";
        private const String Scope = "https://www.googleapis.com/auth/calendar.readonly https://www.googleapis.com/auth/calendar.events";

        public Boolean IsAuthenticated => !String.IsNullOrEmpty(_accessToken) && DateTime.Now < _tokenExpiry;

        public GoogleCalendarService()
        {
            _httpClient = new HttpClient();
        }

        public String GetAuthorizationUrl()
        {
            return $"https://accounts.google.com/o/oauth2/v2/auth?" +
                   $"client_id={ClientId}&" +
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
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["redirect_uri"] = RedirectUri,
                    ["grant_type"] = "authorization_code"
                });

                var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

                    _accessToken = tokenData.GetProperty("access_token").GetString();
                    _refreshToken = tokenData.GetProperty("refresh_token").GetString();
                    var expiresIn = tokenData.GetProperty("expires_in").GetInt32();
                    _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

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

            if (!IsAuthenticated)
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
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");

                var response = await _httpClient.SendAsync(request);
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
            if (!IsAuthenticated)
            {
                return false;
            }

            try
            {
                var eventData = new
                {
                    summary = title,
                    description = "Pomodoro focus block created by MX-Machina",
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
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
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
            var events = await GetUpcomingEventsAsync(1);
            return events.Count > 0 ? events[0] : null;
        }

        public async Task<Boolean> HasConflictAsync(DateTime start, Int32 durationMinutes)
        {
            var events = await GetUpcomingEventsAsync(20);
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
