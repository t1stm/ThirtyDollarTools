using SixLabors.Fonts;
using ThirtyDollarVisualizer.Objects;
using ThirtyDollarVisualizer.Objects.Text;

namespace ThirtyDollarVisualizer.UI;

public class Label : UIElement, IText
{
    protected LabelMode Mode;
    protected TextRenderable renderable = new StaticText();

    public LabelMode LabelMode
    {
        get => Mode;
        set
        {
            UpdateTextRenderableMode(Mode, value);
            Mode = value;
        }
    }

    public Label(string text, LabelMode mode = LabelMode.Static) : this(text, 0, 0, mode) { }
    
    public Label(string text, float x, float y, LabelMode mode = LabelMode.Static) : base(x, y, 0, 0)
    {
        Mode = mode;
        UpdateTextRenderableMode(null, mode);
        SetTextContents(text);
    }

    private void UpdateTextRenderableMode(LabelMode? old_mode, LabelMode new_mode)
    {
        if (old_mode?.Equals(new_mode) ?? false)
            return;

        var old_renderable = renderable;
        renderable = new_mode switch
        {
            LabelMode.Static => new StaticText(),
            LabelMode.BasicDynamic => new BasicDynamicText(),
            LabelMode.CachedDynamic => new CachedDynamicText(),
            _ => throw new ArgumentOutOfRangeException(nameof(new_mode), new_mode, null)
        };

        if (old_mode == null)
            return;

        var value = old_renderable.Value;
        var font_size = old_renderable.FontSizePx;
        var font_style = old_renderable.FontStyle;

        renderable.FontSizePx = font_size;
        renderable.FontStyle = font_style;
        SetTextContents(value);
    }

    public override void Layout()
    {
        renderable.SetPosition((AbsoluteX, AbsoluteY, 0));
        base.Layout();
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(renderable, Index);
    }

    public string Value
    {
        get => renderable.Value;
        set => SetTextContents(value);
    }

    public float FontSizePx
    {
        get => renderable.FontSizePx;
        set => renderable.FontSizePx = value;
    }

    public FontStyle FontStyle
    {
        get => renderable.FontStyle;
        set => renderable.FontStyle = value;
    }

    public void SetTextContents(string text)
    {
        renderable.SetTextContents(text);
        var scale = renderable.GetScale();

        Width = scale.X;
        Height = scale.Y;

        Parent?.Layout();
    }
}

public enum LabelMode
{
    Static,
    BasicDynamic,
    CachedDynamic
}