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
    private Vector3 _position = Vector3.Zero;
    private float _fontSize = 16;

    public Vector3 Scale { get; set; }

    public int Length { get; private set; }
    public int Offset { get; } = range.Start.Value;

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            UpdateCharacters();
        }
    }

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
        get => _fontSize;
        set
        {
            _fontSize = value;
            UpdateCharacters();
        }
    }
    
    private FontMetrics FontMetrics => textBuffer.TextProvider.GlyphProvider.FontMetrics;

    public void SetValue(ReadOnlySpan<char> value)
    {
        Value = value;
    }
    
    private void UpdateCharacters()
    {
        var val = Value;
        var textProvider = textBuffer.TextProvider;
        var fontMetrics = FontMetrics;
        var fontSize = FontSize;
        var cursorX = Position.X;
        var cursorY = Position.Y;

        var minX = cursorX;
        var minY = cursorY;
        var maxX = cursorX;
        var maxY = cursorY;
        
        var bufferIndex = 0;

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
                    bufferIndex++;
                    continue;
                
                case '\n':
                    cursorX = Position.X;
                    cursorY += FontSize * (float)(fontMetrics.LineHeight / fontMetrics.EmSize);
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

            var textCharacter = new TextCharacter();
            var atlasSize = new Vector2(textProvider.TextAtlas.Width, textProvider.TextAtlas.Height);

            textCharacter.TextureUV =
                (textureRectangle.X / atlasSize.X,
                    textureRectangle.Y / atlasSize.Y,
                    (textureRectangle.X + textureRectangle.Z) / atlasSize.X,
                    (textureRectangle.Y + textureRectangle.W) / atlasSize.Y);

            var (advanceUnitSpace, translate, scale) = textAlignmentData;
            
            var translateX = (float)translate.X; // unit space
            var translateY = (float)translate.Y; // unit space
            var scaleX = (float)scale.X; // multiplier of unit space
            var scaleY = (float)scale.Y; // multiplier of unit space

            var positionX = cursorX - translateX * fontSize;
            var positionY = cursorY - (GlyphProvider.GlyphSize / scaleY - translateY) * fontSize;
            var scaleW = GlyphProvider.GlyphSize / scaleX * fontSize;
            var scaleH = GlyphProvider.GlyphSize / scaleY * fontSize;

            textCharacter.Position = new Vector3(positionX, positionY, Position.Z);
            textCharacter.Scale = new Vector2(scaleW, scaleH);

            cursorX += (float)advanceUnitSpace * fontSize;
            
            maxX = Math.Max(maxX, cursorX);
            maxY = Math.Max(maxY, cursorY + FontSize * (float)(fontMetrics.LineHeight / fontMetrics.EmSize));

            if (Offset + bufferIndex >= textBuffer.Characters.Capacity) 
                throw new Exception("TextSlice capacity exceeded.");
            
            textBuffer.Characters[Offset + bufferIndex] = textCharacter;
            bufferIndex++;
        }

        Scale = new Vector3(maxX - minX, maxY - minY, 1);
    }

    public void Dispose()
    {
        _value.AsSpan().Clear();
        UpdateCharacters();
        
        textBuffer.Remove(this);
        GC.SuppressFinalize(this);
    }
}