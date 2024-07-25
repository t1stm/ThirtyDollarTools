using System.Text;

namespace ThirtyDollarVisualizer.Settings;

public static class SettingsHandler
{
    public static readonly VisualizerSettings Settings = new(ChangeHandler);
    private static string? FileLocation;

    /// <summary>
    /// Loads the settings file.
    /// </summary>
    /// <param name="file_location">Where the file is stored.</param>
    public static void Load(string file_location)
    {
        FileLocation = file_location;
        if (!File.Exists(file_location)) return;

        var text = File.ReadAllText(file_location);
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

            if (property_type != typeof(string)) continue;
            property.SetValue(Settings, value, null);
        }
    }

    /// <summary>
    /// Saves the settings file with the loaded location.
    /// </summary>
    public static void Save()
    {
        if (FileLocation != null) Save(FileLocation);
    }
    
    /// <summary>
    /// Saves the settings to a file with the given location.
    /// </summary>
    /// <param name="file_location">Where the file will be stored.</param>
    public static void Save(string file_location)
    {
        FileLocation = file_location;

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
        
        File.WriteAllText(FileLocation, builder.ToString());
    }

    /// <summary>
    /// Handles changes in the VisualizerSettings object.
    /// </summary>
    public static void ChangeHandler()
    {
        Save();
    }
}