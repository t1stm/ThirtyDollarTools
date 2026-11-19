using Sundex.Engine;
using System.Buffers;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Core.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Abstract.Extensions;

namespace Sundex.Components.Inputs;

/// <summary>Restricts what characters a <see cref="TextInput" /> accepts.</summary>
public enum TextInputFilter
{
    None,

    /// <summary>Digits and a leading minus.</summary>
    Integer,

    /// <summary>Digits, a leading minus, and a single decimal point.</summary>
    Decimal
}

/// <summary>
///     Text input with caret, selection (keyboard + mouse drag), undo, the clipboard and numeric
///     filter modes. Single-line by default, scrolling sideways to keep the caret in view; with
///     <see cref="Multiline" /> the text wraps to the box, Enter starts a new line, Up and Down move
///     between lines and the box scrolls down instead.
///     Receives input through the focus system: <see cref="UIContext.DispatchTextInput" />
///     and <see cref="UIContext.DispatchKeyDown" />.
/// </summary>
// ponytail: caret/selection indices are char-based; surrogate pairs can be split by
// arrow keys/backspace. Fine for project names and numbers; revisit if emoji matter.
[Sundex.Components.Attributes.MarkupTag("text-input")]
public class TextInput : Panel
{
    private readonly ColoredPlane _caretPlane = new() { Color = Vector4.One };
    private readonly Label _label;
    private readonly Label _placeholder;
    private readonly ColoredPlane _selectionPlane = new() { Color = new Vector4(0.3f, 0.5f, 0.9f, 0.45f) };

    // A selection over several lines is three boxes: the rest of the first line, the whole lines
    // between, and the start of the last.
    private readonly ColoredPlane _selectionMiddle = new() { Color = new Vector4(0.3f, 0.5f, 0.9f, 0.45f) };
    private readonly ColoredPlane _selectionLast = new() { Color = new Vector4(0.3f, 0.5f, 0.9f, 0.45f) };

    private int _anchor;
    private Vector4i? _inheritedClip;

    // The pointer keeps delivering drags while the button is held after a double-click;
    // don't let them collapse the word selection.
    private bool _suppressDragSelection;
    private string _text;

    public TextInput(UIContext context, string value = "") : base(context)
    {
        _label = new Label(context, value)
        {
            Parent = this,
            FontSizePx = 15f
        };
        _placeholder = new Label(context, "")
        {
            Parent = this,
            FontSizePx = 15f,
            Color = FieldColors.Placeholder
        };
        Children = [_label, _placeholder];
        BorderRadius = 4;
        // The panel's own fill, not a plane of its own: a sheet's fill replaces it, transparent
        // included, where a transparent fill left a separate plane showing.
        Fill = FieldColors.Fill;

        Focusable = true;
        Cursor = CursorType.Text;
        Padding = 5;
        Height = 32;
        _text = Sanitize(value);
        CaretIndex = _anchor = _text.Length;
    }

    public override string Tag => "text-input";

    public string Value
    {
        get => _text;
        set
        {
            var clean = Sanitize(value);
            // A two-way binding writes the text straight back: that must not collapse the selection.
            if (clean == _text) return;
            _undo.Clear();
            _redo.Clear();
            SetText(clean);
        }
    }

    /// <summary>Shows each character as a dot. Copy and cut do nothing while set.</summary>
    [NamedSetting("password")]
    public bool Password
    {
        get;
        set
        {
            field = value;
            _label.Value = Display;
            InvalidateLayout();
        }
    }

    /// <summary>The most characters the field takes; 0 for no limit.</summary>
    [NamedSetting("max-length")]
    public int MaxLength { get; set; }

    [NamedSetting("font")]
    public string? Font
    {
        get => _label.Font;
        set => _label.Font = _placeholder.Font = value;
    }

    private string Display => Password ? new string('•', _text.Length) : _text;

    public TextInputFilter Filter { get; set; } = TextInputFilter.None;

    public int CaretIndex { get; private set; }

    public int SelectionStart => Math.Min(CaretIndex, _anchor);
    public int SelectionEnd => Math.Max(CaretIndex, _anchor);
    public bool HasSelection => CaretIndex != _anchor;

    /// <summary>Horizontal scroll offset in pixels keeping the caret visible.</summary>
    public float ScrollX { get; private set; }

    /// <summary>A multi-line box's vertical scroll, in pixels.</summary>
    public float ScrollY
    {
        get;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxScrollY);
            if (Math.Abs(field - clamped) < 0.01f) return;
            field = clamped;
            InvalidateLayout();
        }
    }

    /// <summary>Wraps to the box and takes Enter as a new line: a <c>&lt;textarea&gt;</c>.</summary>
    [NamedSetting("multiline")]
    public bool Multiline
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            _label.Wrap = value;
            if (!value) SetText(Sanitize(_text));
            InvalidateMeasure();
        }
    }

    /// <summary>A key the box's owner wants first: return true to take it from the field.</summary>
    public Func<KeyboardKeyEventArgs, bool>? OnKey { get; set; }

    /// <summary>Where the caret before <paramref name="index" /> sits, from the text's top left, unscrolled.</summary>
    public (float X, float Y) CaretPoint(int index) => _label.CaretAt(Math.Clamp(index, 0, _text.Length));

    /// <summary>One line's height, in pixels.</summary>
    public float TextLineHeight => LineHeight;

    /// <summary>How far in from the box's top left the text starts.</summary>
    public (float X, float Y) TextOrigin => (Padding, Padding + TextTop);

    /// <summary>Puts the caret at <paramref name="index" />, collapsing the selection.</summary>
    public void MoveCaret(int index) => SetCaret(index, false);

    private float ContentHeight => _label.CaretAt(_text.Length).Y + LineHeight;
    private float InnerHeight => Math.Max(0, Computed.Height - 2 * Padding);
    private float MaxScrollY => Multiline ? Math.Max(0, ContentHeight - InnerHeight) : 0;

    [NamedSetting("font-size")]
    public LiteralOrComputable FontSizePx
    {
        get => _label.FontSizePx;
        set => _label.FontSizePx = _placeholder.FontSizePx = value;
    }

    /// <summary>Text shown, dimmed, while the field is empty - a browser input's placeholder.</summary>
    [NamedSetting("placeholder")]
    public string Placeholder
    {
        get;
        set
        {
            field = value;
            RefreshPlaceholder();
        }
    } = "";

    [NamedSetting("placeholder-color")]
    public Vector4 PlaceholderColor
    {
        get => _placeholder.Color;
        set => _placeholder.Color = value;
    }

    [NamedSetting("font-color")]
    public Vector4 TextColor
    {
        get => _label.Color;
        set => _label.Color = value;
    }

    public Vector4 CaretColor
    {
        get => _caretPlane.Color;
        set => _caretPlane.Color = value;
    }

    public Vector4 SelectionColor
    {
        get => _selectionPlane.Color;
        set => _selectionPlane.Color = _selectionMiddle.Color = _selectionLast.Color = value;
    }

    public Action<TextInput>? OnValueChanged { get; set; }
    public Action<TextInput>? OnCommit { get; set; }

    private float FontSize => _label.FontSizePx.Resolve(14);
    private float LineHeight => _label.LinePixels;

    /// <summary>Width at the right end of the content area not available to text (e.g. spinner buttons).</summary>
    protected virtual float ReservedRightWidth => 0;

    #region Input handling

    public override void HandleTextInput(TextInputEventArgs args)
    {
        // A run of typing is one undo step; a space or a pause ends it.
        Remember(args.AsString != " ");
        Insert(args.AsString);
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs args)
    {
        if (OnKey?.Invoke(args) == true) return true;
        switch (args.Key)
        {
            case Keys.Up when Multiline:
            case Keys.Down when Multiline:
            {
                var (x, y) = _label.CaretAt(CaretIndex);
                var target = y + (args.Key == Keys.Up ? -0.5f : 1.5f) * LineHeight;
                SetCaret(target < 0 ? 0 : _label.IndexAt(x, target), args.Shift);
                return true;
            }

            case Keys.Home when Multiline && !args.Control:
                SetCaret(CaretIndex > 0 ? _text.LastIndexOf('\n', CaretIndex - 1) + 1 : 0, args.Shift);
                return true;

            case Keys.End when Multiline && !args.Control:
            {
                var end = _text.IndexOf('\n', CaretIndex);
                SetCaret(end < 0 ? _text.Length : end, args.Shift);
                return true;
            }

            case Keys.Left:
                if (!args.Shift && HasSelection) SetCaret(SelectionStart, false);
                else SetCaret(CaretIndex - 1, args.Shift);
                return true;

            case Keys.Right:
                if (!args.Shift && HasSelection) SetCaret(SelectionEnd, false);
                else SetCaret(CaretIndex + 1, args.Shift);
                return true;

            case Keys.Home:
                SetCaret(0, args.Shift);
                return true;

            case Keys.End:
                SetCaret(_text.Length, args.Shift);
                return true;

            case Keys.Backspace:
                Remember(false);
                if (HasSelection) RemoveSelection();
                else if (CaretIndex > 0) SetText(_text.Remove(CaretIndex - 1, 1), CaretIndex - 1);

                return true;

            case Keys.Delete:
                Remember(false);
                if (HasSelection) RemoveSelection();
                else if (CaretIndex < _text.Length) SetText(_text.Remove(CaretIndex, 1), CaretIndex);
                return true;

            // ponytail: inline rather than a Sundex-wide modifier abstraction - select-all in
            // a text field has to work with Cmd on a Mac, and it isn't a visualizer keybind.
            case Keys.A when args.Control || args.Command:
                SelectAll();
                return true;

            case Keys.C when args.Control || args.Command:
                if (HasSelection && !Password) Clipboard.Write(SelectedText);
                return true;

            case Keys.X when args.Control || args.Command:
                if (!HasSelection || Password) return true;
                Clipboard.Write(SelectedText);
                Remember(false);
                RemoveSelection();
                return true;

            case Keys.V when args.Control || args.Command:
                Remember(false);
                Insert(Clipboard.Read());
                return true;

            case Keys.Z when (args.Control || args.Command) && args.Shift:
            case Keys.Y when args.Control || args.Command:
                Redo();
                return true;

            case Keys.Z when args.Control || args.Command:
                Undo();
                return true;

            case Keys.Enter or Keys.KeyPadEnter when Multiline && !args.Control:
                Remember(false);
                Insert("\n");
                return true;

            case Keys.Enter or Keys.KeyPadEnter:
                OnCommit?.Invoke(this);
                Context.Blur();
                return true;

            default:
                // A focused field owns plain keystrokes - otherwise Space/letters fall
                // through to scene hotkeys mid-typing. Modified combos and Escape
                // (the blur path) stay app-level.
                return args is { Control: false, Alt: false, Command: false } && args.Key != Keys.Escape;
        }
    }

    public override bool HandlePress(float x, float y)
    {
        _suppressDragSelection = false;
        SetCaret(CaretIndexAt(x, y), false);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_suppressDragSelection) return;
        SetCaret(CaretIndexAt(x, y), true);
    }

    public override bool HandleScroll(Vector2 scrollDelta)
    {
        if (!Multiline || MaxScrollY <= 0) return false;
        ScrollY -= scrollDelta.Y * 3 * LineHeight;
        return true;
    }

    /// <summary>Double-click selects the run of letters/digits under the pointer.</summary>
    public override bool HandleDoublePress(float x, float y)
    {
        _suppressDragSelection = true;
        if (_text.Length == 0) return true;

        var index = Math.Clamp(CaretIndexAt(x, y), 0, _text.Length - 1);
        if (!char.IsLetterOrDigit(_text[index])) return true; // punctuation/gaps just keep the caret

        var start = index;
        var end = index;
        while (start > 0 && char.IsLetterOrDigit(_text[start - 1])) start--;
        while (end < _text.Length && char.IsLetterOrDigit(_text[end])) end++;

        _anchor = start;
        CaretIndex = end;
        AfterCaretMove();
        return true;
    }

    #endregion

    #region Editing

    public void Insert(ReadOnlySpan<char> input)
    {
        var remaining = HasSelection ? _text.Remove(SelectionStart, SelectionEnd - SelectionStart) : _text;
        var caret = HasSelection ? SelectionStart : CaretIndex;

        // Keystrokes are 1-2 chars; only a large paste rents from the pool.
        var rented = input.Length > 256 ? ArrayPool<char>.Shared.Rent(input.Length) : null;
        try
        {
            var accepted = rented ?? stackalloc char[256];
            var count = 0;
            foreach (var character in input)
            {
                if (character == '\r' || (character == '\n' && !Multiline)) continue;
                if (!AllowsCharacter(character, remaining, caret + count)) continue;
                accepted[count++] = character;
            }

            if (MaxLength > 0) count = Math.Min(count, Math.Max(0, MaxLength - remaining.Length));
            if (count == 0 && remaining == _text) return;

            SetText(remaining.Insert(caret, new string(accepted[..count])), caret + count);
        }
        finally
        {
            if (rented != null) ArrayPool<char>.Shared.Return(rented);
        }
    }

    public void SelectAll()
    {
        _anchor = 0;
        CaretIndex = _text.Length;
        AfterCaretMove();
    }

    /// <summary>The selected text, or empty.</summary>
    public string SelectedText => HasSelection ? _text[SelectionStart..SelectionEnd] : "";

    private readonly List<(string Text, int Caret, int Anchor)> _undo = [];
    private readonly List<(string Text, int Caret, int Anchor)> _redo = [];
    private bool _typingRun;
    private long _lastTyped;

    /// <summary>Saves the text before an edit. Consecutive typing within a second is one step.</summary>
    private void Remember(bool typing)
    {
        var now = Environment.TickCount64;
        if (typing && _typingRun && now - _lastTyped < 1000)
        {
            _lastTyped = now;
            return;
        }

        _undo.Add((_text, CaretIndex, _anchor));
        if (_undo.Count > 200) _undo.RemoveAt(0);
        _redo.Clear();
        _typingRun = typing;
        _lastTyped = now;
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Add((_text, CaretIndex, _anchor));
        Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add((_text, CaretIndex, _anchor));
        Restore(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
    }

    private void Restore((string Text, int Caret, int Anchor) state)
    {
        _typingRun = false;
        SetText(state.Text, state.Caret);
        _anchor = Math.Clamp(state.Anchor, 0, _text.Length);
        AfterCaretMove();
    }

    private void RemoveSelection()
    {
        SetText(_text.Remove(SelectionStart, SelectionEnd - SelectionStart), SelectionStart);
    }

    private bool AllowsCharacter(char character, string remaining, int position)
    {
        switch (Filter)
        {
            case TextInputFilter.None:
                return true;

            case TextInputFilter.Integer:
            case TextInputFilter.Decimal:
                if (char.IsAsciiDigit(character)) return true;
                if (character == '-') return position == 0 && !remaining.Contains('-');
                if (character == '.' && Filter == TextInputFilter.Decimal) return !remaining.Contains('.');
                return false;

            default:
                return false;
        }
    }

    private void SetCaret(int index, bool extendSelection)
    {
        CaretIndex = Math.Clamp(index, 0, _text.Length);
        if (!extendSelection) _anchor = CaretIndex;
        AfterCaretMove();
    }

    private void SetText(string text, int? caret = null)
    {
        var changed = text != _text;
        _text = text;
        CaretIndex = Math.Clamp(caret ?? CaretIndex, 0, _text.Length);
        _anchor = CaretIndex;
        _label.Value = Display;
        RefreshPlaceholder();
        AfterCaretMove();
        if (changed) OnValueChanged?.Invoke(this);
    }

    /// <summary>The placeholder stays in the tree and goes blank while there is text, so nothing re-queues.</summary>
    private void RefreshPlaceholder()
    {
        _placeholder.SetTextContents(_text.Length == 0 ? Placeholder : "");
    }

    private void AfterCaretMove()
    {
        ScrollCaretIntoView();
        InvalidateLayout();
    }

    private string Sanitize(string value)
    {
        return Multiline ? value.Replace("\r", "") : value.Replace("\n", "").Replace("\r", "");
    }

    #endregion

    #region Measurement / layout

    private void ScrollCaretIntoView()
    {
        if (Multiline)
        {
            var innerHeight = InnerHeight;
            if (innerHeight <= 0) return;
            var (_, caretY) = _label.CaretAt(CaretIndex);
            if (caretY + LineHeight - ScrollY > innerHeight) ScrollY = caretY + LineHeight - innerHeight;
            if (caretY - ScrollY < 0) ScrollY = caretY;
            ScrollY = ScrollY;
            return;
        }

        var innerWidth = Math.Max(0, Computed.Width - 2 * Padding - ReservedRightWidth);
        if (innerWidth <= 0) return;

        var caretX = MeasurePrefix(CaretIndex);
        if (caretX - ScrollX > innerWidth) ScrollX = caretX - innerWidth;
        if (caretX - ScrollX < 0) ScrollX = caretX;

        var totalWidth = MeasurePrefix(_text.Length);
        ScrollX = Math.Clamp(ScrollX, 0, Math.Max(0, totalWidth - innerWidth));
    }

    /// <summary>Pixel width of the first <paramref name="length" /> characters, in the label's own font.</summary>
    public float MeasurePrefix(int length) => _label.CaretAt(Math.Clamp(length, 0, _text.Length)).X;

    /// <summary>Caret index closest to an absolute point: the line under it in a multi-line box.</summary>
    public int CaretIndexAt(float absoluteX, float absoluteY)
    {
        if (!Multiline) return CaretIndexFromX(absoluteX);
        var localX = absoluteX - (Computed.AbsoluteX + Padding);
        var localY = absoluteY - (Computed.AbsoluteY + Padding) + ScrollY;
        return Math.Clamp(_label.IndexAt(Math.Max(0, localX), Math.Max(0, localY)), 0, _text.Length);
    }

    /// <summary>Caret index closest to the given absolute X coordinate.</summary>
    public int CaretIndexFromX(float absoluteX)
    {
        var local = absoluteX - (Computed.AbsoluteX + Padding - ScrollX);
        return local <= 0 ? 0 : Math.Clamp(_label.IndexAt(local, 0), 0, _text.Length);
    }

    /// <summary>
    ///     How far below the padding the text sits, so a line is centred in the box like a browser's input.
    ///     Negative when the padding leaves less room than a line: a browser centres the line all the same.
    /// </summary>
    private float TextTop => Multiline ? 0 : (Computed.Height - 2 * Padding - LineHeight) / 2;

    protected override void DoLayout()
    {
        if (Multiline)
        {
            LayoutMultiline();
            return;
        }

        _label.X = -ScrollX;
        _label.Y = _placeholder.Y = TextTop;
        base.DoLayout();

        var textX = Computed.AbsoluteX + Padding - ScrollX;
        var textY = Computed.AbsoluteY + Padding + TextTop;

        var caretX = textX + MeasurePrefix(CaretIndex);
        _caretPlane.SetPosition((caretX, textY, 0));
        _caretPlane.Scale = IsFocused ? new Vector3(1.5f, LineHeight, 1) : Vector3.Zero;

        var selectionX = MeasurePrefix(SelectionStart);
        var selectionWidth = MeasurePrefix(SelectionEnd) - selectionX;
        _selectionPlane.SetPosition((textX + selectionX, textY, 0));
        _selectionPlane.Scale = HasSelection && IsFocused
            ? new Vector3(selectionWidth, LineHeight, 1)
            : Vector3.Zero;

        ApplyClip(_inheritedClip);
    }

    private void LayoutMultiline()
    {
        _label.X = 0;
        _label.Y = -ScrollY;
        _placeholder.Y = 0;
        base.DoLayout();
        ScrollY = ScrollY;

        var textX = Computed.AbsoluteX + Padding;
        var textY = Computed.AbsoluteY + Padding - ScrollY;
        var (caretX, caretY) = _label.CaretAt(CaretIndex);
        _caretPlane.SetPosition((textX + caretX, textY + caretY, 0));
        _caretPlane.Scale = IsFocused ? new Vector3(1.5f, LineHeight, 1) : Vector3.Zero;

        var show = HasSelection && IsFocused;
        var (startX, startY) = _label.CaretAt(SelectionStart);
        var (endX, endY) = _label.CaretAt(SelectionEnd);
        var width = Math.Max(0, Computed.Width - 2 * Padding);
        if (!show || Math.Abs(startY - endY) < 0.5f)
        {
            _selectionPlane.SetPosition((textX + startX, textY + startY, 0));
            _selectionPlane.Scale = show ? new Vector3(endX - startX, LineHeight, 1) : Vector3.Zero;
            _selectionMiddle.Scale = _selectionLast.Scale = Vector3.Zero;
        }
        else
        {
            _selectionPlane.SetPosition((textX + startX, textY + startY, 0));
            _selectionPlane.Scale = new Vector3(Math.Max(2, width - startX), LineHeight, 1);
            _selectionMiddle.SetPosition((textX, textY + startY + LineHeight, 0));
            _selectionMiddle.Scale = new Vector3(width, Math.Max(0, endY - startY - LineHeight), 1);
            _selectionLast.SetPosition((textX, textY + endY, 0));
            _selectionLast.Scale = new Vector3(endX, LineHeight, 1);
        }

        ApplyClip(_inheritedClip);
    }

    /// <summary>Clips its own content (text/caret/selection) to the box, intersected with any outer clip.</summary>
    public override void ApplyClip(Vector4i? clip)
    {
        _inheritedClip = clip;
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        var own = IntersectClip(new Vector4i(x, y, x + (int)Computed.Width, y + (int)Computed.Height), clip);

        base.ApplyClip(own);
        _caretPlane.ClipRect = own;
        _selectionPlane.ClipRect = own;
        _selectionMiddle.ClipRect = own;
        _selectionLast.ClipRect = own;
        // The box background itself is only bound by the outer clip.
        if (Background != null) Background.ClipRect = clip;
    }

    protected override void FocusGained()
    {
        InvalidateLayout();
    }

    protected override void FocusLost()
    {
        InvalidateLayout();
    }

    protected override void DrawSelf(UIContext ctx)
    {
        base.DrawSelf(ctx);
        ctx.QueueRender(_selectionPlane, Index);
        ctx.QueueRender(_selectionMiddle, Index);
        ctx.QueueRender(_selectionLast, Index);
        ctx.QueueRender(_caretPlane, Index + 2);
    }

    public override void DrawTo(UIContext ctx)
    {
        if (!Visible) return;
        base.DrawTo(ctx);
        _selectionPlane.Update();
        _selectionMiddle.Update();
        _selectionLast.Update();
        _caretPlane.Update();
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        ApplyAnimations(_selectionPlane, _caretPlane);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        Context.DequeueRender(_selectionPlane, Index);
        Context.DequeueRender(_selectionMiddle, Index);
        Context.DequeueRender(_selectionLast, Index);
        Context.DequeueRender(_caretPlane, Index + 2);
    }

    #endregion
}