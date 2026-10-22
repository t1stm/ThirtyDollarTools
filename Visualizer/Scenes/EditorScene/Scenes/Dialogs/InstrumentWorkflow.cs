using EditorScene.State;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The instrument selector/editor/delete/reassign cluster: opening the picker, creating,
///     duplicating or editing an instrument, deleting one behind a confirmation, and reassigning the
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
    private readonly EditorPlayback _playback;
    private readonly EditorState _state;
    private Instrument? _editingInstrument;
    private IReadOnlyList<Note>? _reassignTargets;

    public InstrumentWorkflow(UIContext context, EditorState state, EditorPlayback playback,
        DialogHost dialogHost, AtlasStore atlasStore, Func<IEnumerable<Sound>> allSounds)
    {
        _state = state;
        _playback = playback;
        _dialogHost = dialogHost;
        _allSounds = allSounds;

        // Selector and editor are ModalLayers added to and removed from the root rather than
        // panels toggled hidden: only entering and leaving the tree manages the render queue.
        _instrumentSelector = new InstrumentSelector(context, atlasStore);
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
        _instrumentSelector.OnClose = () => _instrumentSelectorModal.OnDismissRequested?.Invoke(_instrumentSelectorModal);
        _instrumentSelector.OnPreview = instrument => playback.PreviewInstrument(instrument);
        _instrumentSelector.OnNew = () =>
        {
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);
            OpenNewInstrument();
        };
        // The copy lands at the end of the list and the selector stays open on it, named the
        // way a duplicated track is.
        _instrumentSelector.OnDuplicate = instrument =>
        {
            var copy = state.AddInstrument($"{instrument.Name} copy");
            state.SetInstrumentSounds(copy, instrument.Sounds);
            state.SetInstrumentModifiers(copy, instrument.Value, instrument.Volume, instrument.Pan, instrument.Offset);
            FillSelector();
        };
        _instrumentSelector.OnEdit = instrument =>
        {
            _editingInstrument = instrument;
            dialogHost.Root.RemoveChild(_instrumentSelectorModal);
            OpenEditor(instrument.Name, instrument);
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
                    FillSelector();
                    dialogHost.Root.AddChild(_instrumentSelectorModal);
                },
                () => dialogHost.Root.AddChild(_instrumentSelectorModal));
        };

        _instrumentEditor = new InstrumentEditor(context, atlasStore);
        _instrumentEditorModal = new ModalLayer(context);
        _instrumentEditorModal.AddChild(_instrumentEditor);
        _instrumentEditorModal.OnDismissRequested = _ => CloseEditor();
        _instrumentEditor.DoneButton.OnClick = _ => Commit();
        _instrumentEditor.OnPreviewSound = sound => playback.PreviewSound(_instrumentEditor.Working, sound);
        _instrumentEditor.PreviewButton.OnClick = _ => Preview();
    }

    /// <summary>
    ///     Opens the picker. With null, picking sets ActiveInstrument; with a list, picking
    ///     reassigns every note in it instead - the inspector's "Change" action, for one note
    ///     or for a whole selection.
    /// </summary>
    public void OpenSelector(IReadOnlyList<Note>? reassignTargets = null)
    {
        _reassignTargets = reassignTargets;
        FillSelector();
        _dialogHost.Root.AddChild(_instrumentSelectorModal);
    }

    /// <summary>
    ///     Fills the picker, marking the active instrument - or, when changing notes', the one they
    ///     all share, if they do.
    /// </summary>
    private void FillSelector()
    {
        var marked = _reassignTargets is { } notes
            ? notes.Select(note => note.Instrument).Distinct().Count() == 1 ? notes[0].Instrument : null
            : _state.ActiveInstrument;

        // Instruments name sounds by ID, or by emoji in older projects.
        var sounds = new Dictionary<string, Sound>();
        foreach (var sound in _allSounds())
        {
            sounds.TryAdd(sound.Id, sound);
            if (sound.Emoji != null) sounds.TryAdd(sound.Emoji, sound);
        }

        _instrumentSelector.Fill(_state.Project.Instruments, marked, sounds, _reassignTargets?.Count);
    }

    /// <summary>
    ///     Opens the editor on a fresh instrument, skipping the selector, for callers that
    ///     already show the instrument list themselves.
    /// </summary>
    public void OpenNewInstrument()
    {
        _editingInstrument = null;
        OpenEditor("Instrument", null);
    }

    /// <summary>
    ///     The play key's job while the instrument editor is open: preview the instrument
    ///     being built, as its Preview button does. False when the editor is closed.
    /// </summary>
    public bool TryPreview()
    {
        if (_instrumentEditorModal.Parent == null) return false;
        Preview();
        return true;
    }

    /// <summary>Previews the editor's working copy - the unsaved instrument, as tuned so far.</summary>
    private void Preview()
    {
        _playback.PreviewInstrument(_instrumentEditor.Working);
    }

    /// <summary>Takes the editor down and drops its cached preview with it.</summary>
    private void CloseEditor()
    {
        _dialogHost.Root.RemoveChild(_instrumentEditorModal);
        _playback.ClearPreviewCache();
    }

    /// <summary>Forwards the held modifiers to the editor, whose scroll gestures and key chips follow them.</summary>
    public void SetModifiers(bool shift, bool ctrl, bool alt)
    {
        _instrumentEditor.SetModifiers(ctrl, shift, alt);
    }

    /// <summary>
    ///     Opens the editor modal on a copy of <paramref name="source" />, or a blank instrument.
    ///     The sound grid is filled before the sounds are seeded, so the picker can map a sound
    ///     stored under its emoji onto the sound's ID.
    /// </summary>
    private void OpenEditor(string name, Instrument? source)
    {
        _instrumentEditor.EnsureSounds(_allSounds());
        _instrumentEditor.Load(name, source);
        _dialogHost.Root.AddChild(_instrumentEditorModal);
    }

    private void Commit()
    {
        var name = string.IsNullOrWhiteSpace(_instrumentEditor.NameInput.Value)
            ? "Instrument"
            : _instrumentEditor.NameInput.Value;

        var working = _instrumentEditor.Working;
        var target = _editingInstrument ?? _state.AddInstrument(name);
        _state.RenameInstrument(target, name);
        _state.SetInstrumentSounds(target, working.Sounds);
        _state.SetInstrumentModifiers(target, working.Value, working.Volume, working.Pan, working.Offset);
        if (_editingInstrument == null) ApplyInstrumentPick(target);

        CloseEditor();
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