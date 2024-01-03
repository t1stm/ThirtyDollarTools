using SixLabors.Fonts;

namespace ThirtyDollarVisualizer.Objects.Text;

public interface IText
{
    public string Value { get; protected set; }
    public float FontSizePx { get; }
    public void SetTextContents(string text);
    protected FontStyle FontStyle { get; }
}