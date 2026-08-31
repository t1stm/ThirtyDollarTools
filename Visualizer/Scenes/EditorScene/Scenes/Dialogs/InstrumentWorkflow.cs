using EditorScene.State;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The instrument selector/editor/delete/reassign cluster: opening the picker, creating
///     or editing an instrument, deleting one behind a confirmation, and reassigning the
///     instrument of the notes the selector was opened for.
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

        // Selector and editor are ModalLayers added to and removed from the root rather than
        // panels toggled hidden: only entering and leaving the tree manages the render queue.
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
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);
            OpenNewInstrument();
        };
        _instrumentSelector.OnEdit = instrument =>
        {
            _editingInstrument = instrument;
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);
            OpenEditor(instrument.Name, instrument.Sounds);
        };
        _instrumentSelector.OnDelete = instrument =>
        {
            // Both modals sit at the same top z-index and would collide, so the selector comes
            // down while the confirm dialog is up and goes back afterwards either way.
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
    ///     Opens the picker. With null, picking sets ActiveInstrument; with a list, picking
    ///     reassigns every note in it instead - the inspector's "Change" action, for one note
    ///     or for a whole selection.
    /// </summary>
    public void OpenSelector(IReadOnlyList<Note>? reassignTargets = null)
    {
        _reassignTargets = reassignTargets;
        _instrumentSelector.Fill(_state.Project.Instruments);
        _dialogHost.Root.AddChild(_instrumentSelectorModal);
    }

    /// <summary>
    ///     Opens the editor on a fresh instrument, skipping the selector, for callers that
    ///     already show the instrument list themselves.
    /// </summary>
    public void OpenNewInstrument()
    {
        _editingInstrument = null;
        OpenEditor("Instrument", []);
    }

    /// <summary>Forwards the held Shift/Ctrl state to the editor's sound picker (pan/volume scroll modes).</summary>
    public void SetModifiers(bool shift, bool ctrl)
    {
        _instrumentEditor.SoundsPicker.ShiftHeld = shift;
        _instrumentEditor.SoundsPicker.CtrlHeld = ctrl;
    }

    /// <summary>
    ///     Opens the editor modal on the given name and sounds. The sound grid is filled
    ///     before the selection is seeded, so the picker can map a sound stored under its
    ///     emoji onto the sound's ID.
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
    ///     Applies a pick: sets the instrument active, or, when the selector was opened with
    ///     reassign targets, assigns it to every one of those notes instead.
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
            // Setting ActiveInstrument raises no State event of its own; this is what makes
            // the active-instrument button refresh.
            _state.NotifyInstrumentsChanged();
        }

        _reassignTargets = null;
    }
}