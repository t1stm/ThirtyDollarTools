using System.Buffers;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Text.Fonts;
using FontMetrics = Msdfgen.Extensions.FontMetrics;

namespace ThirtyDollarVisualizer.Engine.Text;

[PreloadGraphicsContext]
public class TextSlice(TextBuffer textBuffer, Range range)
    : IPositionable, IDisposable
{
    private readonly char[] _value = new char[range.End.Value - range.Start.Value];

    public int Length { get; private set; }
    public int Offset { get; } = range.Start.Value;

    public bool UpdateManually { get; set; }

    public ReadOnlySpan<char> Value
    {
        get => _value;
        set
        {
            Span<char> destination = _value;
            if (_value.Length < value.Length)
                throw new InvalidOperationException("TextSlice capacity exceeded.");

            value.CopyTo(destination);
            Length = value.Length;

            if (_value.Length > value.Length)
                _value.AsSpan(value.Length).Clear();

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
    
    public Vector4 Color { get; set; } = Vector4.One;

    private FontMetrics FontMetrics => textBuffer.TextProvider.GlyphProvider.GetFontMetrics();

    public void Dispose()
    {
        textBuffer.Remove(this);
        GC.SuppressFinalize(this);
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

            Vector4 textureRectangle;
            TextAlignmentData textAlignmentData;

            if (char.IsSurrogate(character) && index + 1 < val.Length &&
                char.IsSurrogatePair(character, val[index + 1]))
            {
                characters[0] = character;
                characters[1] = val[index + 1];

                (textureRectangle, textAlignmentData) = textProvider.GetTextCharacterRect(characters);
                index++;
            }
            else
            {
                characters[0] = character;
                characters[1] = (char)0;
                (textureRectangle, textAlignmentData) = textProvider.GetTextCharacterRect(characters[..1]);
            }

            var textCharacter = new TextCharacter
            {
                Color = Color
            };
            var atlasSize = new Vector2(textProvider.TextAtlas.Width, textProvider.TextAtlas.Height);

            textCharacter.TextureUV =
                (textureRectangle.X / atlasSize.X,
                    textureRectangle.Y / atlasSize.Y,
                    (textureRectangle.X + textureRectangle.Z) / atlasSize.X,
                    (textureRectangle.Y + textureRectangle.W) / atlasSize.Y);

            layoutsSpan[bufferIndex] = new FlexLineItemPlacementLayout
            {
                Advance = textAlignmentData.AdvanceInUnitSpace,
                Translate = new Vector2((float)textAlignmentData.Translate.X, (float)textAlignmentData.Translate.Y),
                Scale = new Vector2((float)textAlignmentData.Scale.X, (float)textAlignmentData.Scale.Y),
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
}