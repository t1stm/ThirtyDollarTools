using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The instrument selector/editor/delete/reassign cluster: opening the picker,
///     creating/editing an instrument, deleting one (with the confirm dance), and
///     reassigning a single note's instrument. Lifted out of <see cref="EditorInterface" />.
/// </summary>
public sealed class InstrumentWorkflow
{
    private readonly Func<IEnumerable<Sound>> _allSounds;
    private readonly DialogHost _dialogHost;
    private readonly InstrumentEditor _instrumentEditor;
    private readonly ModalLayer _instrumentEditorModal;
    private readonly InstrumentSelector _instrumentSelector;
    private readonly ModalLayer _instrumentSelectorModal;
    private readonly EditorState _state;
    private Instrument? _editingInstrument;
    private IReadOnlyList<Note>? _reassignTargets;

    public InstrumentWorkflow(UIContext context, EditorState state, EditorPlayback playback,
        DialogHost dialogHost, AtlasStore atlasStore, Func<IEnumerable<Sound>> allSounds)
    {
        _state = state;
        _dialogHost = dialogHost;
        _allSounds = allSounds;

        // The instrument selector/editor open as modals (add/remove on the root, the
        // tested show-hide pattern) instead of a DropDownLabel - hidden-panel toggling
        // doesn't manage the render queue.
        _instrumentSelector = new InstrumentSelector(context);
        _instrumentSelectorModal = new ModalLayer(context);
        _instrumentSelectorModal.AddChild(_instrumentSelector);
        _instrumentSelectorModal.OnDismissRequested = modal =>
        {
            dialogHost.Root.RemoveChild(modal);
            _reassignTargets = null;
        };
        _instrumentSelector.OnPick = instrument =>
        {
            ApplyInstrumentPick(instrument);
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);
        };
        _instrumentSelector.OnNew = () =>
        {
            _editingInstrument = null;
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);
            OpenEditor("Instrument", []);
        };
        _instrumentSelector.OnEdit = instrument =>
        {
            _editingInstrument = instrument;
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);
            OpenEditor(instrument.Name, instrument.Sounds);
        };
        _instrumentSelector.OnDelete = instrument =>
        {
            // Both are ModalLayers pinned to the same top z-index, so they'd collide -
            // close the selector while the confirm dialog is up, reopening it after.
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);

            dialogHost.Confirm($"Delete \"{instrument.Name}\"?\n" +
                               "This removes it from every note that uses it.",
                () =>
                {
                    state.DeleteInstrumentEverywhere(instrument);
                    _instrumentSelector.Fill(state.Project.Instruments);
                    dialogHost.Root.AddChild(_instrumentSelectorModal);
                },
                () => dialogHost.Root.AddChild(_instrumentSelectorModal));
        };

        _instrumentEditor = new InstrumentEditor(context, atlasStore);
        _instrumentEditorModal = new ModalLayer(context);
        _instrumentEditorModal.AddChild(_instrumentEditor);
        _instrumentEditorModal.OnDismissRequested = modal => dialogHost.Root.RemoveChild(modal);
        _instrumentEditor.DoneButton.OnClick = _ => Commit();
        _instrumentEditor.SoundsPicker.OnPreviewSound = playback.PreviewSound;
        _instrumentEditor.PreviewButton.OnClick = _ =>
            playback.PreviewInstrument(_instrumentEditor.SoundsPicker.Instances);
    }

    /// <summary>
    ///     Opens the picker; null means picking sets ActiveInstrument, non-null
    ///     means picking reassigns every note in the list instead (one for the inspector's
    ///     single-note "Change", several for a multi-note selection's).
    /// </summary>
    public void OpenSelector(IReadOnlyList<Note>? reassignTargets = null)
    {
        _reassignTargets = reassignTargets;
        _instrumentSelector.Fill(_state.Project.Instruments);
        _dialogHost.Root.AddChild(_instrumentSelectorModal);
    }

    /// <summary>Shift/Ctrl held: forwarded to the editor's sound picker (pan/volume scroll modes).</summary>
    public void SetModifiers(bool shift, bool ctrl)
    {
        _instrumentEditor.SoundsPicker.ShiftHeld = shift;
        _instrumentEditor.SoundsPicker.CtrlHeld = ctrl;
    }

    /// <summary>
    ///     Fills the sound grid before seeding the selection, not after: an older
    ///     project may hold a sound under its emoji, and the picker can only map that to the
    ///     sound's ID once it has been filled.
    /// </summary>
    private void OpenEditor(string name, IEnumerable<InstrumentSound> sounds)
    {
        _instrumentEditor.EnsureSounds(_allSounds());
        _instrumentEditor.Load(name, sounds);
        _dialogHost.Root.AddChild(_instrumentEditorModal);
    }

    private void Commit()
    {
        var name = string.IsNullOrWhiteSpace(_instrumentEditor.NameInput.Value)
            ? "Instrument"
            : _instrumentEditor.NameInput.Value;

        if (_editingInstrument is { } existing)
        {
            _state.RenameInstrument(existing, name);
            _state.SetInstrumentSounds(existing, _instrumentEditor.SoundsPicker.Instances);
        }
        else
        {
            var created = _state.AddInstrument(name);
            _state.SetInstrumentSounds(created, _instrumentEditor.SoundsPicker.Instances);
            ApplyInstrumentPick(created);
        }

        _dialogHost.Root.RemoveChild(_instrumentEditorModal);
        _state.NotifyInstrumentsChanged();
    }

    /// <summary>
    ///     "Picking" an instrument means setting it active, unless the selector was
    ///     opened from the inspector's "Change" action targeting one or more notes -
    ///     then it reassigns every one of them instead.
    /// </summary>
    private void ApplyInstrumentPick(Instrument instrument)
    {
        if (_reassignTargets is { } notes)
        {
            _state.Edit(() =>
            {
                foreach (var note in notes) note.Instrument = instrument;
            });
        }
        else
        {
            _state.ActiveInstrument = instrument;
            // A plain ActiveInstrument set fires no State.On* event of its own - this is
            // the active-instrument button's refresh signal (EditorInterface subscribes
            // RefreshActiveInstrument to OnInstrumentsChanged).
            _state.NotifyInstrumentsChanged();
        }

        _reassignTargets = null;
    }
}