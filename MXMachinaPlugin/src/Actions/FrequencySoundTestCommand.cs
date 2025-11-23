namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;

    public class FrequencySoundTestCommand : PluginDynamicCommand
    {
        private Boolean _isPlaying = false;
        private Process _currentSoundProcess;
        private Int32 _currentFrequency = 440;

        public Boolean IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public FrequencySoundTestCommand()
            : base(displayName: "Frequency Test", description: "Test frequency sound playback (hardcoded)", groupName: "Pomodoro")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.IsMacOS)
            {
                PomodoroService.Notification.ShowNotification(
                    "Frequency Test",
                    "Only available on macOS",
                    "Basso"
                );
                return;
            }

            if (this._isPlaying)
            {
                this.StopFrequency();
                PomodoroService.Notification.ShowNotification(
                    "Frequency Test",
                    "Stopped",
                    "Purr"
                );
                this.ActionImageChanged();
                return;
            }

            // Hardcoded test frequencies - cycle through them
            Int32[] testFrequencies = { 200, 440, 800, 1000, 2000, 5000 };
            
            // Get frequency from action parameter or cycle through
            Int32 frequency;
            if (!String.IsNullOrEmpty(actionParameter) && Int32.TryParse(actionParameter, out var parsedFreq))
            {
                frequency = parsedFreq;
            }
            else
            {
                // Cycle through test frequencies
                var index = Array.IndexOf(testFrequencies, this._currentFrequency);
                index = (index + 1) % testFrequencies.Length;
                frequency = testFrequencies[index];
                this._currentFrequency = frequency;
            }

            var duration = 5; // 5 seconds for testing

            try
            {
                this.PlayFrequency(frequency, duration);

                PomodoroService.Notification.ShowNotification(
                    "Frequency Test",
                    $"Playing {frequency} Hz",
                    "Glass"
                );
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to play frequency {frequency} Hz");
                PomodoroService.Notification.ShowNotification(
                    "Frequency Test",
                    $"Error: {ex.Message}",
                    "Basso"
                );
            }

            this.ActionImageChanged();
        }

        private void PlayFrequency(Int32 frequencyHz, Int32 durationSeconds)
        {
            // Stop any currently playing sound
            this.StopFrequency();

            try
            {
                // Generate a simple tone using sox if available
                var tempFile = Path.Combine(Path.GetTempPath(), $"frequency_test_{frequencyHz}Hz_{Guid.NewGuid()}.wav");
                
                PluginLog.Info($"Generating tone at {frequencyHz} Hz for {durationSeconds} seconds");

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
                var output = soxProcess.StandardOutput.ReadToEnd();
                var error = soxProcess.StandardError.ReadToEnd();
                soxProcess.WaitForExit(3000);

                if (soxProcess.ExitCode == 0 && File.Exists(tempFile))
                {
                    PluginLog.Info($"Generated audio file: {tempFile}");

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

                    PluginLog.Info($"Playing frequency {frequencyHz} Hz for {durationSeconds} seconds");

                    // Clean up file after playback
                    Task.Delay((durationSeconds + 1) * 1000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (File.Exists(tempFile))
                            {
                                File.Delete(tempFile);
                                PluginLog.Info($"Cleaned up temp file: {tempFile}");
                            }
                        }
                        catch (Exception ex)
                        {
                            PluginLog.Verbose($"Could not delete temp file: {ex.Message}");
                        }
                        finally
                        {
                            this._isPlaying = false;
                            this.ActionImageChanged();
                        }
                    });
                }
                else
                {
                    var errorMsg = $"sox failed with exit code {soxProcess.ExitCode}. Error: {error}";
                    PluginLog.Error(errorMsg);
                    
                    if (!String.IsNullOrEmpty(output))
                    {
                        PluginLog.Info($"sox output: {output}");
                    }

                    // Fallback: Try white noise
                    PluginLog.Info("Trying fallback: white noise");
                    this.PlayFrequencyFallback(durationSeconds);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"Failed to play frequency {frequencyHz} Hz");
                throw;
            }
        }

        private void PlayFrequencyFallback(Int32 durationSeconds)
        {
            // Fallback: Generate white noise using sox
            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"whitenoise_test_{Guid.NewGuid()}.wav");
                
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
                soxProcess.WaitForExit(3000);

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

                    PluginLog.Info($"Playing white noise (fallback) for {durationSeconds} seconds");

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
                            this.ActionImageChanged();
                        }
                    });
                }
                else
                {
                    PluginLog.Error("Could not generate audio. Please install sox: brew install sox");
                    PomodoroService.Notification.ShowNotification(
                        "Frequency Test",
                        "Error: sox not found. Install: brew install sox",
                        "Basso"
                    );
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to play frequency using fallback method");
            }
        }

        private void StopFrequency()
        {
            if (this._currentSoundProcess != null && !this._currentSoundProcess.HasExited)
            {
                try
                {
                    this._currentSoundProcess.Kill();
                    this._currentSoundProcess.WaitForExit(1000);
                    PluginLog.Info("Stopped frequency playback");
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

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            if (!this.IsMacOS)
            {
                return "Freq Test\n(macOS only)";
            }

            if (this._isPlaying)
            {
                return $"Test\n{this._currentFrequency} Hz";
            }

            return "Frequency\nTest";
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                if (!this.IsMacOS)
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("macOS\nonly", BitmapColor.White);
                }
                else if (this._isPlaying)
                {
                    bitmapBuilder.Clear(BitmapColor.Green);
                    bitmapBuilder.DrawText($"🔊\n{this._currentFrequency} Hz", BitmapColor.White);
                }
                else
                {
                    bitmapBuilder.Clear(BitmapColor.Orange);
                    bitmapBuilder.DrawText("🔊\nTEST", BitmapColor.White);
                }

                return bitmapBuilder.ToImage();
            }
        }
    }
}

