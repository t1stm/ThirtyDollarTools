using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Attributes;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Text;

namespace Sundex.Components.Labels;

[PreloadGraphicsContext]
public class Label : UIElement
{
    private const float ReferenceFontSize = 14;

    protected readonly TextBuffer? TextBuffer;

    // The text as given, while wrap-width breaks it into lines; null when the label doesn't wrap.
    private string? _source;

    /// <param name="font">
    ///     A bundled font's name (<c>FontProvider</c>), e.g. <see cref="Icons.Font" />. Null draws in
    ///     the interface font. Fixed for the label's lifetime: the buffer is built for one font.
    /// </param>
    public Label(UIContext context, ReadOnlySpan<char> text, string? font = null) : base(context)
    {
        TextBuffer = new TextBuffer(font == null ? context.TextProvider : context.GetTextProvider(font),
            context.DeleteQueue);
        TextSlice = TextBuffer.GetTextSlice(text);
    }

    protected TextSlice? TextSlice
    {
        get;
        set
        {
            field = value;
            if (field == null) return;
            ApplySize(field);
        }
    }

    [NamedSetting("text-value")]
    public ReadOnlySpan<char> Value
    {
        get => _source != null ? _source : TextSlice != null ? TextSlice.Value : "";
        set => SetTextContents(value);
    }

    /// <summary>
    ///     The widest a line may get, in pixels: past it the text breaks at the last space. 0
    ///     (the default) never breaks, so the label is as wide as its longest line.
    /// </summary>
    // shortcut: a literal width rather than a percentage of the parent - the label sizes itself
    // before its parent lays it out. Make it relative if a wrapping label ever needs to follow a
    // resizable parent; and a word longer than the width overflows rather than being split.
    [NamedSetting("wrap-width")]
    public float WrapWidth
    {
        get;
        set
        {
            if (Math.Abs(field - value) < 0.01f) return;
            var text = Value.TrimEnd('\0').ToString();
            field = value;
            SetTextContents(text);
        }
    }

    // Defaults to TextSlice's own hardcoded FontSize (16): SetTextContents resolves this
    // into TextSlice.FontSize on every call, so a label that never sets this explicitly
    // must still default to a real size - otherwise its first text update zeroes it out.
    [NamedSetting("font-size")]
    public LiteralOrComputable FontSizePx
    {
        get;
        set
        {
            if (field.IsPercentage == value.IsPercentage && Math.Abs(field.Value - value.Value) < 0.01f) return;
            field = value;
            if (TextSlice == null) return;
            TextSlice.FontSize = value.Resolve(ReferenceFontSize);
            ApplySize(TextSlice);
            // The lines were broken for the old size.
            if (_source != null) ShowText(Wrapped(_source));
        }
    } = 16;

    [NamedSetting("font-color")]
    public Vector4 Color
    {
        get;
        set
        {
            field = value;
            TextSlice?.Color = value;
        }
    } = Vector4.One;

    public override string Tag => "label";

    public override void StopRendering()
    {
        base.StopRendering();
        if (TextBuffer != null)
            Context.DequeueRender(TextBuffer, Index);
    }

    /// <summary>The clip rect this label's glyphs currently render under; null while unclipped.</summary>
    public Vector4i? ClipRect { get; private set; }

    public override void ApplyClip(Vector4i? clip)
    {
        ClipRect = clip;
        TextBuffer?.ClipRect = clip;
    }

    public void SetTextContents(ReadOnlySpan<char> text)
    {
        if (WrapWidth <= 0)
        {
            _source = null;
            ShowText(text);
            return;
        }

        _source = text.ToString();
        ShowText(Wrapped(_source));
    }

    /// <summary>
    ///     <paramref name="text" /> with the space before each word that would cross
    ///     <see cref="WrapWidth" /> turned into a line break - greedy, so the same length.
    /// </summary>
    private string Wrapped(string text)
    {
        if (TextBuffer == null) return text;
        var provider = TextBuffer.TextProvider;
        var fontSize = FontSizePx.Resolve(ReferenceFontSize);
        var chars = text.ToCharArray();

        float lineWidth = 0, widthThroughSpace = 0;
        var lastSpace = -1;
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c == '\n')
            {
                lineWidth = 0;
                lastSpace = -1;
                continue;
            }

            var glyph = char.IsHighSurrogate(c) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1])
                ? chars.AsSpan(i++, 2)
                : chars.AsSpan(i, 1);
            lineWidth += (float)provider.GetGlyph(glyph).Advance * fontSize;

            if (c == ' ')
            {
                lastSpace = i;
                widthThroughSpace = lineWidth;
            }
            else if (lineWidth > WrapWidth && lastSpace >= 0)
            {
                chars[lastSpace] = '\n';
                lineWidth -= widthThroughSpace;
                lastSpace = -1;
            }
        }

        return new string(chars);
    }

    private void ShowText(ReadOnlySpan<char> text)
    {
        if (TextSlice == null) return;
        if (TextBuffer == null) return;

        if (text.Length == TextSlice.Value.Length && text.SequenceEqual(TextSlice.Value))
            return;

        if (text.Length > TextSlice.Length)
        {
            // The replacement slice's constructor runs UpdateCharacters at the default
            // (0,0,0) position, before Position/FontSize/Color are applied below, so the
            // final explicit UpdateCharacters call is what actually writes this text -
            // nothing else is guaranteed to touch Position again this frame.
            var position = TextSlice.Position;
            TextSlice.Dispose();
            var newSlice = TextBuffer.GetTextSlice(text);
            newSlice.UpdateManually = true;
            newSlice.Position = position;
            newSlice.FontSize = FontSizePx.Resolve(ReferenceFontSize);
            newSlice.Color = Color;
            newSlice.UpdateManually = false;
            newSlice.UpdateCharacters();
            TextSlice = newSlice; // setter reads Scale - already correct from the update above
        }
        else
        {
            TextSlice.UpdateManually = true;

            TextSlice.FontSize = FontSizePx.Resolve(ReferenceFontSize);
            TextSlice.Color = Color;
            TextSlice.Value = text;

            TextSlice.UpdateManually = false;
            TextSlice.UpdateCharacters();
        }

        ApplySize(TextSlice);
        Layout();
    }

    /// <summary>
    ///     Sizes the box to the text. The slice measures each line at the font's line height, line
    ///     gap included; the box leaves out the last line's gap, so it ends at the descender. That
    ///     changes nothing for Lato, which has no gap. For Lucide, whose icons are drawn centred in
    ///     an em that runs from the baseline (descender 0) to the ascender (1 em), it makes the box
    ///     the em square, so an icon sits centred in its label and lines up with text beside it.
    /// </summary>
    private void ApplySize(TextSlice slice)
    {
        var metrics = TextBuffer!.TextProvider.GlyphProvider.GetFontMetrics();
        var lineGap = (metrics.LineHeight - (metrics.AscenderY - metrics.DescenderY)) / metrics.EmSize;

        Width = slice.Scale.X;
        // An empty slice measures no line at all, so there is no gap to take off.
        Height = Math.Max(0, slice.Scale.Y - (float)Math.Max(0, lineGap) * slice.FontSize);
    }

    protected override void DoLayout()
    {
        TextSlice?.SetPosition((Computed.AbsoluteX, Computed.AbsoluteY, 0));
    }

    protected override void DrawSelf(UIContext context)
    {
        if (TextBuffer != null)
            context.QueueRender(TextBuffer, Index);
    }
}