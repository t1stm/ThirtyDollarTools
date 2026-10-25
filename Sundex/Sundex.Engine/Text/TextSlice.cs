using System.Buffers;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Slug;
using Sundex.Slug.Fonts;

namespace Sundex.Engine.Text;

[PreloadGraphicsContext]
public class TextSlice(TextBuffer textBuffer, Range range)
    : IPositionable, IDisposable
{
    // Can outgrow the range after a Rebind to a shorter one; _capacity is the range's length.
    private char[] _value = new char[range.End.Value - range.Start.Value];
    private int _capacity = range.End.Value - range.Start.Value;

    public int Length { get; private set; }
    public int Offset { get; private set; } = range.Start.Value;

    public bool UpdateManually { get; set; }

    public ReadOnlySpan<char> Value
    {
        get => _value.AsSpan(0, _capacity);
        set
        {
            var destination = _value.AsSpan(0, _capacity);
            if (_capacity < value.Length)
                throw new InvalidOperationException("TextSlice capacity exceeded.");

            value.CopyTo(destination);
            Length = value.Length;

            if (_capacity > value.Length)
                destination[value.Length..].Clear();

            if (UpdateManually) return;
            UpdateCharacters();
        }
    }

    public float FontSize
    {
        get;
        set
        {
            field = value;
            if (UpdateManually) return;
            UpdateCharacters();
        }
    } = 16;

    public Vector4 Color
    {
        get;
        set
        {
            field = value;
            if (UpdateManually) return;
            UpdateCharacters();
        }
    } = Vector4.One;

    /// <inheritdoc cref="TextCharacter.ClipRect" />
    public Vector4 ClipRect
    {
        get;
        set
        {
            field = value;
            if (UpdateManually) return;
            UpdateCharacters();
        }
    }

    private SlugFontMetrics FontMetrics => textBuffer.TextProvider.GlyphProvider.GetFontMetrics();

    public void Dispose()
    {
        textBuffer.Remove(this);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Moves this slice to another range of its buffer, empty, for
    ///     <see cref="TextBuffer.Reuse" />. Keeps its storage when the new range fits in it.
    /// </summary>
    internal void Rebind(Range range)
    {
        Offset = range.Start.Value;
        _capacity = range.End.Value - range.Start.Value;
        if (_value.Length < _capacity) _value = new char[_capacity];
        else _value.AsSpan(0, _capacity).Clear();
        Length = 0;
    }

    public Vector3 Scale { get; set; }

    public Vector3 Position
    {
        get;
        set
        {
            field = value;
            if (UpdateManually) return;
            UpdateCharacters();
        }
    } = Vector3.Zero;

    public void SetValue(ReadOnlySpan<char> value)
    {
        Value = value;
    }

    public void UpdateCharacters()
    {
        var val = Value;
        var textProvider = textBuffer.TextProvider;
        var fontMetrics = FontMetrics;

        var positioningProvider = new FlexLinePositioningProvider<TextCharacter>
        {
            FontSize = FontSize,
            BasePosition = Position,
            LineHeight = fontMetrics.LineHeight,
            EmSize = fontMetrics.EmSize
        };

        var layouts = ArrayPool<FlexLineItemPlacementLayout>.Shared.Rent(val.Length);
        var layoutsSpan = layouts.AsSpan()[..val.Length];
        layoutsSpan.Clear();

        var bufferIndex = 0;
        var newLineCount = 0;

        Span<char> characters = stackalloc char[2]; // this is an array because we need to support surrogate pairs
        for (var index = 0; index < val.Length; index++)
        {
            var character = val[index];
            switch (character)
            {
                case (char)0:
                    if (Offset + bufferIndex >= textBuffer.Characters.Capacity)
                        throw new Exception("TextSlice capacity exceeded.");

                    textBuffer.Characters[Offset + bufferIndex] = new TextCharacter();
                    layoutsSpan[bufferIndex] = new FlexLineItemPlacementLayout();
                    bufferIndex++;
                    continue;

                case '\n':
                    newLineCount += 1;
                    continue;
            }

            SlugGlyph glyph;
            if (char.IsSurrogate(character) && index + 1 < val.Length &&
                char.IsSurrogatePair(character, val[index + 1]))
            {
                characters[0] = character;
                characters[1] = val[index + 1];

                glyph = textProvider.GetGlyph(characters);
                index++;
            }
            else
            {
                characters[0] = character;
                characters[1] = (char)0;
                glyph = textProvider.GetGlyph(characters[..1]);
            }

            var textCharacter = new TextCharacter
            {
                Color = Color,
                ClipRect = ClipRect,
                EmRect = Convert(glyph.EmRect),
                Banding = Convert(glyph.Banding),
                GlyphData = Convert(glyph.GlyphData)
            };

            // The quad is the glyph's padded ink box: the positioning provider places it from
            // Translate/Scale at its default RelativeSize of 1 (sundex-slug-plan.md, "Layout mapping").
            layoutsSpan[bufferIndex] = new FlexLineItemPlacementLayout
            {
                Advance = glyph.Advance,
                Translate = new Vector2(glyph.TextTranslate.X, glyph.TextTranslate.Y),
                Scale = new Vector2(glyph.TextScale.X, glyph.TextScale.Y),
                NewLines = newLineCount
            };

            if (Offset + bufferIndex >= textBuffer.Characters.Capacity)
            {
                ArrayPool<FlexLineItemPlacementLayout>.Shared.Return(layouts);
                throw new Exception("TextSlice capacity exceeded.");
            }

            textBuffer.Characters[Offset + bufferIndex] = textCharacter;
            bufferIndex++;
            newLineCount = 0;
        }

        var textCharacters = textBuffer.Characters;
        var size = positioningProvider.UpdatePositions(
            ref textCharacters, layoutsSpan, Offset, bufferIndex);
        Scale = new Vector3(size.X, size.Y, 1);

        ArrayPool<FlexLineItemPlacementLayout>.Shared.Return(layouts);
    }

    private static Vector4 Convert(System.Numerics.Vector4 v) => new(v.X, v.Y, v.Z, v.W);
}