using System.Text.Json.Serialization;

namespace ThirtyDollarParser;

public class Sound
{
    /// <summary>
    /// Thumbnail url for emoji sounds.
    /// </summary>
    private const string Twemoji_SVG_URL = "https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/svg";

    /// <summary>
    /// Thumbnail url for other sounds.
    /// </summary>
    private const string Thirty_Dollar_Asset_URL = "https://thirtydollar.website/icons";

    /// <summary>
    /// The id of the current sound
    /// </summary>
    [JsonPropertyName("id")] [JsonInclude] public string? Id { get; init; }

    /// <summary>
    /// An optional emoji of the current sound.
    /// </summary>
    [JsonPropertyName("emoji")]
    [JsonInclude]
    public string? Emoji { get; init; }

    /// <summary>
    /// The name of the current sound.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonInclude]
    public string? Name { get; init; }
    
    /// <summary>
    /// Source of the current sound.
    /// </summary>
    [JsonPropertyName("source")]
    [JsonInclude]
    public string? Source { get; init; }

    /// <summary>
    /// How the sound is interpreted by the composition and the sound server.
    /// </summary>
    public string? Filename => Emoji ?? Id;

    /// <summary>
    /// The icon url for this sound.
    /// </summary>
    public string Icon_URL => Emoji == null ? 
        $"{Thirty_Dollar_Asset_URL}/{Id}.png" : 
        $"{Twemoji_SVG_URL}/{char.ConvertToUtf32(Emoji, 0).ToString("X").ToLower()}.svg";
}