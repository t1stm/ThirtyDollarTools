using System.Globalization;
using System.Text;

namespace VisualizerScene.Settings;

/// <summary>
///     The settings file's text: one <c>Name = value</c> line per property. The desktop keeps it
///     in a file, the browser build in its local storage.
/// </summary>
public static class SettingsText
{
    /// <summary>Sets every property <paramref name="text" /> names; unknown names and bad values are skipped.</summary>
    public static void Read(VisualizerSettings settings, string text)
    {
        var lines = text.Split(Environment.NewLine);

        var type = settings.GetType();
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
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var int_value)) continue;
                property.SetValue(settings, int_value, null);
                continue;
            }

            if (property_type == typeof(float))
            {
                // Files saved before the switch to the invariant culture hold commas on
                // comma-decimal locales; reading them back keeps the user's values.
                if (!float.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var float_value)) continue;
                property.SetValue(settings, float_value, null);
                continue;
            }

            if (property_type == typeof(bool))
            {
                if (!bool.TryParse(value, out var bool_value)) continue;
                property.SetValue(settings, bool_value, null);
                continue;
            }

            if (property_type != typeof(string)) continue;
            property.SetValue(settings, value, null);
        }
    }

    public static string Write(VisualizerSettings settings)
    {
        var type = settings.GetType();
        var properties = type.GetProperties();

        var builder = new StringBuilder();
        builder.AppendLine("# This is the settings file of the Thirty Dollar Visualizer.");
        builder.AppendLine("# Please don't try to break the program. I haven't implemented any fallback behavior.");
        builder.AppendLine();

        foreach (var property in properties)
        {
            var name = property.Name;
            var value = Convert.ToString(property.GetValue(settings), CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(value)) continue;
            builder.AppendLine($"{name} = {value}");
        }

        return builder.ToString();
    }
}
