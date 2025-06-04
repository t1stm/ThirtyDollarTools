using SixLabors.Fonts;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Base_Objects.Text;

public class CharacterCache(FontFamily fontFamily, FontFamily emojiFamily)
{
    public readonly Dictionary<string, StaticTexture> Emojis = new();
    public readonly Dictionary<FontStyle, Dictionary<float, Dictionary<char, StaticTexture>>> Letters = new();
    public FontFamily FontFamily => fontFamily;

    public StaticTexture Get(char character, float fontSize, FontStyle fontStyle = FontStyle.Regular)
    {
        lock (Letters)
        {
            if (character == '\n') return StaticTexture.TransparentPixel;

            if (!Letters.TryGetValue(fontStyle, out var letters))
            {
                letters = new Dictionary<float, Dictionary<char, StaticTexture>>();
                Letters.TryAdd(fontStyle, letters);
            }

            if (letters.TryGetValue(fontSize, out var chars))
                return GetCharacter(chars, character, fontSize, fontStyle);

            chars = new Dictionary<char, StaticTexture>();
            letters.TryAdd(fontSize, chars);

            return GetCharacter(chars, character, fontSize, fontStyle);
        }
    }

    private StaticTexture GetCharacter(Dictionary<char, StaticTexture> chars, char character, float fontSize,
        FontStyle fontStyle)
    {
        if (chars.TryGetValue(character, out var texture)) return texture;
        var font = fontFamily.CreateFont(fontSize, fontStyle);

        texture = new FontTexture(font, new string(character, 1));
        chars.TryAdd(character, texture);
        return texture;
    }

    public StaticTexture GetEmoji(string emoji, float fontSize, FontStyle fontStyle)
    {
        return GetEmoji(emoji.AsSpan(), fontSize, fontStyle);
    }

    public StaticTexture GetEmoji(ReadOnlySpan<char> emoji, float fontSize, FontStyle fontStyle)
    {
        var alternative_lookup = Emojis.GetAlternateLookup<ReadOnlySpan<char>>();

        if (alternative_lookup.TryGetValue(emoji, out var texture)) return texture;
        var font = emojiFamily.CreateFont(fontSize, fontStyle);

        var emoji_string = emoji.ToString();
        texture = new FontTexture(font, emoji_string);
        Emojis.TryAdd(emoji_string, texture);
        return texture;
    }
}