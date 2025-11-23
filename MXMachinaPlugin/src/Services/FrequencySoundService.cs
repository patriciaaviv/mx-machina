namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Text.RegularExpressions;

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
        private NoiseType _currentNoiseType = NoiseType.PinkNoise;
        private const Int32 FadeDurationSeconds = 2; // 2 seconds fade in/out

        // Use the hardcoded path to secrets.json in the project root
        private static String SecretsFilePath => "/Users/silv/Documents/Programming/mx-machina/secrets.json";

        public Boolean IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public FrequencySoundService()
        {
            this._httpClient = new HttpClient();
            this.LoadSecrets();
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

        public async Task<Int32> GetRecommendedFrequencyAsync()
        {
            if (!this.IsMacOS)
            {
                PluginLog.Warning("Frequency sounds are only available on macOS");
                return 0;
            }

            if (String.IsNullOrEmpty(this._llmApiKey))
            {
                PluginLog.Warning("OpenAI API key not configured. Using default frequency.");
                return 440; // Default A4 note
            }

            try
            {
                var context = this.GatherContext();
                var frequency = await this.QueryLLMForFrequencyAsync(context);
                return frequency;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to get recommended frequency from LLM");
                return 440; // Fallback to A4
            }
        }

        private UserContext GatherContext()
        {
            var context = new UserContext
            {
                TimeOfDay = DateTime.Now,
                TimerState = PomodoroService.Timer.CurrentState.ToString(),
                TimerElapsedMinutes = this.GetTimerElapsedMinutes(),
                TimerRemainingMinutes = (Int32)PomodoroService.Timer.RemainingTime.TotalMinutes,
                CompletedPomodoros = PomodoroService.Timer.CompletedPomodoros,
                IsTimerRunning = PomodoroService.Timer.IsRunning
            };

            // Get active application on macOS
            if (this.IsMacOS)
            {
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
            }

            return context;
        }

        private Int32 GetTimerElapsedMinutes()
        {
            var timer = PomodoroService.Timer;
            if (!timer.IsRunning)
            {
                return 0;
            }

            var totalMinutes = timer.CurrentState switch
            {
                PomodoroState.Work => timer.WorkMinutes,
                PomodoroState.ShortBreak => timer.ShortBreakMinutes,
                PomodoroState.LongBreak => timer.LongBreakMinutes,
                _ => 0
            };

            var remaining = (Int32)timer.RemainingTime.TotalMinutes;
            return Math.Max(0, totalMinutes - remaining);
        }

        private async Task<Int32> QueryLLMForFrequencyAsync(UserContext context)
        {
            var prompt = $@"Based on the following context, recommend a frequency in Hz (between 20-20000) for ambient focus sounds or white noise.

Context:
- Time of day: {context.TimeOfDay:HH:mm}
- Timer state: {context.TimerState}
- Timer elapsed: {context.TimerElapsedMinutes} minutes
- Timer remaining: {context.TimerRemainingMinutes} minutes
- Completed pomodoros: {context.CompletedPomodoros}
- Timer running: {context.IsTimerRunning}
- Active application: {context.ActiveApplication}

Consider:
- Early morning (before 9 AM): Lower frequencies (100-300 Hz) for gentle wake-up
- Work sessions: Mid-range frequencies (400-800 Hz) for focus
- Long sessions (30+ minutes): Slightly lower frequencies to prevent fatigue
- Break time: Higher frequencies (1000-2000 Hz) for energy
- Evening (after 6 PM): Lower frequencies (200-500 Hz) for relaxation

Respond with ONLY a JSON object containing a single 'frequency' field with an integer value between 20 and 20000.";

            var requestBody = new
            {
                model = this._llmModel,
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant that recommends audio frequencies for focus and productivity based on user context. Always respond with valid JSON only." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = 100
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

                // Try to extract frequency from response
                if (responseData.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var message = choices[0].GetProperty("message").GetProperty("content").GetString();
                    
                    // Try to parse JSON from the message
                    try
                    {
                        var llmResponse = JsonSerializer.Deserialize<JsonElement>(message);
                        if (llmResponse.TryGetProperty("frequency", out var freq))
                        {
                            var frequency = freq.GetInt32();
                            return Math.Clamp(frequency, 20, 20000);
                        }
                    }
                    catch
                    {
                        // If not JSON, try to extract number from text
                        var frequencyStr = Regex.Match(message, @"\d+").Value;
                        if (Int32.TryParse(frequencyStr, out var freq))
                        {
                            return Math.Clamp(freq, 20, 20000);
                        }
                    }
                }
            }

            PluginLog.Warning("Failed to parse LLM response, using default frequency");
            return 440;
        }

        /// <summary>
        /// Automatically selects and plays appropriate noise based on Pomodoro timer context
        /// </summary>
        public void PlayContextualNoise(Int32 durationSeconds = 30)
        {
            var timer = PomodoroService.Timer;
            var noiseType = this.DetermineNoiseTypeForContext();
            
            PluginLog.Info($"Playing contextual noise: {noiseType} based on timer state: {timer.CurrentState}");
            this.PlayNoise(noiseType, durationSeconds); // Fade is enabled by default
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
                
                if (timer.IsRunning && timer.CurrentState == PomodoroState.Work)
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
            
            if (timer.CurrentState == PomodoroState.Inactive || !timer.IsRunning)
            {
                // No timer running - use gentle pink noise
                return NoiseType.PinkNoise;
            }

            if (timer.CurrentState == PomodoroState.ShortBreak || timer.CurrentState == PomodoroState.LongBreak)
            {
                // Break time - use relaxing brown noise
                return NoiseType.BrownNoise;
            }

            // Work session - determine based on progress
            var totalMinutes = timer.CurrentState == PomodoroState.Work 
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
            String fadePart = useFade && durationSeconds > FadeDurationSeconds * 2
                ? $"fade t 0 {FadeDurationSeconds} {FadeDurationSeconds}"
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

                var soxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sox",
                        Arguments = soxArguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                soxProcess.Start();
                soxProcess.WaitForExit(3000);

                if (soxProcess.ExitCode == 0 && File.Exists(tempFile))
                {
                    // Play the generated file
                    this._currentSoundProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "afplay",
                            Arguments = $"\"{tempFile}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    this._currentSoundProcess.Start();
                    this._isPlaying = true;
                    this._currentNoiseType = noiseType;

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
                    PluginLog.Error($"Failed to generate {noiseType}. Exit code: {soxProcess.ExitCode}");
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

                // Generate new noise with fade in
                var newSoxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sox",
                        Arguments = soxArguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                newSoxProcess.Start();
                newSoxProcess.WaitForExit(3000);

                if (newSoxProcess.ExitCode == 0 && File.Exists(newTempFile))
                {
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
                            FileName = "afplay",
                            Arguments = $"\"{newTempFile}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    newPlayProcess.Start();
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
                var soxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sox",
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
                            FileName = "afplay",
                            Arguments = $"\"{tempFile}\"",
                            UseShellExecute = false,
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
                var soxProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sox",
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
                            FileName = "afplay",
                            Arguments = $"\"{tempFile}\"",
                            UseShellExecute = false,
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
            if (this._currentSoundProcess != null && !this._currentSoundProcess.HasExited)
            {
                if (useFadeOut)
                {
                    // For fade out, we'll let the process finish naturally (it already has fade out)
                    // Or we can kill it after a short delay to simulate fade
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(FadeDurationSeconds * 1000);
                        try
                        {
                            if (this._currentSoundProcess != null && !this._currentSoundProcess.HasExited)
                            {
                                this._currentSoundProcess.Kill();
                                this._currentSoundProcess.WaitForExit(500);
                            }
                        }
                        catch { }
                        finally
                        {
                            this._currentSoundProcess = null;
                            this._isPlaying = false;
                        }
                    });
                }
                else
                {
                    // Immediate stop
                    try
                    {
                        this._currentSoundProcess.Kill();
                        this._currentSoundProcess.WaitForExit(1000);
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Verbose($"Error stopping sound: {ex.Message}");
                    }
                    finally
                    {
                        this._currentSoundProcess = null;
                        this._isPlaying = false;
                    }
                }
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
    }
}

