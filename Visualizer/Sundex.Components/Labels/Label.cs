using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Text;

namespace Sundex.Components.Labels;

[PreloadGraphicsContext]
public class Label : UIElement
{
    protected readonly TextBuffer? TextBuffer;
    private string _textValue;

    protected TextSlice? TextSlice
    {
        get;
        set
        {
            field = value;
            if (field == null) return;
            Width = field.Scale.X;
            Height = field.Scale.Y;
        }
    }

    public Label(UIContext context, ReadOnlySpan<char> text) : base(context)
    {
        _textValue = text.ToString();
        TextBuffer = new TextBuffer(context.TextProvider);
        TextSlice = TextBuffer.GetTextSlice(text);
    }

    [NamedSetting("text-value")]
    public ReadOnlySpan<char> Value
    {
        get => TextSlice != null ? TextSlice.Value : _textValue;
        set => SetTextContents(value);
    }

    [NamedSetting("font-size")]
    public float FontSizePx
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 0.01f) return;
            field = value;
            if (TextSlice == null) return;
            TextSlice.FontSize = value;
            
            var scale = TextSlice.Scale;
            Width = scale.X;
            Height = scale.Y;
        }
    }

    [NamedSetting("font-color")]
    public Vector4 Color
    {
        get;
        set
        {
            field = value;
            TextSlice?.Color = value;
        }
    } = Vector4.One;

    public void SetTextContents(ReadOnlySpan<char> text)
    {
        _textValue = text.ToString();
        if (TextSlice == null) return;
        if (TextBuffer == null) return;
        
        if (text.Length > TextSlice.Length)
        {
            TextSlice.Dispose();
            TextSlice = TextBuffer.GetTextSlice(text);
            TextSlice.UpdateManually = true;
            
            TextSlice.FontSize = FontSizePx;
            TextSlice.Color = Color;

            TextSlice.UpdateManually = false;
            TextSlice.SetPosition((Computed.AbsoluteX, Computed.AbsoluteY, 0));
        }
        else
        {
            TextSlice.UpdateManually = true;
            
            TextSlice.Value = text;
            TextSlice.FontSize = FontSizePx;
            TextSlice.Color = Color;
            
            TextSlice.UpdateManually = false;
            TextSlice.UpdateCharacters();
        }
        
        var scale = TextSlice.Scale;

        Width = scale.X;
        Height = scale.Y;

        InvalidateLayout();
    }

    public override string Tag => "label";

    protected override void DoLayout()
    {
        TextSlice?.SetPosition((Computed.AbsoluteX, Computed.AbsoluteY, 0));
    }

    protected override void DrawSelf(UIContext context)
    {
        if (TextBuffer != null)
            context.QueueRender(TextBuffer, Index);
    }
}