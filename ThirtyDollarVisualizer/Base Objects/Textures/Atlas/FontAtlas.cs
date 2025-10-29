using System.Buffers;
using Remora.MSDFGen;
using Remora.MSDFGen.Graphics;

namespace ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

public class FontAtlas : ImageAtlas
{
    private readonly Dictionary<int, AtlasReference> _coordinateTable
            = new(); /* UTF32 values instead of char to support every Unicode character
                       (this is kind of a big stretch since we're using only one font) */

    private const int GlyphSize = 64;
    
    public AtlasReference GetGlyph(int character)
    {
        return _coordinateTable.TryGetValue(character, out var reference) ? reference : AddGlyph(character);
    }

    public AtlasReference AddGlyph(int character)
    {
        var rent = ArrayPool<Color3>.Shared.Rent(GlyphSize * GlyphSize);
        var texture = new Pixmap<Color3>(GlyphSize, GlyphSize, rent);
        var shape = new Shape();
        
        // MSDF.GenerateMSDF(texture);
        
        var reference = new AtlasReference();
        _coordinateTable.Add(character, reference);

        ArrayPool<Color3>.Shared.Return(rent);
        return reference;
    }

    #region UTF16 Support

    public void AddGlyph(char character)
    {
        AddGlyph((int)character);
    }

    public void AddGlyph(char highSurrogate, char lowSurrogate)
    {
        var utf32 = 0x10000 + ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00);
        AddGlyph(utf32);
    }

    public AtlasReference GetGlyph(char highSurrogate, char lowSurrogate)
    {
        var utf32 = 0x10000 + ((highSurrogate - 0xD800) << 10) + (lowSurrogate - 0xDC00);
        return GetGlyph(utf32);
    }

    public AtlasReference GetGlyph(char character)
    {
        return !char.IsSurrogate(character)
            ? GetGlyph((int)character)
            : throw new Exception(
                "Surrogate character is given on a function that expects a single character. Use GetGlyph(char, char) instead.");
    }

    #endregion
}