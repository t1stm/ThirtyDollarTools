using Components.Abstractions;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Text;
using ThirtyDollarVisualizer.Engine.Text.Fonts;

namespace Components.Labels;

[PreloadGraphicsContext]
public class Label : UIElement
{
    protected readonly TextBuffer TextBuffer;
    protected readonly TextSlice TextSlice;

    public Label(UIContext context, ReadOnlySpan<char> text, float x = 0, float y = 0) : base(context, x, y, 0, 0)
    {
        SetTextContents(text);
        TextBuffer = new TextBuffer(context.TextProvider);
        TextSlice = TextBuffer.GetTextSlice(text);
    }

    public ReadOnlySpan<char> Value
    {
        get => TextSlice.Value;
        set => SetTextContents(value);
    }

    public float FontSizePx
    {
        get => TextSlice.FontSize;
        set => TextSlice.FontSize = value;
    }

    public void SetTextContents(ReadOnlySpan<char> text)
    {
        TextSlice.Value = text;
        var scale = TextSlice.Scale;

        Width = scale.X;
        Height = scale.Y;

        Parent?.Layout();
    }

    public override void Layout()
    {
        TextSlice.SetPosition((AbsoluteX, AbsoluteY, 0));
        base.Layout();
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(TextBuffer, Index);
    }
}