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
            // Single frequency test
            if (int.TryParse(args[0], out int frequency))
            {
                duration = args.Length > 1 && int.TryParse(args[1], out int d) ? d : 5;
                TestSingleFrequency(frequency, duration).Wait();
            }
            else
            {
                Console.WriteLine("Usage: dotnet run [frequency] [duration]");
                Console.WriteLine("Example: dotnet run 440 5");
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

