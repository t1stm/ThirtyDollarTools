using SixLabors.Fonts;
using ThirtyDollarVisualizer.Base_Objects.Text;
using ThirtyDollarVisualizer.UI.Abstractions;

namespace ThirtyDollarVisualizer.UI.Components.Labels;

public class Label : UIElement, IText
{
    protected LabelMode Mode;
    protected TextRenderable Renderable = new StaticText();

    public Label(string text, LabelMode mode = LabelMode.Static) : this(text, 0, 0, mode)
    {
    }

    public Label(string text, float x, float y, LabelMode mode = LabelMode.Static) : base(x, y, 0, 0)
    {
        Mode = mode;
        UpdateTextRenderableMode(null, mode);
        SetTextContents(text);
    }

    public LabelMode LabelMode
    {
        get => Mode;
        set
        {
            UpdateTextRenderableMode(Mode, value);
            Mode = value;
        }
    }

    public string Value
    {
        get => Renderable.Value;
        set => SetTextContents(value);
    }

    public float FontSizePx
    {
        get => Renderable.FontSizePx;
        set => Renderable.FontSizePx = value;
    }

    public FontStyle FontStyle
    {
        get => Renderable.FontStyle;
        set => Renderable.FontStyle = value;
    }

    public void SetTextContents(string text)
    {
        Renderable.Value = text;
        var scale = Renderable.Scale;

        Width = scale.X;
        Height = scale.Y;

        Parent?.Layout();
    }

    private void UpdateTextRenderableMode(LabelMode? oldMode, LabelMode newMode)
    {
        if (oldMode?.Equals(newMode) ?? false)
            return;

        var old_renderable = Renderable;
        Renderable = newMode switch
        {
            LabelMode.Static => new StaticText(),
            LabelMode.BasicDynamic => new BasicDynamicText(),
            LabelMode.CachedDynamic => new CachedDynamicText(),
            _ => throw new ArgumentOutOfRangeException(nameof(newMode), newMode, null)
        };

        if (oldMode == null)
            return;

        var value = old_renderable.Value;
        var font_size = old_renderable.FontSizePx;
        var font_style = old_renderable.FontStyle;

        Renderable.FontSizePx = font_size;
        Renderable.FontStyle = font_style;
        SetTextContents(value);
    }

    public override void Layout()
    {
        Renderable.SetPosition((AbsoluteX, AbsoluteY, 0));
        base.Layout();
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(Renderable, Index);
    }
}

public enum LabelMode
{
    Static,
    BasicDynamic,
    CachedDynamic
}