using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using Shared.Animations;
using VisualizerScene.Objects;
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

    /// <summary>
    ///     How high a right-click preview hops, against a played slot's full bounce. The site
    ///     nudges a sound when you audition it; a full bounce would read as "this played".
    /// </summary>
    private const float PreviewBounce = 0.35f;

    /// <summary>
    ///     The hop that says a scrolled value moved: half the height of a played bounce, and
    ///     short enough to keep up with a fast scroll instead of trailing behind it.
    /// </summary>
    private const float AdjustBounce = 0.5f;

    private const int AdjustBounceMs = 220;

    private const double ValueStep = 1;
    private const float VolumeStep = 5;
    private const float PanStep = 5;

    private readonly EventCanvas _canvas;
    private readonly EditorState _state;

    /// <summary>The item each drawn event belongs to - <see cref="FaithfulTrack.ExpandTagged" />'s left half.</summary>
    private FaithfulItem[] _itemByEvent = [];

    /// <summary>When each slot is played, from <see cref="FaithfulTrack.PlayTimes" />; ordered by time.</summary>
    private (double Minutes, int Index, BaseEvent Event)[] _playTimes = [];

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
            OnPreview = PreviewAt,
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

    /// <summary>
    ///     Whether Space is down. Held with Left/Right it slides the selected item along the
    ///     sequence instead of walking the selection; <c>Editor.KeyDown</c> stops the same
    ///     press from starting playback - see <see cref="Scenes.EditorInterface.SpaceMovesSelection" />.
    /// </summary>
    public bool SpaceHeld { get; set; }

    /// <summary>Previews a slot's sound as it will sound. Actions have nothing to play and are skipped.</summary>
    public Action<Note>? OnPreviewNote { get; set; }

    /// <summary>Hover hint text, relayed to the hint bar. Null clears it.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>
    ///     Right-clicking an action that carries a value asks for it to be edited, the way the
    ///     site reopens the action's form. <c>EditorInterface</c> owns the dialog.
    /// </summary>
    public Action<FaithfulItem>? OnEditAction { get; set; }

    /// <summary>
    ///     Redraws from the opened faithful track. Rebuilding regenerates the chunk's GL
    ///     buffers, so this is only worth calling when the sequence actually changed - which is
    ///     what <see cref="Scenes.EditorInterface" /> does (only while the panel is open).
    ///     The rebuild itself is incremental - see <see cref="EventCanvas.SetEvents" />, which
    ///     only regenerates the chunks whose slots actually draw differently.
    /// </summary>
    public void Refresh()
    {
        if (_state.OpenedFaithfulTrack is not { } track)
        {
            _itemByEvent = [];
            _canvas.SetEvents([]);
            return;
        }

        // Split in one pass: this runs after every edit on a stream tens of thousands of slots
        // long, and the two LINQ passes it replaces built two more arrays of that.
        var tagged = track.ExpandTagged().ToArray();
        var items = new FaithfulItem[tagged.Length];
        var events = new BaseEvent[tagged.Length];
        for (var i = 0; i < tagged.Length; i++) (items[i], events[i]) = tagged[i];
        _itemByEvent = items;

        // Only when the edit could have moved something: a sound's value, volume or pan
        // changes what a slot draws and nothing about when it plays, and walking the whole
        // sequence for a schedule that is still correct costs as much as the redraw did.
        if (!_canvas.SetEvents(events)) return;

        _playTimes = [.. track.PlayTimes().OrderBy(entry => entry.Minutes)];
        _played = 0;
        _lastPlayheadMinutes = -1;
    }

    /// <summary>
    ///     Animates every slot the playhead has passed since the last frame, the way the
    ///     visualizer does as it plays them - see <see cref="Play" />. Null means the playhead
    ///     is not inside this track, which re-arms the walk so a seek or a second placement
    ///     starts clean.
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
            var (_, index, ev) = _playTimes[_played];
            last = index;
            Play(index, ev);
            _played++;
        }

        if (last >= 0 && FollowScroll) ScrollTo(last);
    }

    /// <summary>
    ///     What the playfield does to a slot as it is played, straight out of
    ///     <c>PlayfieldContainer</c>'s subscriptions: a sound bounces, an action fades and
    ///     expands, and the two actions that carry a countdown rewrite their number first.
    /// </summary>
    private void Play(int index, BaseEvent ev)
    {
        switch (ev.SoundEvent)
        {
            // Both of these fade and expand of their own accord - see SoundRenderable.SetValue.
            case "!loopmany":
                _canvas.SetValue(index, ev, ValueChangeWrapMode.RemoveTexture);
                return;
            case "!stop":
                _canvas.SetValue(index, ev, ValueChangeWrapMode.ResetToDefault);
                return;
        }

        if ((ev.SoundEvent?.StartsWith('!') ?? true) || ev is ICustomActionEvent) _canvas.FadeExpand(index);
        else _canvas.Bounce(index);
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

    /// <summary>
    ///     Keeps the slot on screen, parking it a line down from the top edge - in either
    ///     direction, so replaying a track scrolls back up to its first slot.
    ///     <see cref="EventCanvas.OffsetOf" /> is measured from the canvas's own top, which is
    ///     the content, not the screen: the scroller moves the canvas by <see cref="ScrollY" />,
    ///     so the offset does not change as the view scrolls and subtracting ScrollY is what
    ///     turns it into a position inside the viewport.
    /// </summary>
    private void ScrollTo(int eventIndex)
    {
        if (_canvas.OffsetOf(eventIndex) is not { } offset) return;

        var line = _canvas.LineHeight;
        var onScreen = offset - ScrollY;
        if (onScreen >= 0 && onScreen + line <= Computed.Height) return;

        // The setter clamps to [0, MaxScroll], so the first line needs no special case.
        ScrollY = offset - line;
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
    ///     The right-click gesture: a sound hops as it is auditioned, and an action carrying a
    ///     value opens its dialog instead - both are what the site does with that button, and
    ///     the dialog is the only way to reach a packed "!bg"/"!pulse" payload.
    /// </summary>
    private void PreviewAt(int eventIndex)
    {
        if (ItemAt(eventIndex) is not { } item) return;

        if (item.Action is { } action && FaithfulAction.TakesValue(action.SoundEvent))
        {
            OnEditAction?.Invoke(item);
            return;
        }

        BounceItem(item, PreviewBounce);
        Preview(item);
    }

    /// <summary>
    ///     Bounces every slot an item occupies - a layered instrument is several of them, and
    ///     they all move together because they are one thing to the user.
    /// </summary>
    private void BounceItem(FaithfulItem item, float scale, int lengthMs = BounceAnimation.DefaultLengthMs)
    {
        for (var index = 0; index < _itemByEvent.Length; index++)
            if (_itemByEvent[index] == item)
                _canvas.Bounce(index, scale, lengthMs);
    }

    /// <summary>Scrolls the item into view, wherever the sequence has drawn it.</summary>
    private void ScrollToItem(FaithfulItem item)
    {
        var slot = Array.IndexOf(_itemByEvent, item);
        if (slot >= 0) ScrollTo(slot);
    }

    /// <summary>
    ///     Scrolling a slot adjusts it. Under Select it also selects it - you are editing it,
    ///     so the inspector should be showing it - but never under Draw, where a selection
    ///     would only leave a lit panel behind after the pointer has moved on.
    ///     False when the slot has no value to turn ("!divider", "_pause", the packed
    ///     "!bg"/"!pulse"): the wheel then belongs to the view, as it does on the site.
    /// </summary>
    private bool Adjust(int eventIndex, int notches)
    {
        if (ItemAt(eventIndex) is not { } item || !Adjustable(item)) return false;

        if (Selecting) _state.SelectItem(item);
        AdjustBy(item, notches);
        return true;
    }

    /// <summary>Whether the scroll gesture (and the up/down keys) mean anything on this slot.</summary>
    private static bool Adjustable(FaithfulItem item)
    {
        if (item.Note is not null) return true;
        return item.Action is { } action && FaithfulAction.ScrollRangeFor(action) is not null;
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
            if (SpaceHeld) MoveSelected(track, dx);
            else if (CtrlHeld && ShiftHeld) ExtendSelection(track, dx);
            else WalkSelection(track, dx);
            return;
        }

        if (dy != 0 && Current is { } selected && Adjustable(selected)) AdjustBy(selected, dy);
    }

    /// <summary>Left/Right on their own move the selection one item along, clamping at either end.</summary>
    private void WalkSelection(FaithfulTrack track, int dx)
    {
        var index = Current is { } current
            ? track.Items.IndexOf(current) + dx
            : dx > 0 ? 0 : track.Items.Count - 1;

        var item = track.Items[Math.Clamp(index, 0, track.Items.Count - 1)];
        _state.SelectItem(item);
        // Walking off the visible block would otherwise select something out of sight.
        ScrollToItem(item);
    }

    /// <summary>
    ///     Primary+Shift+Left/Right grows the selection one item at a time - the keyboard's
    ///     version of Ctrl-clicking each of them in turn. Turning back around shrinks it
    ///     again rather than stalling on an item that is already in.
    /// </summary>
    private void ExtendSelection(FaithfulTrack track, int dx)
    {
        var index = Current is { } current
            ? track.Items.IndexOf(current) + dx
            : dx > 0 ? 0 : track.Items.Count - 1;
        if (index < 0 || index >= track.Items.Count) return;

        var next = track.Items[index];
        var selection = _state.SelectedItems.ToList();

        // Walking back onto the item added before the last one: drop the last instead of
        // re-adding what is already there.
        if (selection.Count > 1 && selection[^2] == next) selection.RemoveAt(selection.Count - 1);
        else
        {
            selection.Remove(next);
            selection.Add(next);
        }

        _state.SetItemSelection(selection);
        ScrollToItem(next);
    }

    /// <summary>
    ///     Space+Left/Right slides the selected item along the sequence - the keyboard's
    ///     version of dragging it. One item, not the whole selection: each would otherwise be
    ///     its own undo entry, exactly as with the up/down adjust.
    /// </summary>
    private void MoveSelected(FaithfulTrack track, int dx)
    {
        if (Current is not { } current) return;

        var from = track.Items.IndexOf(current);
        var to = from + dx;
        if (to < 0 || to >= track.Items.Count) return;

        _state.MoveItem(track, from, to);
        ScrollToItem(current);
    }

    /// <summary>
    ///     Enter lays down another copy of the selected slot right after it - the site's
    ///     "click the sound again" without a trip back to the palette. The copy becomes the
    ///     selection, so a held Enter draws a run.
    /// </summary>
    public void PlaceAgain()
    {
        if (!Selecting || _state.OpenedFaithfulTrack is not { } track || Current is not { } current) return;

        var copy = current.Duplicate();
        _state.InsertItemAt(track, copy, track.Items.IndexOf(current) + 1);
        _state.SelectItem(copy);
        ScrollToItem(copy);
    }

    /// <summary>
    ///     Tab breaks the line after the last slot. "!divider" is the site's own line break
    ///     and appending one is the only thing it is ever used for, so it needs no dialog.
    /// </summary>
    public void AppendDivider()
    {
        if (_state.OpenedFaithfulTrack is not { } track) return;
        if (FaithfulItem.Parse("!divider") is { } divider) _state.AppendItem(track, divider);
    }

    /// <summary>
    ///     One notch of the value/volume/pan gesture on an item - the scroll wheel's body and
    ///     the arrow keys'. A sound item's value/volume/pan ride on its <see cref="Note" />;
    ///     an action's single value is what its own notch moves.
    /// </summary>
    private void AdjustBy(FaithfulItem item, int notches)
    {
        // A slot that is mid-bounce while the playhead is inside this track is one being
        // played right now; leave it alone rather than retuning a sound under the playhead.
        // Only while playing: an adjust bounces the slot itself (below), and that must not
        // lock out the next notch of the same gesture.
        var slot = Array.IndexOf(_itemByEvent, item);
        if (_lastPlayheadMinutes >= 0 && slot >= 0 && _canvas.IsBouncing(slot)) return;

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
            if (FaithfulAction.ScrollRangeFor(action) is not { } range) return;

            // The site's own bounds and notch size for this action - a "!loopmany" never goes
            // below one pass, and a "!speed@2@x" moves in tenths because it is a factor.
            action.Value = Math.Clamp(Math.Round(action.Value + notches * range.Step, 4), range.Min, range.Max);
            action.WorkingValue = action.Value;
        });

        Preview(item);
        // Up bounces up, down bounces down - the site's own feedback that the value moved.
        // After the edit: the adjustment rebuilds the block, which drops a running bounce.
        BounceItem(item, Math.Sign(notches) * AdjustBounce, AdjustBounceMs);
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        // The canvas answers a scroll landing on a slot; anything else scrolls the view.
        return _canvas.HandleScroll(scrollDelta) || base.HandleScroll(scrollDelta);
    }
}
