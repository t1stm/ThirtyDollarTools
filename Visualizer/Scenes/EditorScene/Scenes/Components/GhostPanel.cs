using Sundex.Components.Abstractions;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

internal class GhostPanel(UIContext context) : Panel(context)
{
    public override UIElement? HitTest(float x, float y)
    {
        return null;
    }
}
