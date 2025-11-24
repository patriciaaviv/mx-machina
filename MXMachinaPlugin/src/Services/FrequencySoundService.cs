namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public enum NoiseType
    {
        WhiteNoise,    // Equal energy at all frequencies - good for sleep and masking sounds
        PinkNoise,     // Softer, more natural - good for study, sleep support, and gentle focus
        BrownNoise,    // Deep, rumbling - good for deep sleep and relaxation
        GreenNoise,    // Mid-range frequencies - naturally calming, good for work and focus
        BlueNoise,     // High frequencies emphasized - lively, good for alertness and concentration
        VioletNoise,   // Very high frequencies - most uplifting, good for focus and masking tinnitus
        GreyNoise,     // Perceptually balanced - smoother white noise, most even-sounding
        SineTone       // Pure tone at specific frequency
    }

    public class FrequencySoundService
    {
        private readonly HttpClient _httpClient;
        private String _llmApiKey;
        private String _llmApiUrl;
        private String _llmModel;
        private Process _currentSoundProcess;
        private Boolean _isPlaying = false;
        private Boolean _isPaused = false;
        private NoiseType _currentNoiseType = NoiseType.PinkNoise;
        private const Int32 FadeDurationSeconds = 2; // 2 seconds fade in/out

        // Playlist state for pause/resume
        private NoisePlaylist _currentPlaylist;
        private Int32 _currentSegmentIndex = 0;
        private DateTime _segmentStartTime;
        private Int32 _segmentElapsedSeconds = 0;

        // Use the hardcoded path to secrets.json in the project root
        private static String SecretsFilePath => Path.Combine(Utils.GetDataDirectory(), "secrets.json");

        public Boolean IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Finds the sox executable path
        /// </summary>
        private String FindSoxExecutable()
        {
            // Try common locations for sox
            var possiblePaths = new[]
            {
                "/usr/local/bin/sox",  // Homebrew default
                "/opt/homebrew/bin/sox", // Homebrew on Apple Silicon
                "/usr/bin/sox",         // System path (unlikely but possible)
                "sox"                   // Fallback to PATH
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = path,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit(1000);
                    if (process.ExitCode == 0)
                    {
                        PluginLog.Verbose($"Found sox at: {path}");
                        return path;
                    }
                }
                catch
                {
                    // Try next path
                    continue;
                }
            }

            return null;
        }

        public FrequencySoundService()
        {
            this._httpClient = new HttpClient();
            this._httpClient.Timeout = TimeSpan.FromSeconds(30); // Set timeout for API calls
            this.LoadSecrets();

            // Hook into timer events
            PomodoroService.Timer.OnStateChanged += this.OnTimerStateChanged;
            PomodoroService.Timer.OnPause += this.OnTimerPaused;
            PomodoroService.Timer.OnWorkBegin += this.OnTimerResumed;
        }

        /// <summary>
        /// Called when timer state changes - handles pause/resume/stop
        /// </summary>
        private void OnTimerStateChanged()
        {
            var timer = PomodoroService.Timer;

            // Stop if timer is completely stopped (not paused)
            if (timer.State == TimerState.Stopped && this._isPlaying)
            {
                PluginLog.Info("Timer stopped, force stopping frequency sound");
                this.StopFrequency(useFadeOut: false); // Immediate stop when timer is stopped
                this._currentPlaylist = null;
                this._currentSegmentIndex = 0;
                this._isPaused = false;
            }
            // Pause if timer is paused
            else if (timer.State == TimerState.WorkPaused && this._isPlaying && !this._isPaused)
            {
                PluginLog.Info("Timer state changed to paused, pausing frequency sound");
                this.PauseFrequency();
            }
            // Resume if timer resumed from pause
            else if (timer.State == TimerState.WorkRunning && this._isPaused && this._currentPlaylist != null)
            {
                PluginLog.Info("Timer state changed to running, resuming frequency sound");
                this.ResumeFrequency();
            }
        }

        /// <summary>
        /// Called when timer is paused - pause the frequency sound
        /// </summary>
        private void OnTimerPaused()
        {
            if (this._isPlaying && !this._isPaused)
            {
                PluginLog.Info("Timer paused event fired, pausing frequency sound");
                this.PauseFrequency();
            }
        }

        /// <summary>
        /// Called when timer resumes - resume the frequency sound
        /// </summary>
        private void OnTimerResumed()
        {
            if (this._isPaused && this._currentPlaylist != null)
            {
                PluginLog.Info("Timer resumed event fired, resuming frequency sound");
                this.ResumeFrequency();
            }
        }

        private void LoadSecrets()
        {
            try
            {
                var secretsPath = SecretsFilePath;
                if (File.Exists(secretsPath))
                {
                    var json = File.ReadAllText(secretsPath);
                    var secrets = JsonSerializer.Deserialize<JsonElement>(json);

                    if (secrets.TryGetProperty("OpenAI", out var openai))
                    {
                        this._llmApiKey = openai.TryGetProperty("ApiKey", out var apiKey)
                            ? apiKey.GetString() : null;
                        // Default to OpenAI API endpoint
                        this._llmApiUrl = "https://api.openai.com/v1/chat/completions";
                        this._llmModel = "gpt-4o-mini"; // Default model
                        PluginLog.Info("OpenAI credentials loaded from secrets.json");
                    }
                    else
                    {
                        PluginLog.Warning("OpenAI configuration not found in secrets.json");
                    }
                }
                else
                {
                    PluginLog.Warning($"secrets.json not found at {secretsPath}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load OpenAI secrets");
            }
        }

        /// <summary>
        /// Gets a recommended noise playlist from LLM based on context
        /// </summary>
        public async Task<NoisePlaylist> GetRecommendedPlaylistAsync()
        {
            if (!this.IsMacOS)
            {
                PluginLog.Warning("Frequency sounds are only available on macOS");
                return null;
            }

            if (String.IsNullOrEmpty(this._llmApiKey))
            {
                PluginLog.Warning("OpenAI API key not configured. Using default playlist.");
                return this.CreateDefaultPlaylist();
            }

            try
            {
                var context = this.GatherContext();
                var playlist = await this.QueryLLMForPlaylistAsync(context);
                return playlist;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to get recommended playlist from LLM");
                return this.CreateDefaultPlaylist();
            }
        }

        /// <summary>
        /// Creates a default playlist when LLM is not available
        /// </summary>
        private NoisePlaylist CreateDefaultPlaylist()
        {
            var timer = PomodoroService.Timer;
            var remainingSeconds = (Int32)timer.RemainingTime.TotalSeconds;
            if (remainingSeconds <= 0)
            {
                remainingSeconds = 1800; // Default 30 minutes
            }

            var playlist = new NoisePlaylist
            {
                TotalDurationSeconds = remainingSeconds
            };

            // Simple default: Pink -> White -> Brown progression
            var segment1 = remainingSeconds / 3;
            var segment2 = remainingSeconds / 3;
            var segment3 = remainingSeconds - segment1 - segment2;

            playlist.Segments.Add(new NoisePlaylistSegment
            {
                NoiseType = NoiseType.PinkNoise,
                DurationSeconds = segment1,
                StartOffsetSeconds = 0,
                Reason = "Soft, natural sound for gentle focus and awakening"
            });

            playlist.Segments.Add(new NoisePlaylistSegment
            {
                NoiseType = NoiseType.WhiteNoise,
                DurationSeconds = segment2,
                StartOffsetSeconds = segment1,
                Reason = "Equal energy masking for consistent background focus"
            });

            playlist.Segments.Add(new NoisePlaylistSegment
            {
                NoiseType = NoiseType.BrownNoise,
                DurationSeconds = segment3,
                StartOffsetSeconds = segment1 + segment2,
                Reason = "Deep, rumbling sound for relaxation and sustained focus"
            });

            return playlist;
        }

        private UserContext GatherContext()
        {
            var timer = PomodoroService.Timer;
            var currentTime = DateTime.Now;
            var remainingTime = timer.RemainingTime;

            // Calculate timer start and end times
            DateTime? timerStartTime = null;
            DateTime? timerEndTime = null;
            if (timer.IsRunning)
            {
                // Calculate start time: endTime - totalDuration
                var totalMinutes = timer.Phase switch
                {
                    PomodoroPhase.Work => timer.WorkMinutes,
                    PomodoroPhase.ShortBreak => timer.ShortBreakMinutes,
                    PomodoroPhase.LongBreak => timer.LongBreakMinutes,
                    _ => 0
                };
                if (totalMinutes > 0)
                {
                    var totalDuration = TimeSpan.FromMinutes(totalMinutes);
                    timerEndTime = currentTime + remainingTime;
                    timerStartTime = timerEndTime.Value - totalDuration;
                }
            }

            var context = new UserContext
            {
                TimeOfDay = currentTime,
                CurrentTime = currentTime,
                TimerState = timer.State.ToString(),
                TimerElapsedMinutes = this.GetTimerElapsedMinutes(),
                TimerRemainingMinutes = (Int32)remainingTime.TotalMinutes,
                CompletedPomodoros = timer.CompletedPomodoros,
                IsTimerRunning = timer.IsRunning,
                TimerStartTime = timerStartTime,
                TimerEndTime = timerEndTime
            };

            // Get active application, all open applications, and screenshot on macOS
            if (this.IsMacOS)
            {
                // Get active (frontmost) application
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "osascript",
                            Arguments = "-e 'tell application \"System Events\" to get name of first application process whose frontmost is true'",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(1000);

                    context.ActiveApplication = output;
                }
                catch (Exception ex)
                {
                    PluginLog.Verbose($"Could not get active application: {ex.Message}");
                    context.ActiveApplication = "Unknown";
                }

                // Get all open applications (visible in dock)
                try
                {
                    var script = "tell application \"System Events\"\n    get name of every application process whose visible is true\nend tell";
                    var tempScript = Path.Combine(Path.GetTempPath(), $"get_open_apps_{Guid.NewGuid()}.scpt");
                    File.WriteAllText(tempScript, script);

                    try
                    {
                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "osascript",
                                Arguments = $"\"{tempScript}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };

                        process.Start();
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit(2000);

                        if (process.ExitCode == 0 && !String.IsNullOrEmpty(output))
                        {
                            // Parse comma-separated list of applications
                            var apps = output.Split(',')
                                .Select(app => app.Trim())
                                .Where(app => !String.IsNullOrEmpty(app))
                                .ToList();
                            context.OpenApplications = apps;
                        }
                    }
                    finally
                    {
                        try
                        {
                            if (File.Exists(tempScript))
                            {
                                File.Delete(tempScript);
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    PluginLog.Verbose($"Could not get open applications: {ex.Message}");
                    context.OpenApplications = new List<String>();
                }

                // Capture screenshot of the current window
                try
                {
                    context.ScreenshotBase64 = this.CaptureWindowScreenshot();
                }
                catch (Exception ex)
                {
                    PluginLog.Verbose($"Could not capture screenshot: {ex.Message}");
                    context.ScreenshotBase64 = null;
                }
            }

            return context;
        }

        /// <summary>
        /// Captures a screenshot of the current active window on macOS
        /// Returns base64-encoded PNG image, or null if capture fails
        /// </summary>
        private String CaptureWindowScreenshot()
        {
            if (!this.IsMacOS)
            {
                return null;
            }

            try
            {
                // Create a temporary file for the screenshot
                var tempFile = Path.Combine(Path.GetTempPath(), $"screenshot_{Guid.NewGuid()}.png");

                // First, get window bounds using AppleScript
                var boundsScript = @"tell application ""System Events""
    set frontApp to first application process whose frontmost is true
    set frontWindow to window 1 of frontApp
    set {x, y} to position of frontWindow
    set {w, h} to size of frontWindow
    return x & "" "" & y & "" "" & w & "" "" & h
end tell";

                var boundsProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "osascript",
                        Arguments = $"-e '{boundsScript}'",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                boundsProcess.Start();
                var boundsOutput = boundsProcess.StandardOutput.ReadToEnd().Trim();
                boundsProcess.WaitForExit(2000);

                Process process;
                if (boundsProcess.ExitCode == 0 && !String.IsNullOrEmpty(boundsOutput))
                {
                    // Parse bounds and capture window region
                    var parts = boundsOutput.Split(' ');
                    if (parts.Length >= 4 &&
                        Int32.TryParse(parts[0], out var x) &&
                        Int32.TryParse(parts[1], out var y) &&
                        Int32.TryParse(parts[2], out var w) &&
                        Int32.TryParse(parts[3], out var h))
                    {
                        // Capture specific region: -R x,y,w,h
                        process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "screencapture",
                                Arguments = $"-x -R {x},{y},{w},{h} -t png \"{tempFile}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };
                    }
                    else
                    {
                        // Fallback to full screen if bounds parsing fails
                        process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "screencapture",
                                Arguments = $"-x -t png \"{tempFile}\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };
                    }
                }
                else
                {
                    // Fallback to full screen capture
                    process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "screencapture",
                            Arguments = $"-x -t png \"{tempFile}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                }

                process.Start();
                process.WaitForExit(2000); // Wait up to 2 seconds

                if (process.ExitCode == 0 && File.Exists(tempFile))
                {
                    // Read the image file and convert to base64
                    var imageBytes = File.ReadAllBytes(tempFile);
                    var base64 = Convert.ToBase64String(imageBytes);

                    // Clean up temp file
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch { }

                    PluginLog.Info($"Screenshot captured successfully ({imageBytes.Length} bytes)");
                    return base64;
                }
                else
                {
                    PluginLog.Verbose($"Screenshot capture failed (exit code: {process.ExitCode})");
                    // Clean up temp file if it exists
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch { }
                    return null;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Verbose($"Exception capturing screenshot: {ex.Message}");
                return null;
            }
        }

        private Int32 GetTimerElapsedMinutes()
        {
            var timer = PomodoroService.Timer;
            if (!timer.IsRunning)
            {
                return 0;
            }

            var totalMinutes = timer.Phase switch
            {
                PomodoroPhase.Work => timer.WorkMinutes,
                PomodoroPhase.ShortBreak => timer.ShortBreakMinutes,
                PomodoroPhase.LongBreak => timer.LongBreakMinutes,
                _ => 0
            };

            var remaining = (Int32)timer.RemainingTime.TotalMinutes;
            return Math.Max(0, totalMinutes - remaining);
        }

        /// <summary>
        /// Queries LLM for a noise playlist based on context
        /// </summary>
        private async Task<NoisePlaylist> QueryLLMForPlaylistAsync(UserContext context)
        {
            var remainingSeconds = context.TimerRemainingMinutes * 60;
            var elapsedSeconds = context.TimerElapsedMinutes * 60;
            var timerInfo = "";
            if (context.TimerStartTime.HasValue && context.TimerEndTime.HasValue)
            {
                timerInfo = $@"
Timer timeline:
- Timer started: {context.TimerStartTime.Value:HH:mm:ss}
- Current time: {context.CurrentTime:HH:mm:ss}
- Timer ends: {context.TimerEndTime.Value:HH:mm:ss}
- Time elapsed: {elapsedSeconds} seconds
- Time remaining: {remainingSeconds} seconds";
            }

            var prompt = $@"Create a noise playlist for ambient focus sounds. Distribute different noise types across the remaining timer duration to optimize focus and productivity.

Context:
- Current time: {context.CurrentTime:HH:mm}
- Time of day: {context.TimeOfDay:HH:mm}
- Timer state: {context.TimerState}
- Timer elapsed: {context.TimerElapsedMinutes} minutes ({elapsedSeconds} seconds)
- Timer remaining: {context.TimerRemainingMinutes} minutes ({remainingSeconds} seconds)
- Completed pomodoros: {context.CompletedPomodoros}
- Timer running: {context.IsTimerRunning}
- Active application: {context.ActiveApplication}
- Open applications: {(context.OpenApplications != null && context.OpenApplications.Any() ? String.Join(", ", context.OpenApplications) : "None")}
{timerInfo}

Available noise types:
- PinkNoise: Soft, natural - good for awakening/gentle focus
- WhiteNoise: Equal energy - good for sleep and masking sounds
- BrownNoise: Deep, rumbling - good for deep sleep and relaxation
- GreenNoise: Mid-range - naturally calming, good for work
- BlueNoise: High frequencies - lively, good for alertness
- VioletNoise: Very high frequencies - most uplifting, sharp focus
- GreyNoise: Perceptually balanced - smoothest, most even

Create a playlist that:
1. Selects only the noise types that best fit the context (you don't need to use all types)
2. Uses segment durations that make sense for the context (they don't need to be equal length)
3. Adapts to the work context (analyze screenshot if provided)
4. Progresses appropriately through the timer duration
5. Uses appropriate noise types for the time of day and work type
6. Ensures smooth transitions between segments

Respond with ONLY a JSON object in this exact format:
{{
  ""segments"": [
    {{
      ""noiseType"": ""PinkNoise"",
      ""durationSeconds"": 300,
      ""startOffsetSeconds"": 0,
      ""reason"": ""Soft awakening sound for early session start""
    }},
    {{
      ""noiseType"": ""WhiteNoise"",
      ""durationSeconds"": 600,
      ""startOffsetSeconds"": 300,
      ""reason"": ""Consistent masking for deep focus phase""
    }}
  ],
  ""totalDurationSeconds"": {remainingSeconds}
}}

The sum of all segment durations should equal totalDurationSeconds. Start offsets should be cumulative. Include a brief 'reason' for each segment explaining why that noise type fits the context.";

            // Build messages array
            var messages = new List<Object>();

            // System message
            messages.Add(new
            {
                role = "system",
                content = "You are a helpful assistant that creates optimized noise playlists for focus and productivity. Analyze the screenshot to understand what the user is working on. You do NOT need to use all available noise types, and segments do NOT need to be equal length. Choose only the noise types and durations that best fit the user's context and work situation. Always respond with valid JSON only, following the exact format specified."
            });

            // User message with optional screenshot
            if (!String.IsNullOrEmpty(context.ScreenshotBase64))
            {
                // Use vision API format with image
                var contentItems = new List<Object>
                {
                    new { type = "text", text = prompt + "\n\nAnalyze the screenshot to understand the user's current work context and adjust the playlist accordingly." },
                    new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:image/png;base64,{context.ScreenshotBase64}"
                        }
                    }
                };
                messages.Add(new
                {
                    role = "user",
                    content = contentItems
                });

                // Use vision-capable model
                this._llmModel = "gpt-4o-mini"; // or "gpt-4o" for better vision
            }
            else
            {
                // Text-only message
                messages.Add(new { role = "user", content = prompt });
            }

            var requestBody = new
            {
                model = this._llmModel,
                messages = messages,
                temperature = 0.7,
                max_tokens = 500 // Increased for playlist response
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, this._llmApiUrl);
            request.Headers.Add("Authorization", $"Bearer {this._llmApiKey}");
            request.Content = content;

            var response = await this._httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                // Try to extract playlist from response
                if (responseData.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message").GetProperty("content").GetString();

                    // Try to parse JSON from the message
                    // LLM might wrap JSON in markdown code blocks, so strip them
                    try
                    {
                        var jsonText = message.Trim();

                        // Remove markdown code blocks if present
                        if (jsonText.StartsWith("```json"))
                        {
                            jsonText = jsonText.Substring(7); // Remove "```json"
                        }
                        else if (jsonText.StartsWith("```"))
                        {
                            jsonText = jsonText.Substring(3); // Remove "```"
                        }
                        if (jsonText.EndsWith("```"))
                        {
                            jsonText = jsonText.Substring(0, jsonText.Length - 3); // Remove trailing "```"
                        }
                        jsonText = jsonText.Trim();
                        var llmResponse = JsonSerializer.Deserialize<JsonElement>(jsonText);
                        return this.ParsePlaylistFromJson(llmResponse, remainingSeconds);
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Warning($"Failed to parse playlist JSON: {ex.Message}");
                        PluginLog.Verbose($"LLM response: {message}");
                    }
                }
            }

            PluginLog.Warning("Failed to parse LLM playlist response, using default playlist");
            return this.CreateDefaultPlaylist();
        }

        /// <summary>
        /// Parses playlist from LLM JSON response
        /// </summary>
        private NoisePlaylist ParsePlaylistFromJson(JsonElement json, Int32 defaultTotalDuration)
        {
            var playlist = new NoisePlaylist
            {
                TotalDurationSeconds = defaultTotalDuration
            };

            try
            {
                if (json.TryGetProperty("totalDurationSeconds", out var totalDuration))
                {
                    playlist.TotalDurationSeconds = totalDuration.GetInt32();
                }

                if (json.TryGetProperty("segments", out var segments) && segments.ValueKind == JsonValueKind.Array)
                {
                    foreach (var segment in segments.EnumerateArray())
                    {
                        var noiseTypeStr = segment.GetProperty("noiseType").GetString();
                        var duration = segment.GetProperty("durationSeconds").GetInt32();
                        var offset = segment.TryGetProperty("startOffsetSeconds", out var off)
                            ? off.GetInt32()
                            : 0;
                        var reason = segment.TryGetProperty("reason", out var reasonProp)
                            ? reasonProp.GetString()
                            : "No reason provided";

                        // Parse noise type
                        if (Enum.TryParse<NoiseType>(noiseTypeStr, out var noiseType))
                        {
                            playlist.Segments.Add(new NoisePlaylistSegment
                            {
                                NoiseType = noiseType,
                                DurationSeconds = duration,
                                StartOffsetSeconds = offset,
                                Reason = reason
                            });
                        }
                        else
                        {
                            PluginLog.Warning($"Unknown noise type: {noiseTypeStr}, using PinkNoise");
                            playlist.Segments.Add(new NoisePlaylistSegment
                            {
                                NoiseType = NoiseType.PinkNoise,
                                DurationSeconds = duration,
                                StartOffsetSeconds = offset,
                                Reason = reason
                            });
                        }
                    }
                }

                // Validate and adjust durations if needed
                var totalSegmentDuration = playlist.Segments.Sum(s => s.DurationSeconds);
                if (totalSegmentDuration != playlist.TotalDurationSeconds)
                {
                    PluginLog.Info($"Adjusting playlist: segments total {totalSegmentDuration}s, expected {playlist.TotalDurationSeconds}s");
                    // Adjust the last segment to match
                    if (playlist.Segments.Count > 0)
                    {
                        var diff = playlist.TotalDurationSeconds - totalSegmentDuration;
                        playlist.Segments[playlist.Segments.Count - 1].DurationSeconds += diff;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Error parsing playlist JSON");
            }

            return playlist;
        }

        /// <summary>
        /// Automatically selects and plays appropriate noise based on Pomodoro timer context
        /// </summary>
        public void PlayContextualNoise(Int32 durationSeconds = 30)
        {
            var timer = PomodoroService.Timer;
            var noiseType = this.DetermineNoiseTypeForContext();
            PluginLog.Info($"Playing contextual noise: {noiseType} based on timer state: {timer.State}");
            this.PlayNoise(noiseType, durationSeconds); // Fade is enabled by default
        }

        /// <summary>
        /// Plays a noise playlist, transitioning between segments with crossfading
        /// Adjusts durations to match current timer state (accounts for LLM processing delay)
        /// </summary>
        public void PlayPlaylist(NoisePlaylist playlist)
        {
            if (playlist == null || playlist.Segments == null || playlist.Segments.Count == 0)
            {
                PluginLog.Warning("Cannot play empty playlist");
                return;
            }

            if (!this.IsMacOS)
            {
                PluginLog.Warning("Frequency sounds are only available on macOS");
                return;
            }

            // Check timer state before starting playback
            var timer = PomodoroService.Timer;
            if (!timer.IsRunning)
            {
                PluginLog.Warning("Timer is not running, cannot play playlist");
                return;
            }

            // Get current remaining time to adjust playlist durations
            var currentRemainingSeconds = (Int32)timer.RemainingTime.TotalSeconds;
            var originalTotalSeconds = playlist.TotalDurationSeconds;

            // Adjust playlist if LLM processing took time (or if timer has less time than expected)
            if (currentRemainingSeconds != originalTotalSeconds)
            {
                var timeDifference = originalTotalSeconds - currentRemainingSeconds;
                if (timeDifference > 0)
                {
                    PluginLog.Info($"Adjusting playlist: LLM processing took ~{timeDifference}s, adjusting from {originalTotalSeconds}s to {currentRemainingSeconds}s");
                }
                else
                {
                    PluginLog.Info($"Adjusting playlist: Timer has more time than expected ({currentRemainingSeconds}s vs {originalTotalSeconds}s), adjusting");
                }

                // Ensure we don't exceed remaining time
                var targetDuration = Math.Min(currentRemainingSeconds, originalTotalSeconds);

                // Scale all segment durations proportionally
                var scaleFactor = targetDuration > 0 ? (Double)targetDuration / originalTotalSeconds : 0.0;
                var adjustedTotal = 0;
                foreach (var segment in playlist.Segments)
                {
                    var originalDuration = segment.DurationSeconds;
                    segment.DurationSeconds = Math.Max(1, (Int32)(originalDuration * scaleFactor));
                    adjustedTotal += segment.DurationSeconds;
                }

                // Adjust last segment to match exactly
                if (playlist.Segments.Count > 0 && adjustedTotal != targetDuration)
                {
                    var lastSegment = playlist.Segments[playlist.Segments.Count - 1];
                    var difference = targetDuration - adjustedTotal;
                    lastSegment.DurationSeconds = Math.Max(1, lastSegment.DurationSeconds + difference);
                }
                playlist.TotalDurationSeconds = targetDuration;
            }

            PluginLog.Info($"Playing playlist with {playlist.Segments.Count} segments, total duration: {playlist.TotalDurationSeconds}s");

            // Stop any currently playing sound
            this.StopFrequency();

            // Store playlist for pause/resume
            this._currentPlaylist = playlist;
            this._currentSegmentIndex = 0;
            this._isPaused = false;
            this._segmentElapsedSeconds = 0;

            // Play segments sequentially in background task
            Task.Run(async () => await this.PlayPlaylistSegmentsAsync());
        }

        /// <summary>
        /// Plays playlist segments with pause/resume support
        /// </summary>
        private async Task PlayPlaylistSegmentsAsync()
        {
            for (Int32 i = this._currentSegmentIndex; i < this._currentPlaylist.Segments.Count; i++)
            {
                // Check if timer is stopped (not paused)
                if (PomodoroService.Timer.State == TimerState.Stopped)
                {
                    PluginLog.Info("Timer stopped during playback, stopping frequency sound");
                    this.StopFrequency(useFadeOut: true);
                    this._currentPlaylist = null;
                    return;
                }

                // Wait if paused
                while (this._isPaused)
                {
                    await Task.Delay(100);
                    // Check if timer was stopped while paused
                    if (PomodoroService.Timer.State == TimerState.Stopped)
                    {
                        PluginLog.Info("Timer stopped while paused, stopping frequency sound");
                        this.StopFrequency(useFadeOut: true);
                        this._currentPlaylist = null;
                        return;
                    }
                }

                this._currentSegmentIndex = i;
                var segment = this._currentPlaylist.Segments[i];
                PluginLog.Info($"Playing segment {i + 1}/{this._currentPlaylist.Segments.Count}: {segment.NoiseType} for {segment.DurationSeconds}s (offset: {segment.StartOffsetSeconds}s)");

                // Show notification with reason for this noise choice
                var reason = !String.IsNullOrEmpty(segment.Reason)
                    ? segment.Reason
                    : $"Playing {segment.NoiseType} for focus";
                var durationMinutes = segment.DurationSeconds / 60;
                var durationText = durationMinutes > 0
                    ? $"{durationMinutes} min"
                    : $"{segment.DurationSeconds}s";
                PomodoroService.Notification.ShowNotification(
                    $"🎵 {segment.NoiseType}",
                    $"{reason} ({durationText})",
                    "Glass"
                );

                // Calculate remaining duration if resuming from pause
                var remainingDuration = segment.DurationSeconds - this._segmentElapsedSeconds;
                if (remainingDuration <= 0)
                {
                    // Segment already completed, move to next
                    this._segmentElapsedSeconds = 0;
                    continue;
                }

                // Crossfade if not first segment, otherwise just play
                if (i > 0 && this._isPlaying && !this._isPaused)
                {
                    this.CrossfadeToNoise(segment.NoiseType, remainingDuration);
                }
                else
                {
                    this.PlayNoise(segment.NoiseType, remainingDuration);
                }

                // Reset elapsed time for this segment
                this._segmentElapsedSeconds = 0;
                this._segmentStartTime = DateTime.Now;

                // Wait for segment to complete (minus fade out time for smooth transition)
                var playDuration = remainingDuration - FadeDurationSeconds;
                if (playDuration > 0)
                {
                    var startTime = DateTime.Now;
                    while ((DateTime.Now - startTime).TotalSeconds < playDuration)
                    {
                        // Check if paused
                        if (this._isPaused)
                        {
                            // Calculate how much of this segment we've played
                            this._segmentElapsedSeconds = (Int32)(DateTime.Now - startTime).TotalSeconds;
                            // Stop current sound
                            this.StopFrequency(useFadeOut: false);
                            // Wait in pause loop
                            while (this._isPaused)
                            {
                                await Task.Delay(100);
                                if (PomodoroService.Timer.State == TimerState.Stopped)
                                {
                                    this._currentPlaylist = null;
                                    return;
                                }
                            }
                            // Resume - break out to restart this segment
                            break;
                        }

                        // Check if timer stopped
                        if (PomodoroService.Timer.State == TimerState.Stopped)
                        {
                            PluginLog.Info("Timer stopped during segment playback, stopping frequency sound");
                            this.StopFrequency(useFadeOut: true);
                            this._currentPlaylist = null;
                            return;
                        }
                        await Task.Delay(100);
                    }
                }
            }

            PluginLog.Info("Playlist playback completed");
            this._currentPlaylist = null;
            this._currentSegmentIndex = 0;
            this._isPaused = false;
        }

        /// <summary>
        /// Checks if noise type should change based on timer progress and crossfades if needed
        /// Call this periodically (e.g., every 30 seconds) to auto-update noise
        /// </summary>
        public void UpdateNoiseIfNeeded()
        {
            if (!this._isPlaying)
            {
                return;
            }

            var recommendedNoise = this.DetermineNoiseTypeForContext();

            // If the recommended noise type is different from current, crossfade
            if (recommendedNoise != this._currentNoiseType)
            {
                var timer = PomodoroService.Timer;
                var duration = 30;
                if (timer.IsRunning && timer.Phase == PomodoroPhase.Work)
                {
                    var remaining = (Int32)timer.RemainingTime.TotalMinutes;
                    duration = Math.Min(remaining * 60, 300);
                }

                PluginLog.Info($"Timer progress changed, crossfading from {this._currentNoiseType} to {recommendedNoise}");
                this.CrossfadeToNoise(recommendedNoise, duration);
            }
        }

        /// <summary>
        /// Determines the best noise type based on current Pomodoro timer context
        /// </summary>
        public NoiseType DetermineNoiseTypeForContext()
        {
            var timer = PomodoroService.Timer;
            if (timer.State == TimerState.Stopped || !timer.IsRunning)
            {
                // No timer running - use gentle pink noise
                return NoiseType.PinkNoise;
            }

            if (timer.Phase == PomodoroPhase.ShortBreak || timer.Phase == PomodoroPhase.LongBreak)
            {
                // Break time - use relaxing brown noise
                return NoiseType.BrownNoise;
            }

            // Work session - determine based on progress
            var totalMinutes = timer.Phase == PomodoroPhase.Work
                ? timer.WorkMinutes
                : timer.ShortBreakMinutes;
            var elapsedMinutes = totalMinutes - (Int32)timer.RemainingTime.TotalMinutes;
            var progressPercent = totalMinutes > 0 ? (elapsedMinutes * 100) / totalMinutes : 0;

            // Early stage (0-20%): Soft, awakening - Pink noise (study support, gentle focus)
            if (progressPercent < 20)
            {
                return NoiseType.PinkNoise;
            }
            // Early-middle stage (20-40%): Naturally calming - Green noise (work, focus, relaxation)
            else if (progressPercent < 40)
            {
                return NoiseType.GreenNoise;
            }
            // Middle stage (40-60%): Consistent background - White noise (masking, general focus)
            else if (progressPercent < 60)
            {
                return NoiseType.WhiteNoise;
            }
            // Middle-late stage (60-80%): Perceptually balanced - Grey noise (smooth, even focus)
            else if (progressPercent < 80)
            {
                return NoiseType.GreyNoise;
            }
            // Late stage (80-95%): Lively concentration - Blue noise (alertness, mental clarity)
            else if (progressPercent < 95)
            {
                return NoiseType.BlueNoise;
            }
            // Final push (95-100%): Maximum focus - Violet noise (most uplifting, sharp focus)
            else
            {
                return NoiseType.VioletNoise;
            }
        }

        /// <summary>
        /// Generates sox command arguments for a specific noise type
        /// </summary>
        private String GenerateNoiseCommand(NoiseType noiseType, Int32 durationSeconds, Boolean useFade, String outputFile)
        {
            String baseNoise = "whitenoise";
            String filters = "";

            switch (noiseType)
            {
                case NoiseType.WhiteNoise:
                    baseNoise = "whitenoise";
                    break;
                case NoiseType.PinkNoise:
                    baseNoise = "pinknoise";
                    break;
                case NoiseType.BrownNoise:
                    baseNoise = "brownnoise";
                    break;
                case NoiseType.GreenNoise:
                    // Green noise: emphasize mid-range (500-2000 Hz), reduce extreme lows/highs
                    baseNoise = "pinknoise";
                    filters = "bandpass 1250 750"; // Center frequency 1250 Hz, width 750 Hz (500-2000 range)
                    break;
                case NoiseType.BlueNoise:
                    // Blue noise: emphasize high frequencies, reduce low frequencies
                    baseNoise = "whitenoise";
                    filters = "highpass 2000";
                    break;
                case NoiseType.VioletNoise:
                    // Violet noise: very high frequencies emphasized, minimal bass
                    baseNoise = "whitenoise";
                    filters = "highpass 4000";
                    break;
                case NoiseType.GreyNoise:
                    // Grey noise: perceptually balanced (A-weighted pink noise approximation)
                    baseNoise = "pinknoise";
                    // Use equalizer to approximate perceptual balance
                    filters = "equalizer 1000 1.0q 0";
                    break;
                default:
                    baseNoise = "pinknoise";
                    break;
            }

            // Build sox command with filters and fade
            String synthPart = $"synth {durationSeconds} {baseNoise}";
            // Fade syntax: fade [type] fade-in-length [stop-position [fade-out-length]]
            // For fade in 2s and fade out 2s: fade t 2 [stop-pos] 2
            // stop-pos should be (duration - fade-out-length) to start fade out at the right time
            String fadePart = useFade && durationSeconds > FadeDurationSeconds * 2
                ? $"fade t {FadeDurationSeconds} {durationSeconds - FadeDurationSeconds} {FadeDurationSeconds}"
                : "";

            if (!String.IsNullOrEmpty(filters))
            {
                return $"-n -r 44100 -b 16 -c 1 \"{outputFile}\" {synthPart} {filters} {fadePart}".Trim();
            }
            else
            {
                return $"-n -r 44100 -b 16 -c 1 \"{outputFile}\" {synthPart} {fadePart}".Trim();
            }
        }

        /// <summary>
        /// Plays a specific noise type with smooth fade in/out
        /// </summary>
        public void PlayNoise(NoiseType noiseType, Int32 durationSeconds = 30, Boolean useFade = true)
        {
            if (!this.IsMacOS)
            {
                PluginLog.Warning("Frequency sounds are only available on macOS");
                return;
            }

            // If already playing the same noise type, don't restart
            if (this._isPlaying && this._currentNoiseType == noiseType)
            {
                PluginLog.Info($"Already playing {noiseType}, skipping restart");
                return;
            }

            // If playing a different noise type, crossfade
            if (this._isPlaying && this._currentNoiseType != noiseType)
            {
                PluginLog.Info($"Crossfading from {this._currentNoiseType} to {noiseType}");
                this.CrossfadeToNoise(noiseType, durationSeconds);
                return;
            }

            // Stop any currently playing sound (if not crossfading)
            this.StopFrequency();

            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"{noiseType.ToString().ToLower()}_{Guid.NewGuid()}.wav");
                String soxArguments = this.GenerateNoiseCommand(noiseType, durationSeconds, useFade, tempFile);

                // Find sox executable - try common locations
                String soxPath = this.FindSoxExecutable();
                if (String.IsNullOrEmpty(soxPath))
                {
                    PluginLog.Error("sox not found. Please install with: brew install sox");
                    return;
                }

                var soxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = soxPath,
                        Arguments = soxArguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                soxProcess.Start();
                // For long files, we need to wait longer - calculate based on duration
                var timeoutMs = Math.Max(30000, durationSeconds * 100); // At least 100ms per second
                var exited = soxProcess.WaitForExit(timeoutMs);
                if (!exited)
                {
                    PluginLog.Warning($"sox process timed out after {timeoutMs}ms, killing...");
                    soxProcess.Kill();
                    soxProcess.WaitForExit(1000);
                }

                if (soxProcess.ExitCode == 0 && File.Exists(tempFile))
                {
                    // Verify file size is reasonable (at least 1KB for a short sound)
                    var fileInfo = new FileInfo(tempFile);
                    var expectedSize = durationSeconds * 44100 * 2; // 44.1kHz, 16-bit (2 bytes), mono
                    PluginLog.Info($"Generated audio file: {tempFile}, size: {fileInfo.Length} bytes (expected ~{expectedSize} bytes for {durationSeconds}s)");
                    if (fileInfo.Length < 1000)
                    {
                        PluginLog.Error($"Audio file is too small ({fileInfo.Length} bytes), sox may have failed silently");
                        return;
                    }

                    // Play the generated file
                    this._currentSoundProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "afplay",
                            Arguments = $"\"{tempFile}\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    try
                    {
                        this._currentSoundProcess.Start();
                        this._isPlaying = true;
                        this._currentNoiseType = noiseType;

                        // Check if process started successfully
                        if (this._currentSoundProcess.HasExited)
                        {
                            var error = this._currentSoundProcess.StandardError.ReadToEnd();
                            PluginLog.Error($"afplay exited immediately. Error: {error}");
                            this._isPlaying = false;
                            this._currentSoundProcess = null;
                            return;
                        }
                        PluginLog.Info($"afplay started successfully (PID: {this._currentSoundProcess.Id})");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, $"Failed to start afplay process");
                        this._isPlaying = false;
                        this._currentSoundProcess = null;
                        return;
                    }

                    // Clean up file after playback
                    Task.Delay((durationSeconds + 1) * 1000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (File.Exists(tempFile))
                            {
                                File.Delete(tempFile);
                            }
                        }
                        catch { }
                        finally
                        {
                            this._isPlaying = false;
                        }
                    });

                    PluginLog.Info($"Playing {noiseType} for {durationSeconds} seconds (fade: {useFade})");
                }
                else
                {
                    var soxError = soxProcess.StandardError.ReadToEnd();
                    PluginLog.Error($"Failed to generate {noiseType}. Exit code: {soxProcess.ExitCode}");
                    if (!String.IsNullOrEmpty(soxError))
                    {
                        PluginLog.Error($"sox error: {soxError}");
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to play {noiseType}");
            }
        }

        /// <summary>
        /// Smoothly crossfades from current noise to new noise type
        /// </summary>
        private void CrossfadeToNoise(NoiseType newNoiseType, Int32 durationSeconds)
        {
            try
            {
                // Start the new noise with fade in
                var newTempFile = Path.Combine(Path.GetTempPath(), $"{newNoiseType.ToString().ToLower()}_crossfade_{Guid.NewGuid()}.wav");
                String soxArguments = this.GenerateNoiseCommand(newNoiseType, durationSeconds, useFade: true, newTempFile);

                // Find sox executable
                String soxPath = this.FindSoxExecutable();
                if (String.IsNullOrEmpty(soxPath))
                {
                    PluginLog.Error("sox not found for crossfade. Please install with: brew install sox");
                    return;
                }

                // Generate new noise with fade in
                var newSoxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = soxPath,
                        Arguments = soxArguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                newSoxProcess.Start();
                // For long files, we need to wait longer
                var timeoutMs = Math.Max(30000, durationSeconds * 100);
                var exited = newSoxProcess.WaitForExit(timeoutMs);
                if (!exited)
                {
                    PluginLog.Warning($"sox process timed out after {timeoutMs}ms, killing...");
                    newSoxProcess.Kill();
                    newSoxProcess.WaitForExit(1000);
                }

                if (newSoxProcess.ExitCode == 0 && File.Exists(newTempFile))
                {
                    // Verify file size
                    var fileInfo = new FileInfo(newTempFile);
                    PluginLog.Info($"Generated crossfade audio file: {newTempFile}, size: {fileInfo.Length} bytes");
                    if (fileInfo.Length < 1000)
                    {
                        PluginLog.Error($"Crossfade audio file is too small ({fileInfo.Length} bytes)");
                        return;
                    }

                    // Fade out current sound over fade duration (let it fade naturally)
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(FadeDurationSeconds * 1000);
                        this.StopFrequency(useFadeOut: false); // Stop after fade period
                    });

                    // Start new sound
                    var newPlayProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/afplay",
                            Arguments = newTempFile, // No quotes needed when not using shell
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    try
                    {
                        newPlayProcess.Start();
                        if (newPlayProcess.HasExited)
                        {
                            var error = newPlayProcess.StandardError.ReadToEnd();
                            PluginLog.Error($"afplay exited immediately during crossfade. Error: {error}");
                            return;
                        }
                        PluginLog.Info($"afplay crossfade started successfully (PID: {newPlayProcess.Id})");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, $"Failed to start afplay for crossfade");
                        return;
                    }
                    this._currentSoundProcess = newPlayProcess;
                    this._currentNoiseType = newNoiseType;

                    // Clean up file after playback
                    Task.Delay((durationSeconds + 1) * 1000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (File.Exists(newTempFile))
                            {
                                File.Delete(newTempFile);
                            }
                        }
                        catch { }
                    });

                    PluginLog.Info($"Crossfaded from {this._currentNoiseType} to {newNoiseType}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to crossfade to {newNoiseType}");
                // Fallback: just stop and play new noise (fade enabled by default)
                this.StopFrequency();
                this.PlayNoise(newNoiseType, durationSeconds);
            }
        }

        public void PlayFrequency(Int32 frequencyHz, Int32 durationSeconds = 30)
        {
            if (!this.IsMacOS)
            {
                PluginLog.Warning("Frequency sounds are only available on macOS");
                return;
            }

            // Stop any currently playing sound
            this.StopFrequency();

            try
            {
                // Generate a simple tone using sox if available, otherwise use afplay with a generated file
                var tempFile = Path.Combine(Path.GetTempPath(), $"frequency_{frequencyHz}Hz_{Guid.NewGuid()}.wav");

                // Try to use sox to generate the tone
                // Find sox executable
                String soxPath = this.FindSoxExecutable();
                if (String.IsNullOrEmpty(soxPath))
                {
                    PluginLog.Error("sox not found for frequency playback. Please install with: brew install sox");
                    this.PlayFrequencyFallback(frequencyHz, durationSeconds);
                    return;
                }

                var soxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = soxPath,
                        Arguments = $"-n -r 44100 -b 16 -c 1 \"{tempFile}\" synth {durationSeconds} sine {frequencyHz}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                soxProcess.Start();
                soxProcess.WaitForExit(2000);

                if (soxProcess.ExitCode == 0 && File.Exists(tempFile))
                {
                    // Play the generated file
                    this._currentSoundProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/afplay",
                            Arguments = tempFile,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    this._currentSoundProcess.Start();
                    this._isPlaying = true;

                    // Clean up file after playback
                    Task.Delay(durationSeconds * 1000 + 1000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (File.Exists(tempFile))
                            {
                                File.Delete(tempFile);
                            }
                        }
                        catch { }
                    });

                    PluginLog.Info($"Playing frequency {frequencyHz} Hz for {durationSeconds} seconds");
                }
                else
                {
                    // Fallback: Use say command with very low volume as a simple tone generator
                    // This is a workaround if sox is not available
                    PluginLog.Warning("sox not available, using alternative method");
                    this.PlayFrequencyFallback(frequencyHz, durationSeconds);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to play frequency {frequencyHz} Hz");
                // Try fallback
                this.PlayFrequencyFallback(frequencyHz, durationSeconds);
            }
        }

        private void PlayFrequencyFallback(Int32 frequencyHz, Int32 durationSeconds)
        {
            // Fallback: Use a simple beep or white noise approximation
            // On macOS, we can use say with a very low volume, or generate white noise
            try
            {
                // Generate white noise using sox if available
                var tempFile = Path.Combine(Path.GetTempPath(), $"whitenoise_{Guid.NewGuid()}.wav");
                // Find sox executable
                String soxPath = this.FindSoxExecutable();
                if (String.IsNullOrEmpty(soxPath))
                {
                    PluginLog.Warning("sox not found for fallback. Please install with: brew install sox");
                    return;
                }

                var soxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = soxPath,
                        Arguments = $"-n -r 44100 -b 16 -c 1 \"{tempFile}\" synth {durationSeconds} whitenoise",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                soxProcess.Start();
                soxProcess.WaitForExit(2000);

                if (soxProcess.ExitCode == 0 && File.Exists(tempFile))
                {
                    this._currentSoundProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/usr/bin/afplay",
                            Arguments = tempFile,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    this._currentSoundProcess.Start();
                    this._isPlaying = true;

                    Task.Delay(durationSeconds * 1000 + 1000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (File.Exists(tempFile))
                            {
                                File.Delete(tempFile);
                            }
                        }
                        catch { }
                    });

                    PluginLog.Info($"Playing white noise (fallback) for {durationSeconds} seconds");
                }
                else
                {
                    PluginLog.Warning("Could not generate audio. Please install sox: brew install sox");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to play frequency using fallback method");
            }
        }

        public void StopFrequency(Boolean useFadeOut = false)
        {
            // Force stop any running processes
            this.ForceStopAllAudio();

            // Reset state (but keep playlist if paused)
            this._currentSoundProcess = null;
            this._isPlaying = false;

            // Only clear playlist if not paused (paused state will resume)
            if (!this._isPaused)
            {
                this._currentPlaylist = null;
                this._currentSegmentIndex = 0;
                this._segmentElapsedSeconds = 0;
            }
        }

        /// <summary>
        /// Pauses the current frequency sound playback
        /// </summary>
        private void PauseFrequency()
        {
            if (this._isPlaying && !this._isPaused)
            {
                PluginLog.Info("Pausing frequency sound...");
                this._isPaused = true;

                // Force stop the current audio process
                if (this._currentSoundProcess != null && !this._currentSoundProcess.HasExited)
                {
                    try
                    {
                        // Kill the process to pause (we'll resume from where we left off)
                        this._currentSoundProcess.Kill();
                        this._currentSoundProcess.WaitForExit(500);
                        PluginLog.Info("Audio process killed for pause");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, $"Error pausing sound: {ex.Message}");
                    }
                    finally
                    {
                        this._currentSoundProcess = null;
                        this._isPlaying = false;
                    }
                }
                else
                {
                    // Process might have already exited, but we still want to mark as paused
                    this._isPlaying = false;
                }

                // Also kill any orphaned afplay processes
                this.ForceStopAllAudio();
                PluginLog.Info($"Frequency sound paused (playlist: {this._currentPlaylist != null}, segment: {this._currentSegmentIndex})");
            }
            else
            {
                PluginLog.Verbose($"Cannot pause: isPlaying={this._isPlaying}, isPaused={this._isPaused}");
            }
        }

        /// <summary>
        /// Resumes the paused frequency sound playback
        /// </summary>
        private void ResumeFrequency()
        {
            if (this._isPaused && this._currentPlaylist != null)
            {
                this._isPaused = false;
                PluginLog.Info($"Resuming frequency sound from segment {this._currentSegmentIndex + 1}");
                // Resume playback by continuing the playlist
                Task.Run(async () => await this.PlayPlaylistSegmentsAsync());
            }
        }

        /// <summary>
        /// Force stops all audio processes (afplay, sox) - emergency cleanup
        /// </summary>
        private void ForceStopAllAudio()
        {
            try
            {
                // Kill current process if it exists
                if (this._currentSoundProcess != null && !this._currentSoundProcess.HasExited)
                {
                    try
                    {
                        this._currentSoundProcess.Kill();
                        this._currentSoundProcess.WaitForExit(500);
                    }
                    catch { }
                }

                // Also kill any orphaned processes (safety measure)
                if (this.IsMacOS)
                {
                    try
                    {
                        var killProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "/usr/bin/pkill",
                                Arguments = "-f \"afplay.*\\.wav\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };
                        killProcess.Start();
                        killProcess.WaitForExit(1000);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Verbose($"Error in ForceStopAllAudio: {ex.Message}");
            }
        }

        public Boolean IsPlaying => this._isPlaying;
    }

    internal class UserContext
    {
        public DateTime TimeOfDay { get; set; }
        public String TimerState { get; set; }
        public Int32 TimerElapsedMinutes { get; set; }
        public Int32 TimerRemainingMinutes { get; set; }
        public Int32 CompletedPomodoros { get; set; }
        public Boolean IsTimerRunning { get; set; }
        public String ActiveApplication { get; set; }
        public List<String> OpenApplications { get; set; } = new List<String>();
        public String ScreenshotBase64 { get; set; }
        public DateTime? TimerStartTime { get; set; }
        public DateTime? TimerEndTime { get; set; }
        public DateTime CurrentTime { get; set; }
    }

    public class NoisePlaylistSegment
    {
        public NoiseType NoiseType { get; set; }
        public Int32 DurationSeconds { get; set; }
        public Int32 StartOffsetSeconds { get; set; } // Offset from current time
        public String Reason { get; set; } // Explanation for why this noise was chosen
    }

    public class NoisePlaylist
    {
        public List<NoisePlaylistSegment> Segments { get; set; } = new List<NoisePlaylistSegment>();
        public Int32 TotalDurationSeconds { get; set; }
    }
}