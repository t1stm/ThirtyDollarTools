using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Style.DSL.Abstract.Values;

namespace Sundex.Components.Panels;

/// <summary>
///     A one-button informational modal for messages a scene has to surface but has no
///     place for - a platform refusing an action, a failure that would otherwise be
///     silent. Styled in code rather than from a stylesheet so any scene can raise one
///     without owning rules for it; the look is the editor's dialogs', title and all.
/// </summary>
public static class MessageDialog
{
    // The editor's Theme.snx.ss values (panel, header, text_dim, accent, accent_hover),
    // copied: this component has no sheet to read them from.
    private static readonly Vector4 Frame = new ColorValue("#16161e").Vector;
    private static readonly Vector4 TitleColor = new ColorValue("#7aa2f7").Vector;
    private static readonly Vector4 BodyColor = new ColorValue("#a8b3db").Vector;
    private static readonly Vector4 Accent = new ColorValue("#4c6bcc").Vector;
    private static readonly Vector4 AccentHover = new ColorValue("#6b82c4").Vector;

    public static ModalLayer Show(UIContext context, Panel root, string title, string message,
        string buttonLabel = "OK")
    {
        var modal = new ModalLayer(context);
        var dismiss = new Action(() => root.RemoveChild(modal));

        var fill = new ColoredPlane { Color = Accent };
        var button = new Button(context, buttonLabel, fill)
        {
            FontSizePx = 14f,
            BorderRadius = 6,
            OnClick = _ => dismiss(),
            OnHoverEnter = _ => fill.Color = AccentHover,
            OnHoverExit = _ => fill.Color = Accent
        };

        var content = new FlexPanel(context)
        {
            ID = "message-dialog",
            Direction = LayoutDirection.Vertical,
            // End puts the button flush right, as every editor dialog's actions are. The text
            // column is the widest child, so it spans the frame and still reads left-aligned.
            HorizontalAlign = Align.End,
            Padding = 16,
            Spacing = 16,
            Background = new ColoredPlane { Color = Frame },
            Children =
            [
                new FlexPanel(context)
                {
                    Direction = LayoutDirection.Vertical,
                    Spacing = 8,
                    Children =
                    [
                        new Label(context, title) { FontSizePx = 15f, Color = TitleColor },
                        // TextSlice breaks on '\n'; there is no auto-wrap, so callers line-break the message.
                        new Label(context, message) { FontSizePx = 14f, Color = BodyColor }
                    ]
                },
                button
            ]
        };

        modal.OnDismissRequested = _ => dismiss();
        modal.AddChild(content);
        root.AddChild(modal);
        return modal;
    }
}
