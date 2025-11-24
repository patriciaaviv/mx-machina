namespace Loupedeck.MXMachinaPlugin
{
    public static class Utils
    {
        public static String GetDataDirectory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dataDir = Path.Combine(appData, "MXMachinaPlugin");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            return dataDir;
        }

        public static String InitialDirectorySetup()
        {
            var dataDir = GetDataDirectory();
            String secretFilePath = Path.Combine(dataDir, "secrets.json");
            if (!File.Exists(secretFilePath))
            {
                File.WriteAllText(secretFilePath, """
                    {
                      "GoogleCalendar": {
                        "ClientId": "YOUR_CLIENT_ID",
                        "ClientSecret": "YOUR_CLIENT_SECRET"
                      },
                      "OpenAI": {
                        "ApiKey": "YOUR_OPENAI_API_KEY"
                      }
                    }
                    """);
            }
            return dataDir;
        }
    }
}