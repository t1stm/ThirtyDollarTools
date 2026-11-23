using VisualizerScene.Settings;

namespace ThirtyDollarVisualizer.VisualizerSettings;

public static class SettingsHandler
{
    public static readonly VisualizerScene.Settings.VisualizerSettings Settings = new();
    public static bool Loaded;
    private static string? _fileLocation;

    static SettingsHandler()
    {
        Settings.Changed += _ => ChangeHandler();
    }

    /// <summary>
    ///     Loads the settings file.
    /// </summary>
    /// <param name="fileLocation">Where the file is stored.</param>
    public static void Load(string fileLocation)
    {
        _fileLocation = fileLocation;
        if (!File.Exists(fileLocation))
        {
            Save(fileLocation);
            Loaded = true;
            return;
        }

        SettingsText.Read(Settings, File.ReadAllText(fileLocation));
        Loaded = true;
    }

    /// <summary>
    ///     Saves the settings file with the loaded location.
    /// </summary>
    public static void Save()
    {
        if (_fileLocation != null) Save(_fileLocation);
    }

    /// <summary>
    ///     Saves the settings to a file with the given location.
    /// </summary>
    /// <param name="fileLocation">Where the file will be stored.</param>
    public static void Save(string fileLocation)
    {
        _fileLocation = fileLocation;

        File.WriteAllText(_fileLocation, SettingsText.Write(Settings));
    }

    /// <summary>
    ///     Handles changes in the VisualizerSettings object.
    /// </summary>
    private static void ChangeHandler()
    {
        if (Loaded)
            Save();
    }
}