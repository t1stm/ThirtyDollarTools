using System.Text.Json.Serialization;

namespace ThirtyDollarParser;

public class Sound
{
    private const string Twemoji_PNG_URL = "https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/72x72";
    private const string Twemoji_SVG_URL = "https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/svg";
    private const string Thirty_Dollar_Asset_URL = "https://thirtydollar.website/icons";
    
    [JsonPropertyName("id")] [JsonInclude] public string? Id { get; init; }

    [JsonPropertyName("emoji")]
    [JsonInclude]
    public string? Emoji { get; init; }

    [JsonPropertyName("name")]
    [JsonInclude]
    public string? Name { get; init; }

    [JsonPropertyName("source")]
    [JsonInclude]
    public string? Source { get; init; }

    public string? Filename => Emoji ?? Id;

    public string Icon_URL => Emoji == null ? 
        $"{Thirty_Dollar_Asset_URL}/{Id}.png" : 
        $"{Twemoji_SVG_URL}/{char.ConvertToUtf32(Emoji, 0).ToString("X").ToLower()}.svg";
}