using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using VisualizerScene.Objects.Playfield;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The faithful track's sequence: its items drawn as the expanded TDW event stream, with
///     the playfield's own look and its own scroller - the website's Sequence section.
///     Gestures follow the site where it has one (a left click removes the slot, "!divider"
///     breaks the line) and this editor's own convention where it doesn't (right-click
///     previews, scroll adjusts value, Ctrl+scroll volume, Shift+scroll pan - the same
///     bindings SoundPicker's icons already use).
/// </summary>
public sealed class FaithfulSequence : ScrollView
{
    /// <summary>A playhead jump larger than this is a seek, not a frame's worth of playing.</summary>
    private const double SeekToleranceMinutes = 0.05;

    private const double ValueStep = 1;
    private const float VolumeStep = 5;
    private const float PanStep = 5;

    private readonly EventCanvas _canvas;
    private readonly EditorState _state;

    /// <summary>The item each drawn event belongs to - <see cref="FaithfulTrack.ExpandTagged" />'s left half.</summary>
    private FaithfulItem[] _itemByEvent = [];

    /// <summary>When each slot is played, from <see cref="FaithfulTrack.PlayTimes" />; ordered by time.</summary>
    private (double Minutes, int Index)[] _playTimes = [];

    /// <summary>How far into <see cref="_playTimes" /> the playhead has already been carried.</summary>
    private int _played;

    private double _lastPlayheadMinutes = -1;

    public FaithfulSequence(UIContext context, EditorState state, PlayfieldSettings settings, FaithfulScale scale)
        : base(context)
    {
        _state = state;
        Classes = ["faithful-sequence"];

        _canvas = new EventCanvas(context, settings, FaithfulSizing.SoundSize, FaithfulSizing.Margin)
        {
            Width = LiteralOrComputable.Percent(100),
            // Sixteen across, as on the site, shrinking rather than overflowing when the
            // panel can't hold sixteen full-size boxes.
            PerLine = FaithfulSizing.PerLine,
            FitPerLine = true,
            ReserveBounceRoom = true, // the scroller scissors to its rect; a bounce needs the room
            Scale = scale, // the sequence sets the scale; the palettes follow it
            OnPick = Pick,
            OnPreview = index => Preview(ItemAt(index)),
            OnAdjust = Adjust,
            OnMove = Move,
            // Only under Select: the lit panel says "this is in your selection", and a
            // selection you cannot see the tool for is a blue box that never goes away.
            IsSelected = index => Selecting && ItemAt(index) is { } item && _state.SelectedItems.Contains(item),
            // A layered instrument is several slots but one item - hovering any of them
            // lights all of them.
            GroupOf = index => ItemAt(index)
        };
        AddChild(_canvas);
        _state.OnItemSelectionChanged += _ => _canvas.RefreshSelection();
        // Leaving Select unlights what was selected, so the panels have to be repainted.
        _state.OnToolChanged += _ => _canvas.RefreshSelection();
    }

    /// <summary>Whether the Select tool is the active one - the only tool that selects.</summary>
    private bool Selecting => _state.ActiveTool == EditorTool.Select;

    /// <summary>
    ///     The item the arrow keys work from: the last one added to the selection, so a
    ///     multi-selection walks on from where it was last extended.
    /// </summary>
    private FaithfulItem? Current => _state.SelectedItems.Count > 0 ? _state.SelectedItems[^1] : null;

    /// <summary>
    ///     Whether the view scrolls itself to keep the playing slot visible. Off by default -
    ///     following fights a user who is scrolling somewhere else while the track plays.
    /// </summary>
    public bool FollowScroll { get; set; }

    /// <summary>Same modifier state the rest of the editor reads; see EditorInterface.SetModifiers.</summary>
    public bool CtrlHeld { get; set; }

    public bool ShiftHeld { get; set; }

    /// <summary>Previews a slot's sound as it will sound. Actions have nothing to play and are skipped.</summary>
    public Action<Note>? OnPreviewNote { get; set; }

    /// <summary>Hover hint text, relayed to the hint bar. Null clears it.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>
    ///     Redraws from the opened faithful track. Rebuilding regenerates the chunk's GL
    ///     buffers, so this is only worth calling when the sequence actually changed - which is
    ///     what <see cref="Scenes.EditorInterface" /> does (only while the panel is open).
    ///     ponytail: a whole rebuild per edit, including per scroll notch. Diff the expansion
    ///     and patch the affected renderables if a long sequence ever makes a scroll stutter.
    /// </summary>
    public void Refresh()
    {
        if (_state.OpenedFaithfulTrack is not { } track)
        {
            _itemByEvent = [];
            _canvas.SetEvents([]);
            return;
        }

        var tagged = track.ExpandTagged().ToArray();
        _itemByEvent = [.. tagged.Select(pair => pair.Item)];
        _canvas.SetEvents(tagged.Select(pair => pair.Event));

        _playTimes = [.. track.PlayTimes().OrderBy(entry => entry.Minutes)];
        _played = 0;
        _lastPlayheadMinutes = -1;
    }

    /// <summary>
    ///     Bounces every slot the playhead has passed since the last frame, the way the
    ///     visualizer does as it plays them. Null means the playhead is not inside this track,
    ///     which re-arms the walk so a seek or a second placement starts clean.
    /// </summary>
    public void SetPlayhead(double? localMinutes)
    {
        if (localMinutes is not { } minutes)
        {
            _lastPlayheadMinutes = -1;
            return;
        }

        // A seek (backwards, or a jump forward) re-finds the spot instead of bouncing every
        // slot in between.
        if (minutes < _lastPlayheadMinutes || minutes - _lastPlayheadMinutes > SeekToleranceMinutes)
            _played = Seek(minutes);
        _lastPlayheadMinutes = minutes;

        var last = -1;
        while (_played < _playTimes.Length && _playTimes[_played].Minutes <= minutes)
        {
            last = _playTimes[_played].Index;
            _canvas.Bounce(last);
            _played++;
        }

        if (last >= 0 && FollowScroll) ScrollTo(last);
    }

    /// <summary>
    ///     Where in the schedule a playhead at this position sits. Strictly before, so a slot
    ///     landing exactly on it still bounces - which is every slot at 0 when playback starts.
    /// </summary>
    private int Seek(double minutes)
    {
        var index = 0;
        while (index < _playTimes.Length && _playTimes[index].Minutes < minutes) index++;
        return index;
    }

    /// <summary>Keeps the slot on screen, parking it a line down from the top edge.</summary>
    private void ScrollTo(int eventIndex)
    {
        if (_canvas.OffsetOf(eventIndex) is not { } offset) return;

        // OffsetOf is relative to the canvas's origin, which the scroller has already moved
        // by ScrollY, so the offset is already the slot's position on screen.
        if (offset >= 0 && offset < Computed.Height - FaithfulSizing.SoundSize) return;

        ScrollY = Math.Max(0, offset + ScrollY - FaithfulSizing.SoundSize);
    }

    private FaithfulItem? ItemAt(int eventIndex)
    {
        return eventIndex >= 0 && eventIndex < _itemByEvent.Length ? _itemByEvent[eventIndex] : null;
    }

    /// <summary>
    ///     Under Draw, a click removes the slot as it does on the site. Under Select, a click
    ///     selects it (the primary modifier adds to the selection, and clicking it again drops
    ///     it) - nothing is ever removed by the Select tool, and nothing is ever selected by
    ///     the Draw one.
    /// </summary>
    private void Pick(int eventIndex)
    {
        if (_state.OpenedFaithfulTrack is not { } track || ItemAt(eventIndex) is not { } item) return;

        if (!Selecting)
        {
            _state.RemoveItemAt(track, track.Items.IndexOf(item));
            return;
        }

        if (CtrlHeld || ShiftHeld) _state.ToggleItemSelection(item);
        else _state.SelectItem(item);
    }

    /// <summary>Dragging a slot past another one swaps their order - the site has no such gesture.</summary>
    private void Move(int fromEvent, int toEvent)
    {
        if (_state.OpenedFaithfulTrack is not { } track) return;
        if (ItemAt(fromEvent) is not { } from || ItemAt(toEvent) is not { } to || from == to) return;

        _state.MoveItem(track, track.Items.IndexOf(from), track.Items.IndexOf(to));
    }

    /// <summary>
    ///     Previews a slot. Playback already suppresses previews of its own accord - see
    ///     <see cref="EditorPlayback.PreviewDuringPlayback" /> - so there is no guard here.
    /// </summary>
    private void Preview(FaithfulItem? item)
    {
        if (item?.Note is { } note) OnPreviewNote?.Invoke(note);
    }

    /// <summary>
    ///     Scrolling a slot adjusts it. Under Select it also selects it - you are editing it,
    ///     so the inspector should be showing it - but never under Draw, where a selection
    ///     would only leave a lit panel behind after the pointer has moved on.
    /// </summary>
    private void Adjust(int eventIndex, int notches)
    {
        if (ItemAt(eventIndex) is not { } item) return;

        if (Selecting) _state.SelectItem(item);
        AdjustBy(item, notches);
    }

    /// <summary>
    ///     The arrow keys, under Select - the tool that owns the selection. Left/right walk
    ///     the selection one item along the sequence; up/down adjust the one it is on, with
    ///     the same three meanings the scroll gesture has (value, volume with the primary
    ///     modifier, pan with Shift). With nothing selected yet, right starts at the first
    ///     item and left at the last.
    /// </summary>
    public void Nudge(int dx, int dy)
    {
        if (!Selecting || _state.OpenedFaithfulTrack is not { } track || track.Items.Count == 0) return;

        if (dx != 0)
        {
            var index = Current is { } current
                ? track.Items.IndexOf(current) + dx
                : dx > 0 ? 0 : track.Items.Count - 1;

            var item = track.Items[Math.Clamp(index, 0, track.Items.Count - 1)];
            _state.SelectItem(item);

            // Walking off the visible block would otherwise select something out of sight.
            var slot = Array.IndexOf(_itemByEvent, item);
            if (slot >= 0) ScrollTo(slot);
            return;
        }

        if (dy != 0 && Current is { } selected) AdjustBy(selected, dy);
    }

    /// <summary>
    ///     One notch of the value/volume/pan gesture on an item - the scroll wheel's body and
    ///     the arrow keys'. A sound item's value/volume/pan ride on its <see cref="Note" />;
    ///     an action's single value is what its own notch moves.
    /// </summary>
    private void AdjustBy(FaithfulItem item, int notches)
    {
        // A slot that is mid-bounce is one being played right now; leave it alone rather than
        // retuning a sound under the playhead. Every other slot still adjusts during playback.
        var slot = Array.IndexOf(_itemByEvent, item);
        if (slot >= 0 && _canvas.IsBouncing(slot)) return;

        _state.AdjustItem(item, () =>
        {
            if (item.Note is { } note)
            {
                if (CtrlHeld) note.Volume = Math.Clamp((note.Volume ?? 100) + notches * VolumeStep, 0, 500);
                else if (ShiftHeld) note.Pan = Math.Clamp(note.Pan + notches * PanStep, -100, 100);
                else note.Value = Math.Round(note.Value + notches * ValueStep, 4);
                return;
            }

            if (item.Action is not { } action) return;
            action.Value = Math.Round(action.Value + notches, 4);
            action.WorkingValue = action.Value;
        });

        Preview(item);
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        // The canvas answers a scroll landing on a slot; anything else scrolls the view.
        return _canvas.HandleScroll(scrollDelta) || base.HandleScroll(scrollDelta);
    }
}
