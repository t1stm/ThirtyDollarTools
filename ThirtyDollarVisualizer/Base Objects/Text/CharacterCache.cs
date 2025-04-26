using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects.Text;

public class CharacterCache(FontFamily font_family, FontFamily emoji_family)
{
    public readonly Dictionary<string, StaticTexture> Emojis = new();
    public readonly Dictionary<FontStyle, Dictionary<float, Dictionary<char, StaticTexture>>> Letters = new();
    public FontFamily FontFamily => font_family;

    public StaticTexture Get(char character, float font_size, FontStyle font_style = FontStyle.Regular)
    {
        lock (Letters)
        {
            if (character == '\n') return StaticTexture.Transparent1x1;

            if (!Letters.TryGetValue(font_style, out var letters))
            {
                letters = new Dictionary<float, Dictionary<char, StaticTexture>>();
                Letters.TryAdd(font_style, letters);
            }

            if (letters.TryGetValue(font_size, out var chars))
                return GetCharacter(chars, character, font_size, font_style);

            chars = new Dictionary<char, StaticTexture>();
            letters.TryAdd(font_size, chars);

            return GetCharacter(chars, character, font_size, font_style);
        }
    }

    private StaticTexture GetCharacter(Dictionary<char, StaticTexture> chars, char character, float font_size, FontStyle font_style)
    {
        if (chars.TryGetValue(character, out var texture)) return texture;
        var font = font_family.CreateFont(font_size, font_style);

        texture = new FontTexture(font, new string(character, 1));
        chars.TryAdd(character, texture);
        return texture;
    }

    public StaticTexture GetEmoji(string emoji, float font_size, FontStyle font_style)
    {
        return GetEmoji(emoji.AsSpan(), font_size, font_style);
    }
    
    public StaticTexture GetEmoji(ReadOnlySpan<char> emoji, float font_size, FontStyle font_style)
    {
        var alternative_lookup = Emojis.GetAlternateLookup<ReadOnlySpan<char>>();
        
        if (alternative_lookup.TryGetValue(emoji, out var texture)) return texture;
        var font = emoji_family.CreateFont(font_size, font_style);

        var emoji_string = emoji.ToString();
        texture = new FontTexture(font, emoji_string);
        Emojis.TryAdd(emoji_string, texture);
        return texture;
    }
}