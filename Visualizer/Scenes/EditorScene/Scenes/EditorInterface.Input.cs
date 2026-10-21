using EditorScene.State;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using EditorScene.Scenes.Views;
using VisualizerScene.Settings;

namespace EditorScene.Scenes;

/// <summary>
///     Keyboard and pointer input the scene feeds in from outside the element tree: held
///     modifiers, the faithful sequence's keys, arrow nudges and middle-drag panning.
/// </summary>
public partial class EditorInterface
{
    /// <summary>
    ///     Whether Space is currently a modifier rather than the play/pause key: held with
    ///     Left/Right it slides the faithful selection along the sequence, which is only a
    ///     gesture while there is a selection to slide. Read by <c>Editor.KeyDown</c> before
    ///     the play/pause bind, so the two never fire on one press.
    /// </summary>
    public bool SpaceMovesSelection =>
        State is { OpenedFaithfulTrack: not null, ActiveTool: EditorTool.Select, SelectedItems.Count: > 0 };

    /// <summary>Space's held state, read every frame from the keyboard - see <see cref="SpaceMovesSelection" />.</summary>
    public void SetSpaceHeld(bool held)
    {
        _faithfulSequence.SpaceHeld = held;
    }

    /// <summary>
    ///     The keys that only mean something with a faithful track open, resolved after the
    ///     bind table has had its go: the arrows (which the table can't match while a
    ///     modifier is held - see <see cref="NudgeDirection" />), Enter, Tab, C and X. Delete is
    ///     the bind table's own now (see <see cref="Editor" />'s KeyDown), for every
    ///     selection rather than only a faithful one. False leaves the key to whatever else
    ///     wants it.
    /// </summary>
    public bool FaithfulKeyDown(KeyboardKeyEventArgs e)
    {
        if (State.OpenedFaithfulTrack is null) return false;

        if (NudgeDirection(e) is { } direction)
        {
            _faithfulSequence.Nudge(direction.Dx, direction.Dy);
            return true;
        }

        switch (e.Key)
        {
            case Keys.Enter or Keys.KeyPadEnter:
                _faithfulSequence.PlaceAgain();
                return true;
            case Keys.Tab:
                _faithfulSequence.AppendAction("!divider");
                return true;
            // Plain C only: Ctrl+C is the copy bind, which matches modifiers exactly and has
            // already returned above, and a focused name field eats plain letters itself.
            case Keys.C:
                _faithfulSequence.AppendAction("!combine");
                return true;
            // Plain X, for the same reason: Ctrl+X is the clipboard cut.
            case Keys.X:
                _faithfulSequence.CutHovered();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    ///     The nudge binds' directions, matched on the key with its modifiers ignored. The
    ///     bind table compares modifier sets exactly, by design - that is what keeps Ctrl+Z
    ///     and Ctrl+Shift+Z apart - while the faithful sequence reads Ctrl/Shift on an arrow
    ///     as volume/pan and Space as a move.
    /// </summary>
    private static (int Dx, int Dy)? NudgeDirection(KeyboardKeyEventArgs e)
    {
        if (e.Key == Keybinds.Get(Bind.EditorNudgeLeft).Key) return (-1, 0);
        if (e.Key == Keybinds.Get(Bind.EditorNudgeRight).Key) return (1, 0);
        if (e.Key == Keybinds.Get(Bind.EditorNudgeUp).Key) return (0, 1);
        if (e.Key == Keybinds.Get(Bind.EditorNudgeDown).Key) return (0, -1);
        return null;
    }

    /// <summary>
    ///     Shift: the note editor snaps values to 0.2 instead of 1, and scrolling a sound
    ///     row in the instrument editor adjusts pan instead of value.
    ///     Ctrl: the arrangement wheel zooms instead of panning, and scrolling a sound row
    ///     in the instrument editor adjusts volume instead of value.
    /// </summary>
    public void SetModifiers(bool shift, bool ctrl, bool alt = false)
    {
        _trackList.CtrlHeld = ctrl;
        _faithfulSequence.CtrlHeld = ctrl;
        _faithfulSequence.ShiftHeld = shift;
        _trackEditor.FineSnap = shift;
        _trackEditor.WheelZooms = ctrl;
        _arrangement.FineSnap = shift;
        _arrangement.WheelZooms = ctrl;
        _instrumentWorkflow.SetModifiers(shift, ctrl, alt);
    }

    /// <summary>
    ///     Arrow-key nudge, routed to whichever view is open: whole steps and values in an
    ///     opened piano-roll track, the snap grid and whole channels in the arrangement (up is
    ///     a lower channel index - the arrangement draws channel 0 at the top). A faithful
    ///     track has no grid to nudge a note across, so there the arrows walk and adjust the
    ///     selection instead - see <see cref="FaithfulSequence.Nudge" />.
    /// </summary>
    public void NudgeSelection(int dx, int dy)
    {
        if (State.OpenedFaithfulTrack != null) _faithfulSequence.Nudge(dx, dy);
        else if (State.OpenedTrack != null) State.NudgeNotes(dx, dy, TrackEditorGeometry.MaxValue);
        else State.NudgePlacements(dx * _arrangement.SnapQuarterNotes, -dy, _arrangement.Channels - 1);
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
        // The framework only routes left/right buttons; middle-drag panning is fed here.
        var middle = mouseState.IsButtonDown(MouseButton.Middle);
        if (NoteEditorOpen) _trackEditor.MiddlePan(middle, _context.PointerX, _context.PointerY);
        else if (_openPanel is null) _arrangement.MiddlePan(middle, _context.PointerX, _context.PointerY);
    }
}
