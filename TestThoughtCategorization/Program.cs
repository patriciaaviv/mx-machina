using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestThoughtCategorization
{
    /// <summary>
    /// Standalone test program for thought categorization functionality
    /// Run with: dotnet run
    /// 
    /// This test program tests the OpenAI API integration for categorizing thoughts.
    /// Make sure your secrets.json file is in the project root with:
    /// {
    ///   "OpenAI": {
    ///     "ApiKey": "your-api-key-here"
    ///   }
    /// }
    /// </summary>
    class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 Testing Thought Categorization");
            Console.WriteLine("================================\n");

            try
            {
                // Test 1: Check if secrets.json exists and API key is loaded
                Console.WriteLine("Test 1: Checking OpenAI API Key");
                Console.WriteLine("--------------------------------");
                var apiKey = LoadOpenAIApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("❌ OpenAI API Key not found!");
                    Console.WriteLine("   Make sure secrets.json exists in the project root with OpenAI.ApiKey");
                    Console.WriteLine("   Expected location: ../secrets.json");
                    return;
                }
                else
                {
                    Console.WriteLine($"✅ OpenAI API Key loaded successfully");
                    Console.WriteLine($"   Key preview: {apiKey.Substring(0, Math.Min(10, apiKey.Length))}...");
                }
                Console.WriteLine();

                // Test 2: Test categorization with sample thoughts
                Console.WriteLine("Test 2: Testing Thought Categorization");
                Console.WriteLine("--------------------------------------");
                await TestCategorization(apiKey);
                Console.WriteLine();

                Console.WriteLine("✅ All tests completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        static string LoadOpenAIApiKey()
        {
            try
            {
                // Build list of potential paths to check
                var searchPaths = new List<string>();
                
                // 1. Current directory
                var currentDir = Directory.GetCurrentDirectory();
                searchPaths.Add(Path.Combine(currentDir, "secrets.json"));
                
                // 2. Parent directory (project root)
                var parentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));
                searchPaths.Add(Path.Combine(parentDir, "secrets.json"));
                
                // 3. Two levels up (in case we're in a subdirectory)
                var grandparentDir = Path.GetFullPath(Path.Combine(currentDir, "../.."));
                searchPaths.Add(Path.Combine(grandparentDir, "secrets.json"));
                
                // 4. Also check if we can find it by looking for the project root
                // (look for MXMachinaPlugin directory as a marker)
                var dir = new DirectoryInfo(currentDir);
                while (dir != null && dir.Parent != null)
                {
                    var potentialSecrets = Path.Combine(dir.FullName, "secrets.json");
                    if (!searchPaths.Contains(potentialSecrets))
                    {
                        searchPaths.Add(potentialSecrets);
                    }
                    
                    // Check if we're at the project root (has MXMachinaPlugin folder)
                    if (Directory.Exists(Path.Combine(dir.FullName, "MXMachinaPlugin")))
                    {
                        var rootSecrets = Path.Combine(dir.FullName, "secrets.json");
                        if (!searchPaths.Contains(rootSecrets))
                        {
                            searchPaths.Insert(0, rootSecrets); // Prioritize project root
                        }
                    }
                    dir = dir.Parent;
                }

                Console.WriteLine($"   Looking for secrets.json...");
                Console.WriteLine($"   Current directory: {currentDir}");
                
                string secretsPath = null;
                foreach (var path in searchPaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"   Checking: {fullPath}");
                    if (File.Exists(fullPath))
                    {
                        secretsPath = fullPath;
                        Console.WriteLine($"   ✅ Found at: {fullPath}");
                        break;
                    }
                }

                if (secretsPath == null || !File.Exists(secretsPath))
                {
                    Console.WriteLine($"   ❌ secrets.json not found in any of the checked locations");
                    return null;
                }

                // Check if file is empty
                var fileInfo = new FileInfo(secretsPath);
                if (fileInfo.Length == 0)
                {
                    Console.WriteLine($"   ❌ secrets.json exists but is empty (0 bytes)");
                    Console.WriteLine($"   Please add your OpenAI API key to: {secretsPath}");
                    return null;
                }

                var json = File.ReadAllText(secretsPath);
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine($"   ❌ secrets.json is empty or contains only whitespace");
                    return null;
                }

                JsonElement secrets;
                try
                {
                    secrets = JsonSerializer.Deserialize<JsonElement>(json);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"   ❌ secrets.json contains invalid JSON: {ex.Message}");
                    Console.WriteLine($"   File path: {secretsPath}");
                    return null;
                }

                if (secrets.TryGetProperty("OpenAI", out var openai))
                {
                    if (openai.TryGetProperty("ApiKey", out var apiKey))
                    {
                        var key = apiKey.GetString();
                        if (string.IsNullOrEmpty(key))
                        {
                            Console.WriteLine($"   ❌ OpenAI.ApiKey is empty in secrets.json");
                            return null;
                        }
                        if (key == "KEY")
                        {
                            Console.WriteLine($"   ❌ OpenAI.ApiKey is still set to placeholder value 'KEY'");
                            Console.WriteLine($"   Please replace it with your actual API key");
                            return null;
                        }
                        return key;
                    }
                    else
                    {
                        Console.WriteLine($"   ❌ OpenAI.ApiKey property not found in secrets.json");
                        Console.WriteLine($"   Expected structure: {{\"OpenAI\": {{\"ApiKey\": \"your-key\"}}}}");
                    }
                }
                else
                {
                    Console.WriteLine($"   ❌ OpenAI property not found in secrets.json");
                    Console.WriteLine($"   Expected structure: {{\"OpenAI\": {{\"ApiKey\": \"your-key\"}}}}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Error loading API key: {ex.Message}");
            }

            return null;
        }

        static async Task TestCategorization(string apiKey)
        {
            var testThoughts = new[]
            {
                "Need to send email to client about project deadline",
                "Buy milk and eggs at the grocery store",
                "Doctor appointment next week",
                "Remember to call mom this weekend",
                "Urgent: Fix the bug in production ASAP",
                "Meeting with team at 3pm",
                "Pick up dry cleaning"
            };

            Console.WriteLine($"Testing categorization of {testThoughts.Length} thoughts...\n");

            foreach (var thought in testThoughts)
            {
                Console.WriteLine($"📝 Thought: \"{thought}\"");
                
                try
                {
                    var category = await CategorizeThought(apiKey, thought);
                    Console.WriteLine($"   ✅ Categorized as: {category}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ Failed: {ex.Message}");
                }
                
                // Small delay to avoid rate limiting
                await Task.Delay(500);
                Console.WriteLine();
            }
        }

        static async Task<string> CategorizeThought(string apiKey, string thoughtText)
        {
            var prompt = $@"Categorize the following distracting thought into one of these categories: Work, Personal, Urgent, Shopping, Health, Other.

Thought: {thoughtText}

Return ONLY the category name as a single word. Example: ""Work"" or ""Personal""";

            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,
                max_tokens = 50
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"API request failed: {response.StatusCode} - {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (responseData.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message").GetProperty("content").GetString();
                // Clean up the response (remove quotes, whitespace)
                var category = message?.Trim().Trim('"', '\'', ' ').Trim();

                // Validate category
                var validCategories = new[] { "Work", "Personal", "Urgent", "Shopping", "Health", "Other" };
                if (validCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
                {
                    return validCategories.First(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    throw new Exception($"Invalid category returned: {category}");
                }
            }

            throw new Exception("No response from API");
        }
    }
}

