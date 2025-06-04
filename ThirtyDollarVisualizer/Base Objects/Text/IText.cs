using SixLabors.Fonts;

namespace ThirtyDollarVisualizer.Base_Objects.Text;

public interface IText
{
    public string Value { get; protected set; }
    public float FontSizePx { get; }
    protected FontStyle FontStyle { get; }
    public void SetTextContents(string text);
}