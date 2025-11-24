namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Helper class for showing a minimal text input at cursor position on macOS
    /// </summary>
    public static class MacTextInputHelper
    {
        /// <summary>
        /// Shows a native macOS popup dialog with text input
        /// Returns the entered text, or null if cancelled
        /// </summary>
        public static String ShowInputAtCursor(String placeholder = "Capture thought...")
        {
            try
            {
                // Use simple native dialog with text input field
                var script = "tell application \"System Events\" to activate\n" +
                            "set inputText to text returned of (display dialog \"💭 Capture Thought\" & return & return & \"Quickly jot down your distracting thought:\" default answer \"\" buttons {\"Cancel\", \"Capture ✓\"} default button \"Capture ✓\" with icon note with title \"MX-Machina\" giving up after 60)\n" +
                            "return inputText";

                var result = RunAppleScript(script);
                return String.IsNullOrWhiteSpace(result) ? null : result.Trim();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to show text input dialog");
                return null;
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
