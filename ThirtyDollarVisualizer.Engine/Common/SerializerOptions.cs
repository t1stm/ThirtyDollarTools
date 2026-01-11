using System.Text.Json;

namespace ThirtyDollarVisualizer.Engine.Common;

public static class SerializerOptions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
}