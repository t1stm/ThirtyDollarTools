using Sundex.Components.Abstractions;

namespace Sundex.Components.Panels;

/// <summary>A panel that never takes input - not it, nor anything inside it. For overlays that only paint.</summary>
public class GhostPanel(UIContext context) : Panel(context)
{
    public override UIElement? HitTest(float x, float y)
    {
        return null;
    }
}
