using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     What a list shows while it has nothing in it: an icon and one line saying why, centred
///     in the room the list would take. Styled by the empty-state rules in Dialogs.snx.ss.
/// </summary>
public static class EmptyState
{
    /// <param name="icon">A glyph from <see cref="Icons" />.</param>
    /// <param name="stacked">Icon above the text, for a pane; beside it, for a row-high strip.</param>
    public static FlexPanel Build(UIContext context, string icon, string text, bool stacked = true)
    {
        return new FlexPanel(context)
        {
            Classes = stacked ? ["empty-state", "empty-state-stacked"] : ["empty-state"],
            Children =
            [
                new Label(context, icon, Icons.Font)
                    { Classes = stacked ? ["empty-state-icon", "empty-state-icon-large"] : ["empty-state-icon"] },
                new Label(context, text) { Classes = ["empty-state-text"] }
            ]
        };
    }
}
