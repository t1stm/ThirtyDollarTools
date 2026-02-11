using Shared.Renderer.Planes;

namespace Components.Abstractions;

public interface IColoredBackground
{
    public ColoredPlane? Background { get; set; }
}