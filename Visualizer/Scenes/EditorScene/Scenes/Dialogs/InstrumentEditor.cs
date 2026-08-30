using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Create-or-edit form for one instrument: a name field and a multi-select sound picker
///     with per-sound value/volume/pan adjustment turned on
///     (see <see cref="SoundPicker.ShowAdjustments" />). Pure form - the owner loads and
///     commits the name and <see cref="SoundPicker.Instances" />, and shows/hides the modal.
/// </summary>
public sealed class InstrumentEditor : FlexPanel
{
    public InstrumentEditor(UIContext context, AtlasStore store) : base(context)
    {
        ID = "instrument-editor";
        Classes = ["dialog-frame"];

        PreviewButton = new Button(context, "Preview") { Classes = ["editor-action-button"] };

        NameInput = new TextInput(context) { ID = "instrument-name-input" };
        // Percent-width spacer soaks up the free space so Preview lands flush against the
        // right edge; this framework has no space-between align.
        var nameRowSpacer = new Panel(context) { Classes = ["spacer"] };
        var nameRow = new FlexPanel(context)
        {
            Classes = ["dialog-row"],
            Children =
                [new Label(context, "Name: ") { Classes = ["heading-label"] }, NameInput, nameRowSpacer, PreviewButton]
        };

        SoundsPicker = new SoundPicker(context, store)
        {
            ID = "instrument-editor-picker",
            MultiSelect = true,
            ShowAdjustments = true
        };
        var soundsList = new ScrollView(context) { ID = "instrument-editor-sounds" };
        soundsList.AddChild(SoundsPicker);

        DoneButton = new Button(context, "Done") { Classes = ["editor-action-button"] };
        var doneRow = new FlexPanel(context) { Classes = ["instrument-editor-done-row"] };
        doneRow.AddChild(DoneButton);

        AddChild(nameRow);
        AddChild(soundsList);
        AddChild(doneRow);
    }

    public TextInput NameInput { get; }
    public Button PreviewButton { get; }
    public SoundPicker SoundsPicker { get; }
    public Button DoneButton { get; }

    /// <summary>Fills the sound grid on first use; a no-op once the picker holds icons.</summary>
    public void EnsureSounds(IEnumerable<Sound> sounds)
    {
        if (SoundsPicker.HasSounds) return;
        SoundsPicker.Fill(sounds);
    }

    /// <summary>Pre-loads the form; pass an empty name and selection to start a fresh instrument.</summary>
    public void Load(string name, IEnumerable<InstrumentSound> sounds)
    {
        NameInput.Value = name;
        SoundsPicker.SetInstances(sounds);
    }
}