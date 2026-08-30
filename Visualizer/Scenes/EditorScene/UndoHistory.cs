namespace EditorScene;

/// <summary>
///     Undo/redo stacks of paired actions. Running a command here does nothing else -
///     <see cref="EditorState" /> owns the follow-up (selection clear, Touch()).
/// </summary>
public sealed class UndoHistory
{
    private readonly List<EditorCommand> _redoStack = [];
    private readonly List<EditorCommand> _undoStack = [];
    private int _gestureId;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    ///     Marks the start of a new drag gesture, so a run of MoveNote/MovePlacement calls
    ///     on the same object across the drag's frames collapses into a single undo step.
    /// </summary>
    public void BeginGesture()
    {
        _gestureId++;
    }

    public void Push(Action undo, Action redo)
    {
        _undoStack.Add(new EditorCommand(undo, redo, -1, null));
        _redoStack.Clear();
    }

    /// <summary>
    ///     Appends a new undo entry, or merges into the previous one if it's the same
    ///     gesture (see <see cref="BeginGesture" />) moving the same object.
    /// </summary>
    public void PushOrMergeMove(object subject, Action undo, Action redo)
    {
        if (_undoStack.Count > 0 && _undoStack[^1].GestureId == _gestureId &&
            ReferenceEquals(_undoStack[^1].Subject, subject))
            _undoStack[^1] = _undoStack[^1] with { Redo = redo };
        else
            _undoStack.Add(new EditorCommand(undo, redo, _gestureId, subject));

        _redoStack.Clear();
    }

    /// <summary>Pops and runs the last undo entry. Returns false when the stack is empty.</summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0) return false;
        var command = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        command.Undo();
        _redoStack.Add(command);
        return true;
    }

    /// <summary>Pops and runs the last undone entry. Returns false when the stack is empty.</summary>
    public bool Redo()
    {
        if (_redoStack.Count == 0) return false;
        var command = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        command.Redo();
        _undoStack.Add(command);
        return true;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private record EditorCommand(Action Undo, Action Redo, int GestureId, object? Subject);
}