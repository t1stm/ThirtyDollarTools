using Components.Abstractions;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Text;

namespace Components.Labels;

[PreloadGraphicsContext]
public class Label : UIElement
{
    protected readonly TextBuffer? TextBuffer;
    private string _textValue = string.Empty;

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

    public Label(UIContext context, ReadOnlySpan<char> text, float x = 0, float y = 0) : base(context, x, y, 0, 0)
    {
        _textValue = text.ToString();
        if (context.TextProvider == null) return;
        TextBuffer = new TextBuffer(context.TextProvider);
        TextSlice = TextBuffer.GetTextSlice(text);
    }

    public ReadOnlySpan<char> Value
    {
        get => TextSlice != null ? TextSlice.Value : _textValue;
        set => SetTextContents(value);
    }

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

    public Vector4 Color
    {
        get;
        set
        {
            field = value;
            if (TextSlice != null)
                TextSlice.Color = value;
        }
    } = Vector4.One;

    public void SetTextContents(ReadOnlySpan<char> text)
    {
        _textValue = text.ToString();
        if (TextSlice == null) return;
        if (text.Length > TextSlice.Length)
        {
            TextSlice.Dispose();
            TextSlice = TextBuffer.GetTextSlice(text);
            TextSlice.UpdateManually = true;
            
            TextSlice.FontSize = FontSizePx;
            TextSlice.Color = Color;

            TextSlice.UpdateManually = false;
            TextSlice.SetPosition((AbsoluteX, AbsoluteY, 0));
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

    protected override void DoLayout()
    {
        TextSlice?.SetPosition((AbsoluteX, AbsoluteY, 0));
    }

    protected override void DrawSelf(UIContext context)
    {
        if (TextBuffer != null)
            context.QueueRender(TextBuffer, Index);
    }
}