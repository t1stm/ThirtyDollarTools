using SixLabors.Fonts;

namespace ThirtyDollarVisualizer.Objects.Text;

public class CharacterCache(FontFamily font_family, FontFamily emoji_family)
{
    public readonly Dictionary<FontStyle, Dictionary<float, Dictionary<char, Texture>>> Letters = new();
    public readonly Dictionary<string, Texture> Emojis = new();
    public FontFamily FontFamily => font_family;

    public Texture Get(char character, float font_size, FontStyle font_style = FontStyle.Regular)
    {
        lock (Letters)
        {
            if (character == '\n') return Texture.Transparent1x1;
            
            if (!Letters.TryGetValue(font_style, out var letters))
            {
                letters = new Dictionary<float, Dictionary<char, Texture>>();
                Letters.TryAdd(font_style, letters);
            }
            
            if (letters.TryGetValue(font_size, out var chars))
            {
                return GetCharacter(chars, character, font_size, font_style);
            }

            chars = new Dictionary<char, Texture>();
            letters.TryAdd(font_size, chars);

            return GetCharacter(chars, character, font_size, font_style);
        }
    }

    private Texture GetCharacter(Dictionary<char, Texture> chars, char character, float font_size, FontStyle font_style)
    {
        if (chars.TryGetValue(character, out var texture)) return texture;
        var font = font_family.CreateFont(font_size, font_style);
                
        texture = new Texture(font, new string(character, 1));
        chars.TryAdd(character, texture);
        return texture;
    }

    public Texture GetEmoji(string emoji, float font_size, FontStyle font_style)
    {
        if (Emojis.TryGetValue(emoji, out var texture)) return texture;
        var font = emoji_family.CreateFont(font_size, font_style);

        texture = new Texture(font, emoji);
        Emojis.TryAdd(emoji, texture);
        return texture;
    }
}