using MsdfAtlasGen;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Buffers;
using ThirtyDollarVisualizer.Engine.Renderer.Camera;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;
using ThirtyDollarVisualizer.Engine.Text.Fonts;
using FontMetrics = Msdfgen.Extensions.FontMetrics;

namespace ThirtyDollarVisualizer.Engine.Text;

[PreloadGL]
public class TextSlice : IBindable
{
    private readonly GLBuffer<TextCharacter>.WithCPUCache _characters;
    private readonly VertexArrayObject _vao;
    private string _value = string.Empty;
    private Vector3 _position = Vector3.Zero;
    private readonly TextProvider _textProvider;
    private float _fontSize = 16;

    public TextSlice(TextProvider textProvider)
    {
        _characters = new GLBuffer<TextCharacter>.WithCPUCache(textProvider.AssetProvider.DeleteQueue,
            BufferTarget.ArrayBuffer);

        _textProvider = textProvider;
        _vao = new VertexArrayObject();
        ReflectToVAO(_vao);
    }

    private void ReflectToVAO(VertexArrayObject vao)
    {
        vao.AddBuffer(GLQuad.VBOWithoutUV, new VertexBufferLayout().PushFloat(3));

        var layout = new VertexBufferLayout();
        TextCharacter.SelfReflectToGL(layout);
        vao.AddBuffer(_characters, layout);
        vao.SetIndexBuffer(GLQuad.EBO);
    }

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            UpdateCharacters();
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            _value = value;
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

    private FontMetrics FontMetrics => _textProvider.GlyphProvider.FontMetrics;

    private void UpdateCharacters()
    {
        var fontMetrics = FontMetrics;
        var fontSize = FontSize;
        var cursorX = Position.X;
        var cursorY = Position.Y;
        
        _characters.ResizeCPUBuffer(_value.Length);
        var bufferIndex = 0;

        Span<char> characters = stackalloc char[2]; // this is an array because we need to support surrogate pairs
        for (var index = 0; index < _value.Length; index++)
        {
            var character = _value[index];
            if (character == '\n')
            {
                cursorX = Position.X;
                cursorY += FontSize * (float)(fontMetrics.LineHeight / fontMetrics.EmSize);
                continue;
            }

            TextCharacter textCharacter;
            TextAlignmentData textAlignmentData;

            if (char.IsSurrogate(character) && index + 1 < _value.Length &&
                char.IsSurrogatePair(character, _value[index + 1]))
            {
                characters[0] = character;
                characters[1] = _value[index + 1];

                (textCharacter, textAlignmentData) = _textProvider.GetTextCharacter(characters);
                index++;
            }
            else
            {
                characters[0] = character;
                characters[1] = (char)0;
                (textCharacter, textAlignmentData) = _textProvider.GetTextCharacter(characters[..1]);
            }

            var textureSize = textCharacter.TextureUV; // this is currently atlas coordinates, converting to UV below
            var atlasSize = new Vector2(_textProvider.TextAtlas.Width, _textProvider.TextAtlas.Height);

            textCharacter.TextureUV =
                (textureSize.X / atlasSize.X,
                    textureSize.Y / atlasSize.Y,
                    (textureSize.X + textureSize.Z) / atlasSize.X,
                    (textureSize.Y + textureSize.W) / atlasSize.Y);

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

            _characters[bufferIndex] = textCharacter;
            bufferIndex++;
        }
    }

    public void Render(Camera camera)
    {
        _textProvider.BindAndSetUniforms(camera, Vector4.One);
        Update();

        GL.DrawElementsInstanced(PrimitiveType.Triangles, GLQuad.EBO.Capacity, DrawElementsType.UnsignedInt,
            IntPtr.Zero, _characters.Data.Length);
        RenderMarker.Debug("Rendered Text Slice: ", _value, MarkerType.Hidden);
    }

    public void Update()
    {
        _vao.Bind();
        _vao.Update();
    }

    #region Deferred IBindable implementation

    public BufferState BufferState => _characters.BufferState;
    public int Handle => _characters.Handle;

    public void Bind()
    {
        Update();
    }

    #endregion
}