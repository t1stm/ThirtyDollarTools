using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Track row context menu (ModalLayer content), opened by right-clicking a track in
///     the track list. Just "duplicate" for now: a name field (prefilled "&lt;name&gt; copy")
///     plus confirm/cancel. Pure view - the owner decides what "duplicate" does.
/// </summary>
public sealed class TrackContextMenu : FlexPanel
{
    public TrackContextMenu(UIContext context, string suggestedName) : base(context)
    {
        ID = "track-context-menu";
        Classes = ["dialog-frame"];

        NameInput = new TextInput(context, suggestedName)
            { ID = "duplicate-name-input", Classes = ["text-field"] };
        // No fill class: Cancel here has always been a bare label, unlike the filled
        // Cancel the other dialogs use.
        CancelButton = new Button(context, "Cancel") { Classes = ["dialog-button-shape"] };
        DuplicateButton = new Button(context, "Duplicate")
        {
            Classes = ["dialog-button-light"],
            Label = { Classes = ["dark-label"] }
        };
        NameInput.OnCommit = _ => DuplicateButton.OnClick?.Invoke(DuplicateButton);

        Children =
        [
            new Label(context, "Duplicate track") { Classes = ["heading-label"] },
            NameInput,
            new FlexPanel(context)
            {
                Classes = ["dialog-actions-compact"],
                Children = [CancelButton, DuplicateButton]
            }
        ];
    }

    public TextInput NameInput { get; }
    public Button CancelButton { get; }
    public Button DuplicateButton { get; }
}