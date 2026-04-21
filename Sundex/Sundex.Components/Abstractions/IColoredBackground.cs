using Sundex.Components.Attributes;

namespace Sundex.Components.Abstractions;

public interface IColoredBackground
{
    [NamedSetting("background")] public Renderable? Background { get; set; }
}