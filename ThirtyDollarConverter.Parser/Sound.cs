using System.Text.Json.Serialization;

namespace ThirtyDollarParser;

public class Sound
{
    /// <summary>
    /// Thumbnail url for PNG emoji sounds.
    /// </summary>
    private const string TwemojiPngUrl = "https://cdnjs.cloudflare.com/ajax/libs/twemoji/14.0.2/72x72";

    /// <summary>
    /// Thumbnail url for other sounds.
    /// </summary>
    private const string ThirtyDollarAssetUrl = "https://thirtydollar.website/icons";

    /// <summary>
    /// The id of the current sound
    /// </summary>
    [JsonPropertyName("id")]
    [JsonInclude]
    public required string Id { get; init; }

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
    /// Whether the sound should always use its ID when saving, even if it has an emoji.
    /// </summary>
    [JsonPropertyName("useId")]
    [JsonInclude]
    public bool UseID { get; set; }

    /// <summary>
    /// How the sound is interpreted by the sequence and the sound server.
    /// </summary>
    public string? Filename => Emoji ?? Id;

    /// <summary>
    /// The icon url for this sound.
    /// </summary>
    public string IconUrl => Emoji == null
        ? $"{ThirtyDollarAssetUrl}/{Id}.png"
        : $"{TwemojiPngUrl}/{char.ConvertToUtf32(Emoji, 0).ToString("X").ToLower()}.png";
}