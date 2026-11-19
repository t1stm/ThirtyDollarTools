using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;

namespace Sundex.Components.Panels;

/// <summary>
///     Shows its children in <see cref="UIContext.OverlayHost" /> rather than where it stands: the
///     web's top layer, for dialogs, menus and popovers. It takes no space and draws nothing itself.
///     What it holds is laid out, clipped and hit-tested as the host's children, and ends with it.
///     With no host its children stay inside it, as an ordinary region's would.
/// </summary>
[MarkupTag("portal")]
public class Portal(UIContext context) : Region(context)
{
    private readonly List<UIElement> _moved = [];

    public override string Tag => "portal";

    public override void AddChild(UIElement child)
    {
        if (Context.OverlayHost is not { } host)
        {
            base.AddChild(child);
            return;
        }

        _moved.Add(child);
        host.AddChild(child);
    }

    protected override void OnDispose()
    {
        foreach (var child in _moved) child.Dispose();
        _moved.Clear();
        base.OnDispose();
    }
}
