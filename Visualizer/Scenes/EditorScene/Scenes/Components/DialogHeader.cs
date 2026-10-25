using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     A dialog's first row for the dialogs built in code: the title, an optional fact chip
///     beside it, then the close button flush right - the same tree and classes the markup
///     dialogs spell out (UnsavedChangesDialog.snx.xml). The close button carries the id
///     <c>close-button</c>, which <see cref="Dialogs.DialogHost.Show" /> wires to the modal's
///     dismiss.
/// </summary>
public static class DialogHeader
{
    /// <param name="fact">What the dialog is about, shown in a chip after the title; null for none.</param>
    public static FlexPanel Build(UIContext context, string title, string? fact = null)
    {
        var row = new FlexPanel(context) { Classes = ["dialog-header"] };
        row.AddChild(new Label(context, title) { Classes = ["dialog-title"] });
        if (fact != null)
            row.AddChild(new FlexPanel(context)
            {
                Classes = ["info-chip"],
                Children = [new Label(context, fact) { Classes = ["info-chip-label"] }]
            });
        row.AddChild(new Panel(context) { Classes = ["spacer"] });
        row.AddChild(new Button(context, Icons.X, font: Icons.Font)
        {
            ID = "close-button",
            Classes = ["icon-button"],
            Label = { Classes = ["icon-button-icon"] }
        });
        return row;
    }
}
