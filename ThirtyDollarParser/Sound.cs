using System.Text.Json.Serialization;

namespace ThirtyDollarParser
{
    public class Sound
    {
        [JsonPropertyName("id"), JsonInclude]
        public string? Id { get; init; }
        [JsonPropertyName("emoji"), JsonInclude]
        public string? Emoji { get; init; }
        [JsonPropertyName("name"), JsonInclude]
        public string? Name { get; init; }
        [JsonPropertyName("source"), JsonInclude]
        public string? Source { get; init; }
        public string? Filename => Emoji ?? Id;
    }
}