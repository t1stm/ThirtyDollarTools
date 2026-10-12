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
        get => TextSlice != null ? TextSlice.Value : "";
        set => SetTextContents(value);
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