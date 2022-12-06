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
        public readonly static SampleHolder SampleHolder = new();

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            LoadSettings();

            ApplicationConfiguration.Initialize();
            Application.Run(new MainWindow());
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
            JsonSerializer.Serialize(writeFileStream, Settings);
            Console.WriteLine("Created settings file.");
        }
    }
}