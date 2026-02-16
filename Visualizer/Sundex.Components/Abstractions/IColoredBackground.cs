using Shared.Renderer;

namespace Sundex.Components.Abstractions;

public interface IColoredBackground
{
    public Renderable? Background { get; set; }
}