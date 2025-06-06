using SixLabors.Fonts;

namespace ThirtyDollarVisualizer.Objects.Text;

public abstract class ITextRenderable : Renderable, IText
{
    public abstract string Value { get; set; }
    public abstract float FontSizePx { get; set; }
    public abstract FontStyle FontStyle { get; set; }
    public abstract void SetTextContents(string text);
}