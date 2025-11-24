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
    }
}