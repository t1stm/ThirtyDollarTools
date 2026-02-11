using Shared.Renderer;

namespace Components.Abstractions;

public interface IColoredBackground
{
    public Renderable? Background { get; set; }
}