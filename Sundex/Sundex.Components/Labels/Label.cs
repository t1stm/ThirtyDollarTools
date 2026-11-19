using System.Text;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Components.Renderer.Planes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Text;

namespace Sundex.Components.Labels;

/// <summary>Where each line sits in a label wider than it (CSS <c>text-align</c>).</summary>
public enum TextAlign
{
    Start,
    Center,
    End
}

/// <summary>CSS <c>text-transform</c>.</summary>
public enum TextTransform
{
    None,
    Uppercase,
    Lowercase
}

/// <summary>
///     Text. Content-sized by default: it measures to its longest line. With <see cref="Wrap" /> it
///     breaks to the width its parent gives it, with <see cref="Truncate" /> it ends in an ellipsis
///     there, and <see cref="LineClamp" /> stops it after that many lines. Line height, letter
///     spacing and alignment move the glyphs the text buffer placed; the font can change at any
///     time, from code or a stylesheet.
/// </summary>
[MarkupTag("label")]
[PreloadGraphicsContext]
public class Label : UIElement
{
    private const float ReferenceFontSize = 14;

    protected TextBuffer? TextBuffer;

    private ReadOnlyMemory<char> _raw;
    private char[] _rawBuffer = [];
    private string? _formattedLayout;
    private float[] _lineWidths = [0];
    private int[] _lineStarts = [0];
    private float _measuredW;
    private (float Width, float Size, float Spacing, string? Font, int Mode)? _key;
    private string? _font;
    private BoxPlane? _underline;
    private bool _disposed;

    /// <summary>An empty label in the interface font, for markup: <c>&lt;label value="…"/&gt;</c>.</summary>
    public Label(UIContext context) : this(context, ReadOnlySpan<char>.Empty)
    {
    }

    /// <param name="font">A bundled font's name (<c>FontProvider</c>), e.g. <see cref="Icons.Font" />; null for the interface font.</param>
    public Label(UIContext context, ReadOnlySpan<char> text, string? font = null) : base(context)
    {
        Width = LiteralOrComputable.AutoSize;
        Height = LiteralOrComputable.AutoSize;
        _font = font;
        _raw = text.ToString().AsMemory();
        TextBuffer = new TextBuffer(ProviderFor(font), context.DeleteQueue);
        TextSlice = TextBuffer.GetTextSlice(text);
        TextSlice.FontSize = FontSizePx.Resolve(ReferenceFontSize);
        Compute(float.MaxValue);
    }

    protected TextSlice? TextSlice { get; set; }

    public override string Tag => "label";

    #region Settings

    [NamedSetting("text-value")]
    public ReadOnlySpan<char> Value
    {
        get => _raw.Span;
        set => SetTextContents(value);
    }

    /// <summary>The text as given, before any transform, wrapping or ellipsis.</summary>
    public string Text
    {
        get => _raw.ToString();
        set
        {
            if (value.AsSpan().SequenceEqual(Value)) return;
            _raw = value.AsMemory();
            Changed();
        }
    }

    /// <summary>The text as drawn: transformed, broken into lines and cut, for the width the label last had.</summary>
    public string Shown => ShownText.ToString();

    private ReadOnlySpan<char> ShownText => TextSlice is { } slice ? slice.Value[..slice.Length] : [];
    private ReadOnlySpan<char> LayoutText => _formattedLayout is { } formatted ? formatted.AsSpan() : Value;

    /// <summary>A bundled font's name; null for the interface font.</summary>
    [NamedSetting("font")]
    public string? Font
    {
        get => _font;
        set
        {
            if (_font == value) return;
            _font = value;
            RebuildBuffer();
        }
    }

    [NamedSetting("font-size")]
    public LiteralOrComputable FontSizePx
    {
        get;
        set
        {
            if (field.IsPercentage == value.IsPercentage && Math.Abs(field.Value - value.Value) < 0.01f) return;
            field = value;
            TextSlice?.FontSize = value.Resolve(ReferenceFontSize);
            Changed();
        }
    } = 16;

    [NamedSetting("font-color")]
    public Vector4 Color
    {
        get;
        set
        {
            field = value;
            ApplyColor();
        }
    } = Vector4.One;

    /// <summary>Line height as a multiple of the font size; 0 for the font's own.</summary>
    [NamedSetting("line-height")]
    public float LineHeight
    {
        get;
        set
        {
            field = value;
            Changed();
        }
    }

    /// <summary>Extra space after each character, in ems (CSS <c>letter-spacing</c>).</summary>
    [NamedSetting("letter-spacing")]
    public float LetterSpacing
    {
        get;
        set
        {
            field = value;
            Changed();
        }
    }

    [NamedSetting("text-transform")]
    public TextTransform TextTransform
    {
        get;
        set
        {
            field = value;
            Changed();
        }
    }

    [NamedSetting("text-align")]
    public TextAlign TextAlign
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    }

    /// <summary>Breaks the text to the width the label is given.</summary>
    [NamedSetting("wrap")]
    public bool Wrap
    {
        get;
        set
        {
            field = value;
            Changed();
        }
    }

    /// <summary>Ends a line that doesn't fit in an ellipsis.</summary>
    [NamedSetting("truncate")]
    public bool Truncate
    {
        get;
        set
        {
            field = value;
            Changed();
        }
    }

    /// <summary>Stops after this many lines, the last one ending in an ellipsis if more text follows; 0 for no limit.</summary>
    [NamedSetting("line-clamp")]
    public int LineClamp
    {
        get;
        set
        {
            field = value;
            Changed();
        }
    }

    /// <summary>The widest a line may get, in pixels, whatever the label is given; 0 for no limit.</summary>
    [NamedSetting("wrap-width")]
    public float WrapWidth
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 0.01f) return;
            field = value;
            Changed();
        }
    }

    [NamedSetting("underline")]
    public bool Underline
    {
        get;
        set
        {
            field = value;
            InvalidateLayout();
        }
    }

    /// <summary>Lines of text as drawn.</summary>
    public int LineCount => _lineStarts.Length;

    private bool Constrained => Wrap || Truncate || LineClamp > 0;
    private float Size => FontSizePx.Resolve(ReferenceFontSize);

    /// <summary>The font's own line height for the current size.</summary>
    private float NaturalLine
    {
        get
        {
            var metrics = TextBuffer!.TextProvider.GlyphProvider.GetFontMetrics();
            return (float)(metrics.LineHeight / metrics.EmSize) * Size;
        }
    }

    /// <summary>The height of one line, in pixels.</summary>
    public float LinePixels => LineHeight > 0 ? LineHeight * Size : NaturalLine;

    public override bool CanCollapseX => base.CanCollapseX || Constrained;

    #endregion

    public void SetTextContents(ReadOnlySpan<char> text)
    {
        if (text.SequenceEqual(Value)) return;
        if (text.Length > _rawBuffer.Length)
            Array.Resize(ref _rawBuffer, Math.Max(text.Length, _rawBuffer.Length * 2));
        text.CopyTo(_rawBuffer);
        _raw = _rawBuffer.AsMemory(0, text.Length);
        Changed();
    }

    private void Changed()
    {
        _key = null;
        // Unconstrained text doesn't wait for a width: show it now, as a label always has.
        if (!Constrained && TextBuffer != null)
        {
            Compute(WrapWidth > 0 ? WrapWidth : float.MaxValue);
            Show();
        }

        InvalidateMeasure();
    }

    #region Measuring and breaking

    public override (float width, float height) Measure(float parentWidth, float parentHeight)
    {
        var available = Math.Max(0, parentWidth);
        if (!MaxWidth.Auto) available = Math.Min(available, MaxWidth.Resolve(parentWidth));
        Compute(Constrained ? available : WrapWidth > 0 ? WrapWidth : float.MaxValue);

        var width = _measuredW;
        if (Constrained) width = Math.Min(width, available);
        if (!Width.Auto) width = Width.Resolve(parentWidth);
        var height = Height.Auto ? TextHeight : Height.Resolve(parentHeight);
        return (width, height);
    }

    /// <summary>
    ///     The box's height: one line height per line. Without a set line height, the last line's
    ///     gap is left out, so the box ends at the descender (an icon font's em square, centred).
    /// </summary>
    private float TextHeight
    {
        get
        {
            if (TextBuffer == null) return 0;
            if (LineHeight > 0) return LineCount * LinePixels;
            var metrics = TextBuffer.TextProvider.GlyphProvider.GetFontMetrics();
            var gap = (float)Math.Max(0, (metrics.LineHeight - (metrics.AscenderY - metrics.DescenderY)) / metrics.EmSize);
            return LayoutText.IsEmpty ? 0 : Math.Max(0, LineCount * NaturalLine - gap * Size);
        }
    }

    /// <summary>Lays the text out for a width: transformed, broken and cut.</summary>
    private void Compute(float width)
    {
        if (TextBuffer == null) return;
        if (WrapWidth > 0) width = Math.Min(width, WrapWidth);
        var mode = (Wrap ? 1 : 0) | (Truncate ? 2 : 0) | (LineClamp << 2) | (WrapWidth > 0 ? 1 << 20 : 0);
        var key = (width, Size, LetterSpacing, _font, mode);
        if (_key == key) return;
        _key = key;

        var breaks = Wrap || LineClamp > 0 || WrapWidth > 0;
        // Half a pixel of slack: a parent hands a label back the width it measured, rounded or
        // summed a little differently, and a line exactly that wide must not break over it.
        var room = width + 0.5f;
        var transformed = TextTransform switch
        {
            TextTransform.Uppercase => Text.ToUpperInvariant(),
            TextTransform.Lowercase => Text.ToLowerInvariant(),
            _ => null
        };
        if (Truncate && !breaks) _formattedLayout = Fit((transformed ?? Text).Replace('\n', ' '), room);
        else if (breaks) _formattedLayout = Break(transformed ?? Text, room, LineClamp > 0 ? LineClamp : int.MaxValue);
        else _formattedLayout = transformed;

        MeasureLines();
    }

    private float Advance(ReadOnlySpan<char> glyph) =>
        (float)TextBuffer!.TextProvider.GetGlyph(glyph).Advance * Size + LetterSpacing * Size;

    private float WidthOf(ReadOnlySpan<char> text)
    {
        float width = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var step = char.IsHighSurrogate(text[index]) && index + 1 < text.Length ? 2 : 1;
            width += Advance(text.Slice(index, step));
            index += step - 1;
        }

        return width;
    }

    private string Fit(string text, float width)
    {
        if (WidthOf(text) <= width + 0.5f) return text;
        var ellipsis = Advance("…");
        float used = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var step = char.IsHighSurrogate(text[index]) && index + 1 < text.Length ? 2 : 1;
            var advance = Advance(text.AsSpan(index, step));
            if (used + advance + ellipsis > width) return text[..index].TrimEnd() + "…";
            used += advance;
            index += step - 1;
        }

        return text;
    }

    /// <summary>Greedy, breaking at spaces, and inside a word only when the word is wider than the line.</summary>
    private string Break(string text, float width, int maxLines)
    {
        var result = new StringBuilder(text.Length + 8);
        var lines = 1;
        var paragraphs = text.Split('\n');
        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
        {
            var paragraph = paragraphs[paragraphIndex];
            if (paragraphIndex > 0)
            {
                if (lines >= maxLines) break;
                result.Append('\n');
                lines++;
            }

            float lineWidth = 0;
            var lineStart = result.Length;
            var position = 0;
            while (position < paragraph.Length)
            {
                var space = paragraph.IndexOf(' ', position);
                var end = space < 0 ? paragraph.Length : space + 1;
                var word = paragraph.AsSpan(position, end - position);
                var wordWidth = WidthOf(word);
                var trimmedWidth = WidthOf(word.TrimEnd());

                if (lineWidth + trimmedWidth > width && lineWidth > 0)
                {
                    if (lines >= maxLines)
                    {
                        var tail = result.ToString(lineStart, result.Length - lineStart) + paragraph[position..];
                        result.Length = lineStart;
                        result.Append(Fit(tail, width));
                        return result.ToString();
                    }

                    while (result.Length > lineStart && result[^1] == ' ') result.Length--;
                    result.Append('\n');
                    lines++;
                    lineStart = result.Length;
                    lineWidth = 0;
                }

                if (trimmedWidth > width && lineWidth == 0)
                {
                    // One word wider than the line: split it.
                    for (var index = position; index < end; index++)
                    {
                        var advance = Advance(paragraph.AsSpan(index, 1));
                        if (lineWidth + advance > width && lineWidth > 0)
                        {
                            if (lines >= maxLines)
                            {
                                result.Append('…');
                                return result.ToString();
                            }

                            result.Append('\n');
                            lines++;
                            lineStart = result.Length;
                            lineWidth = 0;
                        }

                        result.Append(paragraph[index]);
                        lineWidth += advance;
                    }
                }
                else
                {
                    result.Append(word);
                    lineWidth += wordWidth;
                }

                position = end;
            }

            // The text's own last space stays: a label followed by another in a row of words needs it.
            if (paragraphIndex < paragraphs.Length - 1)
                while (result.Length > lineStart && result[^1] == ' ') result.Length--;
        }

        return result.ToString();
    }

    private void MeasureLines()
    {
        // The arrays are kept while the line count holds, so a label whose text changes every
        // frame (a load meter) doesn't allocate for its lines.
        var text = LayoutText;
        var lines = text.Count('\n') + 1;
        if (_lineStarts.Length != lines)
        {
            _lineWidths = new float[lines];
            _lineStarts = new int[lines];
        }

        var at = 0;
        float max = 0;
        for (var index = 0; index < lines; index++)
        {
            var length = text[at..].IndexOf('\n');
            if (length < 0) length = text.Length - at;
            _lineStarts[index] = at;
            _lineWidths[index] = WidthOf(text.Slice(at, length));
            max = Math.Max(max, _lineWidths[index]);
            at += length + 1;
        }

        _measuredW = max;
    }

    #endregion

    #region Drawing

    protected override void DoLayout()
    {
        if (_disposed) return;
        Compute(Constrained ? Computed.Width : WrapWidth > 0 ? WrapWidth : float.MaxValue);
        Show();
        TextSlice?.SetPosition((Computed.AbsoluteX, Computed.AbsoluteY, 0));
        PlaceGlyphs();
        PlaceUnderline();
    }

    /// <summary>Puts the laid-out text into the slice, growing it when it no longer fits.</summary>
    private void Show()
    {
        if (TextSlice == null || TextBuffer == null) return;
        var text = LayoutText;
        if (text.SequenceEqual(ShownText)) return;

        if (text.Length > TextSlice.Value.Length)
        {
            // The replacement slice's constructor writes at (0,0,0) before position, size and colour
            // are applied, so the explicit UpdateCharacters at the end is what writes this text.
            var position = TextSlice.Position;
            TextSlice.Dispose();
            var slice = TextBuffer.GetTextSlice(text);
            slice.UpdateManually = true;
            slice.Position = position;
            slice.FontSize = Size;
            slice.Color = Rendered;
            slice.UpdateManually = false;
            slice.UpdateCharacters();
            TextSlice = slice;
        }
        else
        {
            TextSlice.UpdateManually = true;
            TextSlice.FontSize = Size;
            TextSlice.Color = Rendered;
            TextSlice.Value = text;
            TextSlice.UpdateManually = false;
            TextSlice.UpdateCharacters();
        }

        PlaceGlyphs();
    }

    private Vector4 Rendered => Color with { W = Color.W * EffectiveOpacity };

    private void ApplyColor()
    {
        if (_disposed || TextSlice == null) return;
        TextSlice.Color = Rendered;
        _underline?.Color = Rendered;
        PlaceGlyphs();
    }

    protected override void OnOpacityChanged()
    {
        base.OnOpacityChanged();
        ApplyColor();
    }

    /// <summary>
    ///     Moves the glyphs the slice placed to where alignment, letter spacing and line height put
    ///     them. The slice re-places them on any change of position, size or colour, so this follows
    ///     all of those, starting from the slice's own placement each time.
    /// </summary>
    private void PlaceGlyphs()
    {
        var slice = TextSlice;
        var buffer = TextBuffer;
        if (slice == null || buffer == null) return;
        var align = TextAlign switch { TextAlign.Center => 0.5f, TextAlign.End => 1f, _ => 0f };
        var moves = align > 0 || LetterSpacing != 0 || LineHeight > 0;

        // A newline takes no glyph slot, so a slot past the last glyph can still hold one a longer
        // text left there, drawn where it was. The slice clears only what trailing NULs reach.
        var capacity = slice.Value.Length;
        var shown = ShownText;
        var used = 0;
        foreach (var character in shown)
            if (character != '\n' && !char.IsLowSurrogate(character))
                used++;
        for (var index = used; index < capacity && slice.Offset + index < buffer.Characters.Capacity; index++)
            buffer.Characters[slice.Offset + index] = new TextCharacter();

        if (!moves) return;
        slice.UpdateCharacters();

        var natural = NaturalLine;
        var line = LinePixels;
        var firstOffset = LineHeight > 0 ? (line - natural) / 2 : 0;
        var perLine = LineHeight > 0 ? line - natural : 0;
        var spacing = LetterSpacing * Size;
        var box = Computed.Width;

        var slot = 0;
        var row = 0;
        var column = 0;
        var shift = (box - _lineWidths[0]) * align;
        for (var index = 0; index < shown.Length; index++)
        {
            var symbol = shown[index];
            if (symbol == '\n')
            {
                row++;
                column = 0;
                shift = (box - (row < _lineWidths.Length ? _lineWidths[row] : 0)) * align;
                continue;
            }

            if (char.IsHighSurrogate(symbol) && index + 1 < shown.Length) index++;
            if (slot < capacity && slice.Offset + slot < buffer.Characters.Capacity)
            {
                var character = buffer.Characters[slice.Offset + slot];
                var position = character.Position;
                character.Position = new Vector3(position.X + shift + spacing * column,
                    position.Y + firstOffset + perLine * row, position.Z);
                buffer.Characters[slice.Offset + slot] = character;
            }

            slot++;
            column++;
        }
    }

    private void PlaceUnderline()
    {
        if (!Underline)
        {
            _underline?.IsVisible = false;
            return;
        }

        if (_underline == null)
        {
            _underline = new BoxPlane { Color = Rendered };
            if (Drawn) Context.QueueRender(_underline, Index);
        }

        _underline.Color = Rendered;
        _underline.IsVisible = true;
        var metrics = TextBuffer!.TextProvider.GlyphProvider.GetFontMetrics();
        var thickness = Math.Max(1, (float)(metrics.UnderlineThickness / metrics.EmSize) * Size);
        var top = Computed.AbsoluteY + (LineHeight > 0 ? (LinePixels - NaturalLine) / 2 : 0) +
                  Size * (1 - (float)(metrics.UnderlineY / metrics.EmSize));
        _underline.SetPosition((Computed.AbsoluteX, top, 0));
        _underline.Scale = (Math.Min(_measuredW, Computed.Width), thickness, 1);
        _underline.ClipRect = ClipRect;
    }

    private ITextProvider ProviderFor(string? font) =>
        font == null ? Context.TextProvider : Context.GetTextProvider(font);

    private void RebuildBuffer()
    {
        if (_disposed || TextBuffer == null) return;
        var shown = ShownText;
        var drawn = Drawn;
        if (drawn) Context.DequeueRender(TextBuffer, Index);
        TextBuffer.Dispose();

        TextBuffer = new TextBuffer(ProviderFor(_font), Context.DeleteQueue) { ClipRect = ClipRect };
        TextSlice = TextBuffer.GetTextSlice(shown);
        TextSlice.FontSize = Size;
        TextSlice.Color = Rendered;
        if (drawn) Context.QueueRender(TextBuffer, Index);
        _key = null;
        InvalidateMeasure();
    }

    /// <summary>The clip rect this label's glyphs currently render under; null while unclipped.</summary>
    public Vector4i? ClipRect { get; private set; }

    public override void ApplyClip(Vector4i? clip)
    {
        ClipRect = clip;
        TextBuffer?.ClipRect = clip;
        _underline?.ClipRect = clip;
    }

    protected override void DrawSelf(UIContext context)
    {
        if (TextBuffer != null) context.QueueRender(TextBuffer, Index);
        if (_underline != null) context.QueueRender(_underline, Index);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        if (TextBuffer != null) Context.DequeueRender(TextBuffer, Index);
        if (_underline != null) Context.DequeueRender(_underline, Index);
    }

    /// <summary>Frees the text buffer's GPU memory. The label draws nothing afterwards.</summary>
    protected override void OnDispose()
    {
        base.OnDispose();
        if (_disposed) return;
        _disposed = true;
        TextBuffer?.Dispose();
    }

    #endregion

    #region Carets

    /// <summary>Where the caret goes before character <paramref name="index" /> of <see cref="Shown" />, in this label's coordinates.</summary>
    public (float X, float Y) CaretAt(int index)
    {
        var row = LineOf(index);
        var shown = ShownText;
        var end = Math.Clamp(index, _lineStarts[row], shown.Length);
        var run = shown.Slice(_lineStarts[row], end - _lineStarts[row]);
        var align = TextAlign switch { TextAlign.Center => 0.5f, TextAlign.End => 1f, _ => 0f };
        return (WidthOf(run) + (Computed.Width - _lineWidths[row]) * align, row * LinePixels);
    }

    /// <summary>The index in <see cref="Shown" /> nearest a point in this label's coordinates.</summary>
    public int IndexAt(float x, float y)
    {
        var row = Math.Clamp((int)(y / Math.Max(LinePixels, 1)), 0, _lineStarts.Length - 1);
        var start = _lineStarts[row];
        var shown = ShownText;
        var end = row + 1 < _lineStarts.Length ? _lineStarts[row + 1] - 1 : shown.Length;
        float width = 0;
        for (var index = start; index < end; index++)
        {
            var advance = Advance(shown.Slice(index, 1));
            if (x < width + advance / 2) return index;
            width += advance;
        }

        return end;
    }

    /// <summary>The line holding index <paramref name="index" /> of <see cref="Shown" />.</summary>
    public int LineOf(int index)
    {
        var row = 0;
        for (var line = 0; line < _lineStarts.Length; line++)
            if (_lineStarts[line] <= index)
                row = line;
        return row;
    }

    #endregion
}
