namespace Loupedeck.MXMachinaPlugin
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class ThoughtItem
    {
        public String Id { get; set; } = Guid.NewGuid().ToString();
        public String Text { get; set; }
        public DateTime CapturedAt { get; set; } = DateTime.Now;
        public String Category { get; set; } = "Uncategorized"; // Work, Personal, Urgent, etc.
        public Boolean Reviewed { get; set; } = false;
    }

    public class ThoughtCaptureData
    {
        public List<ThoughtItem> Thoughts { get; set; } = new List<ThoughtItem>();
    }

    public class ThoughtCaptureService
    {
        private static String DataFilePath => Path.Combine(Utils.GetDataDirectory(), "thoughts.json");
        private ThoughtCaptureData _data;
        private readonly HttpClient _httpClient;

        public ThoughtCaptureService()
        {
            this._httpClient = new HttpClient();
            this._httpClient.Timeout = TimeSpan.FromSeconds(30);
            this.LoadData();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(DataFilePath))
                {
                    var json = File.ReadAllText(DataFilePath);
                    this._data = JsonSerializer.Deserialize<ThoughtCaptureData>(json) ?? new ThoughtCaptureData();
                    PluginLog.Info($"Thoughts loaded: {this._data.Thoughts.Count} items");
                }
                else
                {
                    this._data = new ThoughtCaptureData();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load thoughts");
                this._data = new ThoughtCaptureData();
            }
        }

        private void SaveData()
        {
            try
            {
                var json = JsonSerializer.Serialize(this._data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DataFilePath, json);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to save thoughts");
            }
        }

        /// <summary>
        /// Captures a thought and adds it to the queue, then categorizes it automatically
        /// </summary>
        public async Task CaptureThoughtAsync(String thoughtText)
        {
            if (String.IsNullOrWhiteSpace(thoughtText))
            {
                return;
            }

            var thought = new ThoughtItem
            {
                Text = thoughtText.Trim(),
                CapturedAt = DateTime.Now
            };

            this._data.Thoughts.Add(thought);
            this.SaveData();

            PluginLog.Info($"Thought captured: {thoughtText}");

            // Automatically categorize the thought
            try
            {
                await this.CategorizeSingleThoughtAsync(thought);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to categorize thought automatically");
                // Fallback to keyword-based categorization
                this.CategorizeWithKeywords(new List<ThoughtItem> { thought });
            }
        }

        /// <summary>
        /// Captures a thought and adds it to the queue (synchronous version for backward compatibility)
        /// </summary>
        public void CaptureThought(String thoughtText)
        {
            // Fire and forget async categorization
            Task.Run(async () =>
            {
                try
                {
                    await this.CaptureThoughtAsync(thoughtText);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, "Failed to capture thought asynchronously");
                }
            });
        }

        /// <summary>
        /// Gets all unreviewed thoughts from the current session
        /// </summary>
        public List<ThoughtItem> GetUnreviewedThoughts()
        {
            return this._data.Thoughts
                .Where(t => !t.Reviewed)
                .OrderByDescending(t => t.CapturedAt)
                .ToList();
        }

        /// <summary>
        /// Gets all thoughts from today
        /// </summary>
        public List<ThoughtItem> GetTodayThoughts()
        {
            var today = DateTime.Today;
            return this._data.Thoughts
                .Where(t => t.CapturedAt.Date == today)
                .OrderByDescending(t => t.CapturedAt)
                .ToList();
        }

        /// <summary>
        /// Categorizes thoughts using AI (OpenAI API or similar)
        /// </summary>
        public async Task CategorizeThoughtsAsync()
        {
            var unreviewed = this.GetUnreviewedThoughts();
            if (unreviewed.Count == 0)
            {
                PluginLog.Info("No thoughts to categorize");
                return;
            }

            try
            {
                // Group thoughts for batch categorization
                var thoughtsText = String.Join("\n", unreviewed.Select((t, i) => $"{i + 1}. {t.Text}"));

                // Use OpenAI API to categorize
                var categories = await this.CategorizeWithAIAsync(thoughtsText, unreviewed.Count);

                // Apply categories
                for (var i = 0; i < unreviewed.Count && i < categories.Count; i++)
                {
                    unreviewed[i].Category = categories[i];
                }

                this.SaveData();
                PluginLog.Info($"Categorized {unreviewed.Count} thoughts");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to categorize thoughts with AI");
                // Fallback: simple keyword-based categorization
                this.CategorizeWithKeywords(unreviewed);
            }
        }

        /// <summary>
        /// Categorizes thoughts using OpenAI API
        /// </summary>
        private async Task<List<String>> CategorizeWithAIAsync(String thoughtsText, Int32 count)
        {
            // Load API key from secrets
            var apiKey = this.LoadOpenAIApiKey();
            if (String.IsNullOrEmpty(apiKey))
            {
                PluginLog.Warning("OpenAI API key not found, using keyword-based categorization");
                return new List<String>();
            }

            try
            {
                var prompt = $@"Categorize the following {count} distracting thoughts into one of these categories: Work, Personal, Urgent, Shopping, Health, Other.

Thoughts:
{thoughtsText}

Return ONLY a JSON array of category names, one per thought, in order. Example: [""Work"", ""Personal"", ""Shopping""]";

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3,
                    max_tokens = 200
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
                request.Content = content;

                var response = await this._httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

                    if (responseData.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var message = choices[0].GetProperty("message").GetProperty("content").GetString();
                        // Parse JSON array from response
                        var categories = JsonSerializer.Deserialize<List<String>>(message);
                        return categories ?? new List<String>();
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "OpenAI API call failed");
            }

            return new List<String>();
        }

        /// <summary>
        /// Categorizes a single thought using AI
        /// </summary>
        private async Task CategorizeSingleThoughtAsync(ThoughtItem thought)
        {
            var apiKey = this.LoadOpenAIApiKey();
            if (String.IsNullOrEmpty(apiKey))
            {
                PluginLog.Warning("OpenAI API key not found, using keyword-based categorization");
                this.CategorizeWithKeywords(new List<ThoughtItem> { thought });
                return;
            }

            try
            {
                var prompt = $@"Categorize the following distracting thought into one of these categories: Work, Personal, Urgent, Shopping, Health, Other.

Thought: {thought.Text}

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

                var response = await this._httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
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
                            thought.Category = validCategories.First(c => String.Equals(c, category, StringComparison.OrdinalIgnoreCase));
                            this.SaveData();
                            PluginLog.Info($"Thought categorized as: {thought.Category}");
                            return;
                        }
                    }
                }

                PluginLog.Warning("Failed to get valid category from OpenAI, using keyword-based categorization");
                this.CategorizeWithKeywords(new List<ThoughtItem> { thought });
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "OpenAI API call failed for single thought categorization");
                this.CategorizeWithKeywords(new List<ThoughtItem> { thought });
            }
        }

        /// <summary>
        /// Fallback categorization using keywords
        /// </summary>
        private void CategorizeWithKeywords(List<ThoughtItem> thoughts)
        {
            foreach (var thought in thoughts)
            {
                var text = thought.Text.ToLower();

                if (text.Contains("email") || text.Contains("meeting") || text.Contains("deadline") ||
                    text.Contains("project") || text.Contains("client") || text.Contains("work"))
                {
                    thought.Category = "Work";
                }
                else if (text.Contains("buy") || text.Contains("milk") || text.Contains("grocery") ||
                         text.Contains("shopping") || text.Contains("store"))
                {
                    thought.Category = "Shopping";
                }
                else if (text.Contains("urgent") || text.Contains("asap") || text.Contains("important"))
                {
                    thought.Category = "Urgent";
                }
                else if (text.Contains("doctor") || text.Contains("appointment") || text.Contains("health"))
                {
                    thought.Category = "Health";
                }
                else
                {
                    thought.Category = "Personal";
                }
            }

            this.SaveData();
        }

        /// <summary>
        /// Loads OpenAI API key from secrets.json (searches multiple locations including project root)
        /// </summary>
        private String LoadOpenAIApiKey()
        {
            try
            {
                var searchPaths = new List<String>();

                // 1. First check the data directory (for deployed plugin)
                var dataDirPath = Path.Combine(Utils.GetDataDirectory(), "secrets.json");
                searchPaths.Add(dataDirPath);

                // 2. Check assembly directory and parent directories (for development)
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var pluginDirectory = Path.GetDirectoryName(assemblyLocation);
                if (!String.IsNullOrEmpty(pluginDirectory))
                {
                    searchPaths.Add(Path.Combine(pluginDirectory, "secrets.json"));

                    // Walk up parent directories looking for project root
                    var currentDir = new DirectoryInfo(pluginDirectory);
                    while (currentDir != null && currentDir.Parent != null)
                    {
                        var potentialSecrets = Path.Combine(currentDir.FullName, "secrets.json");
                        if (!searchPaths.Contains(potentialSecrets))
                        {
                            searchPaths.Add(potentialSecrets);
                        }

                        // Check if we're at the project root (has MXMachinaPlugin folder)
                        var mxMachinaPluginPath = Path.Combine(currentDir.FullName, "MXMachinaPlugin");
                        if (Directory.Exists(mxMachinaPluginPath))
                        {
                            var rootSecrets = Path.Combine(currentDir.FullName, "secrets.json");
                            if (!searchPaths.Contains(rootSecrets))
                            {
                                // Prioritize project root by adding it early
                                searchPaths.Insert(1, rootSecrets);
                            }
                        }

                        currentDir = currentDir.Parent;
                    }
                }

                // Find first existing file with valid API key
                String foundPath = null;
                foreach (var path in searchPaths)
                {
                    if (File.Exists(path))
                    {
                        try
                        {
                            var json = File.ReadAllText(path);
                            if (String.IsNullOrWhiteSpace(json))
                            {
                                continue;
                            }

                            var secrets = JsonSerializer.Deserialize<JsonElement>(json);

                            if (secrets.TryGetProperty("OpenAI", out var openai))
                            {
                                if (openai.TryGetProperty("ApiKey", out var apiKey))
                                {
                                    var key = apiKey.GetString();
                                    if (!String.IsNullOrEmpty(key) && key != "KEY" && key != "your-actual-openai-api-key-here")
                                    {
                                        foundPath = path;
                                        PluginLog.Info($"OpenAI API key loaded from: {foundPath}");
                                        return key;
                                    }
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            PluginLog.Warning($"Invalid JSON in {path}: {ex.Message}");
                            continue;
                        }
                        catch (Exception ex)
                        {
                            PluginLog.Warning($"Error reading {path}: {ex.Message}");
                            continue;
                        }
                    }
                }

                if (foundPath == null)
                {
                    PluginLog.Warning("secrets.json not found or OpenAI.ApiKey is invalid. Searched locations:");
                    foreach (var path in searchPaths.Take(10)) // Limit output to first 10 paths
                    {
                        PluginLog.Warning($"  - {path}");
                    }
                    if (searchPaths.Count > 10)
                    {
                        PluginLog.Warning($"  ... and {searchPaths.Count - 10} more locations");
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to load OpenAI API key");
            }

            return null;
        }

        /// <summary>
        /// Marks thoughts as reviewed after session review
        /// </summary>
        public void MarkAsReviewed(List<String> thoughtIds)
        {
            foreach (var id in thoughtIds)
            {
                var thought = this._data.Thoughts.FirstOrDefault(t => t.Id == id);
                if (thought != null)
                {
                    thought.Reviewed = true;
                }
            }
            this.SaveData();
        }

        /// <summary>
        /// Deletes thoughts permanently (removes them from storage)
        /// </summary>
        public void DeleteThoughts(List<String> thoughtIds)
        {
            var removedCount = 0;
            foreach (var id in thoughtIds)
            {
                var thought = this._data.Thoughts.FirstOrDefault(t => t.Id == id);
                if (thought != null)
                {
                    this._data.Thoughts.Remove(thought);
                    removedCount++;
                }
            }
            this.SaveData();
            PluginLog.Info($"Deleted {removedCount} thought(s) permanently");
        }

        /// <summary>
        /// Deletes a single thought permanently
        /// </summary>
        public void DeleteThought(String thoughtId)
        {
            var thought = this._data.Thoughts.FirstOrDefault(t => t.Id == thoughtId);
            if (thought != null)
            {
                this._data.Thoughts.Remove(thought);
                this.SaveData();
                PluginLog.Info($"Deleted thought: {thought.Text}");
            }
        }

        /// <summary>
        /// Gets categorized thoughts grouped by category
        /// </summary>
        public Dictionary<String, List<ThoughtItem>> GetCategorizedThoughts()
        {
            var unreviewed = this.GetUnreviewedThoughts();
            return unreviewed
                .GroupBy(t => t.Category)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Clears all thoughts (for testing/reset)
        /// </summary>
        public void ClearAllThoughts()
        {
            this._data.Thoughts.Clear();
            this.SaveData();
            PluginLog.Info("All thoughts cleared");
        }
    }
}
