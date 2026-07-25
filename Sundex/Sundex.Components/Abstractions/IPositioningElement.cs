using Sundex.Components.Attributes;

namespace Sundex.Components.Abstractions;

public interface IPositioningElement
{
    [NamedSetting("direction")] public LayoutDirection Direction { get; set; }
    [NamedSetting("padding")] public float Padding { get; set; }
    [NamedSetting("spacing")] public float Spacing { get; set; }
}