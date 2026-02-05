using System.Text;

namespace ThirtyDollarVisualizer.Settings;

public static class SettingsHandler
{
    public static readonly VisualizerSettings Settings = new(ChangeHandler);
    public static bool Loaded;
    private static string? _fileLocation;

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

        var text = File.ReadAllText(fileLocation);
        var lines = text.Split(Environment.NewLine);

        var type = Settings.GetType();
        var properties = type.GetProperties().Select(p => (p.Name, p)).ToDictionary();

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (line.StartsWith('#')) continue;
            if (line.StartsWith('[')) continue;

            var split = line.Split('=');
            if (split.Length < 2) continue;

            var remainder = split[1..];
            var name = split[0].Trim();
            var value = string.Join('=', remainder).Trim();

            // remove comments from value
            value = value.Split(" # ")[0];

            if (!properties.TryGetValue(name, out var property)) continue;
            var property_type = property.PropertyType;

            if (property_type == typeof(int))
            {
                if (!int.TryParse(value, out var int_value)) continue;
                property.SetValue(Settings, int_value, null);
                continue;
            }

            if (property_type == typeof(float))
            {
                if (!float.TryParse(value, out var float_value)) continue;
                property.SetValue(Settings, float_value, null);
                continue;
            }

            if (property_type == typeof(bool))
            {
                if (!bool.TryParse(value, out var bool_value)) continue;
                property.SetValue(Settings, bool_value, null);
                continue;
            }

            if (property_type != typeof(string)) continue;
            property.SetValue(Settings, value, null);
        }

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

        var type = Settings.GetType();
        var properties = type.GetProperties();

        var builder = new StringBuilder();
        builder.AppendLine("# This is the settings file of the Thirty Dollar Visualizer.");
        builder.AppendLine("# Please don't try to break the program. I haven't implemented any fallback behavior.");
        builder.AppendLine();

        foreach (var property in properties)
        {
            var name = property.Name;
            var value = property.GetValue(Settings)?.ToString();
            if (string.IsNullOrEmpty(value)) continue;
            builder.AppendLine($"{name} = {value}");
        }

        File.WriteAllText(_fileLocation, builder.ToString());
    }

    /// <summary>
    ///     Handles changes in the VisualizerSettings object.
    /// </summary>
    public static void ChangeHandler()
    {
        if (Loaded)
            Save();
    }
}