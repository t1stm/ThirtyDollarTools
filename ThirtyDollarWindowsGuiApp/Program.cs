using System.Text.Json;
using ThirtyDollarConverter;
using ThirtyDollarWindowsGuiApp.Forms;
using ThirtyDollarWindowsGuiApp.Managers.Objects;

namespace ThirtyDollarWindowsGuiApp
{
    internal static class Program
    {
        public static ProgramSettings Settings = null!;
        private const string SettingsFileLocation = "./ProgramSettings.json";
        public static SampleHolder SampleHolder = null!;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            LoadSettings();
            ApplySettings();

            ApplicationConfiguration.Initialize();
            Application.Run(new MainWindow());
        }

        private static void ApplySettings()
        {
            SampleHolder = new SampleHolder()
            {
                DownloadLocation = Settings.AudioFileLocation,
                DownloadSampleUrl = Settings.SampleBaseUrl,
                ThirtyDollarWebsiteUrl = Settings.ThirtyDollarSiteUrl
            };
        }

        private static void LoadSettings()
        {
            if (File.Exists(SettingsFileLocation))
            {
                using var readFileStream = File.OpenRead(SettingsFileLocation);
                var deserialized = JsonSerializer.Deserialize<ProgramSettings>(readFileStream);
                Settings = deserialized ?? new ProgramSettings();
                Console.WriteLine("Read settings file.");
                return;
            }
            using var writeFileStream = File.OpenWrite(SettingsFileLocation);
            Settings = new ProgramSettings();
            JsonSerializer.Serialize(writeFileStream, Settings, new JsonSerializerOptions()
            {
                WriteIndented = true, // To make it easy for a person to edit the file.
            });
            Console.WriteLine("Created settings file.");
        }
    }
}