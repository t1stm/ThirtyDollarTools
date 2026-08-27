using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;

namespace Sundex.Components.Panels;

/// <summary>
///     A one-button informational modal for messages a scene has to surface but has no
///     place for - a platform refusing an action, a failure that would otherwise be
///     silent. Styled in code rather than from a stylesheet so any scene can raise one
///     without owning rules for it.
/// </summary>
public static class MessageDialog
{
    public static ModalLayer Show(UIContext context, Panel root, string message, string buttonLabel = "OK")
    {
        var modal = new ModalLayer(context);
        var dismiss = new Action(() => root.RemoveChild(modal));

        var content = new FlexPanel(context)
        {
            ID = "message-dialog",
            Direction = LayoutDirection.Vertical,
            HorizontalAlign = Align.Center,
            Padding = 20,
            Spacing = 16,
            Background = new ColoredPlane { Color = (0.15f, 0.17f, 0.22f, 1f) },
            Children =
            [
                // TextSlice breaks on '\n'; there is no auto-wrap, so callers line-break the message.
                new Label(context, message) { FontSizePx = 15f },
                new Button(context, buttonLabel, new ColoredPlane { Color = (0.30f, 0.42f, 0.80f, 1f) })
                {
                    OnClick = _ => dismiss()
                }
            ]
        };

        modal.OnDismissRequested = _ => dismiss();
        modal.AddChild(content);
        root.AddChild(modal);
        return modal;
    }
}
