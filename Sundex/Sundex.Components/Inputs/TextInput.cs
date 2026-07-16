using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
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
///     Single-line text input with caret, selection (keyboard + mouse drag),
///     horizontal scroll-into-view, and numeric filter modes.
///     Receives input through the focus system: <see cref="UIContext.DispatchTextInput" />
///     and <see cref="UIContext.DispatchKeyDown" />.
/// </summary>
// ponytail: caret/selection indices are char-based; surrogate pairs can be split by
// arrow keys/backspace. Fine for project names and numbers; revisit if emoji matter.
public class TextInput : Panel
{
    private readonly ColoredPlane _caretPlane = new() { Color = Vector4.One };
    private readonly Label _label;
    private readonly ColoredPlane _selectionPlane = new() { Color = new Vector4(0.3f, 0.5f, 0.9f, 0.45f) };
    public override float Padding { get; set; } = 6;
    public override LiteralOrComputable Height { get; set; } = 32;

    private int _anchor;
    private int _caret;
    private Vector4i? _inheritedClip;
    private float _scrollX;
    // The pointer keeps delivering drags while the button is held after a double-click;
    // don't let them collapse the word selection.
    private bool _suppressDragSelection;
    private string _text;

    public TextInput(UIContext context, string value = "") : base(context)
    {
        _label = new Label(context, value)
        {
            Parent = this,
            FontSizePx = 16f
        };
        Children = [_label];

        Focusable = true;
        UpdateCursorOnHover = true;
        _text = SanitizeLine(value);
        _caret = _anchor = _text.Length;
    }

    public override string Tag => "text-input";

    public string Value
    {
        get => _text;
        set => SetText(SanitizeLine(value));
    }

    public TextInputFilter Filter { get; set; } = TextInputFilter.None;

    public int CaretIndex => _caret;
    public int SelectionStart => Math.Min(_caret, _anchor);
    public int SelectionEnd => Math.Max(_caret, _anchor);
    public bool HasSelection => _caret != _anchor;

    /// <summary>Horizontal scroll offset in pixels keeping the caret visible.</summary>
    public float ScrollX => _scrollX;

    [NamedSetting("font-size")]
    public LiteralOrComputable FontSizePx
    {
        get => _label.FontSizePx;
        set => _label.FontSizePx = value;
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
        set => _selectionPlane.Color = value;
    }

    public Action<TextInput>? OnValueChanged { get; set; }
    public Action<TextInput>? OnCommit { get; set; }

    private float FontSize => _label.FontSizePx.Resolve(14);
    private float LineHeight => FontSize * 1.2f;

    /// <summary>Width at the right end of the content area not available to text (e.g. spinner buttons).</summary>
    protected virtual float ReservedRightWidth => 0;

    #region Input handling

    public override void HandleTextInput(TextInputEventArgs e)
    {
        Insert(e.AsString);
    }

    public override bool HandleKeyDown(KeyboardKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Left:
                if (!e.Shift && HasSelection) SetCaret(SelectionStart, false);
                else SetCaret(_caret - 1, e.Shift);
                return true;

            case Keys.Right:
                if (!e.Shift && HasSelection) SetCaret(SelectionEnd, false);
                else SetCaret(_caret + 1, e.Shift);
                return true;

            case Keys.Home:
                SetCaret(0, e.Shift);
                return true;

            case Keys.End:
                SetCaret(_text.Length, e.Shift);
                return true;

            case Keys.Backspace:
                if (HasSelection) RemoveSelection();
                else if (_caret > 0)
                {
                    SetText(_text.Remove(_caret - 1, 1), _caret - 1);
                }

                return true;

            case Keys.Delete:
                if (HasSelection) RemoveSelection();
                else if (_caret < _text.Length) SetText(_text.Remove(_caret, 1), _caret);
                return true;

            case Keys.A when e.Control:
                SelectAll();
                return true;

            case Keys.Enter or Keys.KeyPadEnter:
                OnCommit?.Invoke(this);
                Context.Blur();
                return true;

            default:
                return false;
        }
    }

    public override bool HandlePress(float x, float y)
    {
        _suppressDragSelection = false;
        SetCaret(CaretIndexFromX(x), false);
        return true;
    }

    public override void HandlePointerDrag(float x, float y)
    {
        if (_suppressDragSelection) return;
        SetCaret(CaretIndexFromX(x), true);
    }

    /// <summary>Double-click selects the run of letters/digits under the pointer.</summary>
    public override bool HandleDoublePress(float x, float y)
    {
        _suppressDragSelection = true;
        if (_text.Length == 0) return true;

        var index = Math.Clamp(CaretIndexFromX(x), 0, _text.Length - 1);
        if (!char.IsLetterOrDigit(_text[index])) return true; // punctuation/gaps just keep the caret

        var start = index;
        var end = index;
        while (start > 0 && char.IsLetterOrDigit(_text[start - 1])) start--;
        while (end < _text.Length && char.IsLetterOrDigit(_text[end])) end++;

        _anchor = start;
        _caret = end;
        AfterCaretMove();
        return true;
    }

    #endregion

    #region Editing

    public void Insert(ReadOnlySpan<char> input)
    {
        var remaining = HasSelection ? _text.Remove(SelectionStart, SelectionEnd - SelectionStart) : _text;
        var caret = HasSelection ? SelectionStart : _caret;

        Span<char> accepted = stackalloc char[input.Length];
        var count = 0;
        foreach (var c in input)
        {
            if (c is '\n' or '\r') continue;
            if (!AllowsCharacter(c, remaining, caret + count)) continue;
            accepted[count++] = c;
        }

        if (count == 0 && remaining == _text) return;

        SetText(remaining.Insert(caret, new string(accepted[..count])), caret + count);
    }

    public void SelectAll()
    {
        _anchor = 0;
        _caret = _text.Length;
        AfterCaretMove();
    }

    private void RemoveSelection()
    {
        SetText(_text.Remove(SelectionStart, SelectionEnd - SelectionStart), SelectionStart);
    }

    private bool AllowsCharacter(char c, string remaining, int position)
    {
        switch (Filter)
        {
            case TextInputFilter.None:
                return true;

            case TextInputFilter.Integer:
            case TextInputFilter.Decimal:
                if (char.IsAsciiDigit(c)) return true;
                if (c == '-') return position == 0 && !remaining.Contains('-');
                if (c == '.' && Filter == TextInputFilter.Decimal) return !remaining.Contains('.');
                return false;

            default:
                return false;
        }
    }

    private void SetCaret(int index, bool extendSelection)
    {
        _caret = Math.Clamp(index, 0, _text.Length);
        if (!extendSelection) _anchor = _caret;
        AfterCaretMove();
    }

    private void SetText(string text, int? caret = null)
    {
        var changed = text != _text;
        _text = text;
        _caret = Math.Clamp(caret ?? _caret, 0, _text.Length);
        _anchor = _caret;
        _label.Value = _text;
        AfterCaretMove();
        if (changed) OnValueChanged?.Invoke(this);
    }

    private void AfterCaretMove()
    {
        ScrollCaretIntoView();
        InvalidateLayout();
    }

    private static string SanitizeLine(string value)
    {
        return value.Replace("\n", "").Replace("\r", "");
    }

    #endregion

    #region Measurement / layout

    private void ScrollCaretIntoView()
    {
        var innerWidth = Math.Max(0, Computed.Width - 2 * Padding - ReservedRightWidth);
        if (innerWidth <= 0) return;

        var caretX = MeasurePrefix(_caret);
        if (caretX - _scrollX > innerWidth) _scrollX = caretX - innerWidth;
        if (caretX - _scrollX < 0) _scrollX = caretX;

        var totalWidth = MeasurePrefix(_text.Length);
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, totalWidth - innerWidth));
    }

    /// <summary>Pixel width of the first <paramref name="length" /> characters at the current font size.</summary>
    public float MeasurePrefix(int length)
    {
        var text = _text.AsSpan(0, Math.Min(length, _text.Length));
        var fontSize = FontSize;
        var width = 0f;

        Span<char> pair = stackalloc char[2];
        for (var i = 0; i < text.Length;)
        {
            scoped ReadOnlySpan<char> character;
            if (char.IsSurrogate(text[i]) && i + 1 < text.Length && char.IsSurrogatePair(text[i], text[i + 1]))
            {
                pair[0] = text[i];
                pair[1] = text[i + 1];
                character = pair;
                i += 2;
            }
            else
            {
                pair[0] = text[i];
                character = pair[..1];
                i += 1;
            }

            var (_, alignment) = Context.TextProvider.GetTextCharacterRect(character);
            width += (float)alignment.AdvanceInUnitSpace * fontSize;
        }

        return width;
    }

    /// <summary>Caret index closest to the given absolute X coordinate.</summary>
    public int CaretIndexFromX(float absoluteX)
    {
        var local = absoluteX - (Computed.AbsoluteX + Padding - _scrollX);
        if (local <= 0) return 0;

        var fontSize = FontSize;
        var width = 0f;

        Span<char> pair = stackalloc char[2];
        for (var i = 0; i < _text.Length;)
        {
            var step = char.IsSurrogate(_text[i]) && i + 1 < _text.Length &&
                       char.IsSurrogatePair(_text[i], _text[i + 1])
                ? 2
                : 1;
            pair[0] = _text[i];
            if (step == 2) pair[1] = _text[i + 1];

            var (_, alignment) = Context.TextProvider.GetTextCharacterRect(pair[..step]);
            var advance = (float)alignment.AdvanceInUnitSpace * fontSize;

            if (local < width + advance / 2) return i;
            width += advance;
            i += step;
        }

        return _text.Length;
    }

    protected override void DoLayout()
    {
        _label.X = -_scrollX;
        base.DoLayout();

        var textX = Computed.AbsoluteX + Padding - _scrollX;
        var textY = Computed.AbsoluteY + Padding;

        var caretX = textX + MeasurePrefix(_caret);
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
        ctx.QueueRender(_caretPlane, Index + 2);
    }

    public override void DrawTo(UIContext ctx)
    {
        if (!Visible) return;
        base.DrawTo(ctx);
        _selectionPlane.Update();
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
        Context.DequeueRender(_caretPlane, Index + 2);
    }

    #endregion
}
