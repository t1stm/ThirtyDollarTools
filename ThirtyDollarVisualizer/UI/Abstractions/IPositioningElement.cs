namespace ThirtyDollarVisualizer.UI;

public interface IPositioningElement
{
    public LayoutDirection Direction { get; set; }
    public float Padding { get; set; }
    public float Spacing { get; set; }
}