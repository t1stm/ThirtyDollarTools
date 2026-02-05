using ThirtyDollarVisualizer.Base_Objects.Planes;

namespace ThirtyDollarVisualizer.UI.Abstractions;

public interface IColoredBackground
{
    public ColoredPlane? Background { get; set; }
}