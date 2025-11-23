using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class TestFrequency
{
    private static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    static void Main(string[] args)
    {
        Console.WriteLine("Frequency Sound Test - macOS");
        Console.WriteLine("============================\n");

        if (!IsMacOS)
        {
            Console.WriteLine("❌ Error: This test only works on macOS");
            Environment.Exit(1);
        }

        // Check if sox is installed
        if (!IsCommandAvailable("sox"))
        {
            Console.WriteLine("❌ Error: sox is not installed");
            Console.WriteLine("Install it with: brew install sox");
            Environment.Exit(1);
        }

        // Test frequencies
        int[] testFrequencies = { 200, 440, 800, 1000, 2000, 5000 };
        int duration = 3; // seconds

        if (args.Length > 0)
        {
            string firstArg = args[0].ToLower();
            
            // Check for noise types
            if (firstArg == "white" || firstArg == "whitenoise")
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestNoise("whitenoise", "White Noise", duration).Wait();
            }
            else if (firstArg == "pink" || firstArg == "pinknoise")
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestNoise("pinknoise", "Pink Noise", duration).Wait();
            }
            else if (firstArg == "brown" || firstArg == "brownnoise")
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestNoise("brownnoise", "Brown Noise", duration).Wait();
            }
            else if (firstArg == "green" || firstArg == "greennoise")
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestNoise("pinknoise", "Green Noise (Calming)", duration, "bandpass 1250 750").Wait();
            }
            else if (firstArg == "blue" || firstArg == "bluenoise")
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestNoise("whitenoise", "Blue Noise (Lively)", duration, "highpass 2000").Wait();
            }
            else if (firstArg == "violet" || firstArg == "violetnoise" || firstArg == "purple")
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestNoise("whitenoise", "Violet Noise (Uplifting)", duration, "highpass 4000").Wait();
            }
            else if (firstArg == "grey" || firstArg == "greynoise" || firstArg == "gray")
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestNoise("pinknoise", "Grey Noise (Balanced)", duration, "equalizer 1000 1.0q 0").Wait();
            }
            else if (firstArg == "noise")
            {
                // Test all noise types with fade (need at least 5 seconds to see fade effect)
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 6;
                Console.WriteLine($"Testing all noise types ({duration}s each with fade)...\n");
                
                TestNoise("pinknoise", "Pink Noise (Soft)", duration).Wait();
                Console.WriteLine();
                System.Threading.Thread.Sleep(500);
                TestNoise("whitenoise", "White Noise (Sleep)", duration).Wait();
                Console.WriteLine();
                System.Threading.Thread.Sleep(500);
                TestNoise("brownnoise", "Brown Noise (Deep)", duration).Wait();
                Console.WriteLine();
                System.Threading.Thread.Sleep(500);
                TestNoise("pinknoise", "Green Noise (Calming)", duration, "bandpass 1250 750").Wait();
                Console.WriteLine();
                System.Threading.Thread.Sleep(500);
                TestNoise("whitenoise", "Blue Noise (Lively)", duration, "highpass 2000").Wait();
                Console.WriteLine();
                System.Threading.Thread.Sleep(500);
                TestNoise("whitenoise", "Violet Noise (Uplifting)", duration, "highpass 4000").Wait();
                Console.WriteLine();
                System.Threading.Thread.Sleep(500);
                TestNoise("pinknoise", "Grey Noise (Balanced)", duration, "equalizer 1000 1.0q 0").Wait();
                Console.WriteLine("\n✅ All noise types tested!");
                Console.WriteLine("Note: Each noise fades in over 2s and fades out over 2s (fade is always enabled)");
            }
            // Single frequency test
            else if (int.TryParse(args[0], out int frequency))
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestSingleFrequency(frequency, duration).Wait();
            }
            else
            {
                Console.WriteLine("Usage: dotnet run [frequency|white|pink|brown|green|blue|violet|grey|noise] [duration]");
                Console.WriteLine("Examples:");
                Console.WriteLine("  dotnet run 440 5        # Play 440 Hz for 5 seconds");
                Console.WriteLine("  dotnet run white 10     # Play white noise (sleep) for 10 seconds");
                Console.WriteLine("  dotnet run pink 5       # Play pink noise (soft) for 5 seconds");
                Console.WriteLine("  dotnet run brown 5      # Play brown noise (deep) for 5 seconds");
                Console.WriteLine("  dotnet run green 5      # Play green noise (calming) for 5 seconds");
                Console.WriteLine("  dotnet run blue 5       # Play blue noise (lively) for 5 seconds");
                Console.WriteLine("  dotnet run violet 5     # Play violet noise (uplifting) for 5 seconds");
                Console.WriteLine("  dotnet run grey 5       # Play grey noise (balanced) for 5 seconds");
                Console.WriteLine("  dotnet run noise        # Test all noise types (6s each with fade)");
                Console.WriteLine("  dotnet run noise 10     # Test all noise types (10s each with fade)");
            }
        }
        else
        {
            // Test all frequencies
            Console.WriteLine($"Testing {testFrequencies.Length} frequencies ({duration}s each)...\n");
            
            foreach (int freq in testFrequencies)
            {
                TestSingleFrequency(freq, duration).Wait();
                Console.WriteLine();
                System.Threading.Thread.Sleep(500); // Small pause between frequencies
            }

            Console.WriteLine("✅ All frequencies tested!");
        }
    }

    static async Task TestSingleFrequency(int frequencyHz, int durationSeconds)
    {
        Console.WriteLine($"Testing {frequencyHz} Hz for {durationSeconds} seconds...");

        var tempFile = Path.Combine(Path.GetTempPath(), $"frequency_test_{frequencyHz}Hz_{Guid.NewGuid()}.wav");

        try
        {
            // Generate tone using sox
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
            var error = await soxProcess.StandardError.ReadToEndAsync();
            soxProcess.WaitForExit(3000);

            if (soxProcess.ExitCode == 0 && File.Exists(tempFile))
            {
                Console.WriteLine($"  ✅ Generated: {tempFile}");
                Console.WriteLine($"  🔊 Playing...");

                // Play the file
                var playProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "afplay",
                        Arguments = $"\"{tempFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                playProcess.Start();
                playProcess.WaitForExit();

                if (playProcess.ExitCode == 0)
                {
                    Console.WriteLine($"  ✅ {frequencyHz} Hz playback completed");
                }
                else
                {
                    Console.WriteLine($"  ❌ Playback failed (exit code: {playProcess.ExitCode})");
                }
            }
            else
            {
                Console.WriteLine($"  ❌ Failed to generate audio (exit code: {soxProcess.ExitCode})");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"  Error: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error: {ex.Message}");
        }
        finally
        {
            // Clean up
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }
    }

    static async Task TestNoise(string noiseType, string displayName, int durationSeconds, string filters = "", bool useFade = true)
    {
        Console.WriteLine($"Testing {displayName} for {durationSeconds} seconds (fade: {useFade})...");

        var tempFile = Path.Combine(Path.GetTempPath(), $"{noiseType}_test_{Guid.NewGuid()}.wav");
        const int fadeDuration = 2; // 2 seconds fade in/out

        try
        {
            // Generate noise using sox with fade and optional filters
            string soxArguments;
            string synthPart = $"synth {durationSeconds} {noiseType}";
            string fadePart = useFade && durationSeconds > fadeDuration * 2
                ? $"fade t 0 {fadeDuration} {fadeDuration}"
                : "";

            if (!string.IsNullOrEmpty(filters))
            {
                soxArguments = $"-n -r 44100 -b 16 -c 1 \"{tempFile}\" {synthPart} {filters} {fadePart}".Trim();
            }
            else if (useFade && durationSeconds > fadeDuration * 2)
            {
                // Add fade in at start and fade out at end
                soxArguments = $"-n -r 44100 -b 16 -c 1 \"{tempFile}\" {synthPart} {fadePart}".Trim();
            }
            else
            {
                // No fade for short durations
                soxArguments = $"-n -r 44100 -b 16 -c 1 \"{tempFile}\" {synthPart}".Trim();
            }

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
            var error = await soxProcess.StandardError.ReadToEndAsync();
            soxProcess.WaitForExit(3000);

            if (soxProcess.ExitCode == 0 && File.Exists(tempFile))
            {
                Console.WriteLine($"  ✅ Generated: {tempFile}");
                Console.WriteLine($"  🔊 Playing {displayName}...");

                // Play the file
                var playProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "afplay",
                        Arguments = $"\"{tempFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                playProcess.Start();
                playProcess.WaitForExit();

                if (playProcess.ExitCode == 0)
                {
                    Console.WriteLine($"  ✅ {displayName} playback completed");
                }
                else
                {
                    Console.WriteLine($"  ❌ Playback failed (exit code: {playProcess.ExitCode})");
                }
            }
            else
            {
                Console.WriteLine($"  ❌ Failed to generate {displayName} (exit code: {soxProcess.ExitCode})");
                if (!string.IsNullOrEmpty(error))
                {
                    Console.WriteLine($"  Error: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Error: {ex.Message}");
        }
        finally
        {
            // Clean up
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch { }
        }
    }

    static bool IsCommandAvailable(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

