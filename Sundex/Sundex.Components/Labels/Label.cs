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

    public Label(UIContext context, ReadOnlySpan<char> text) : base(context)
    {
        TextBuffer = new TextBuffer(context.TextProvider, context.DeleteQueue);
        TextSlice = TextBuffer.GetTextSlice(text);
    }

    protected TextSlice? TextSlice
    {
        get;
        set
        {
            field = value;
            if (field == null) return;
            Width = field.Scale.X;
            Height = field.Scale.Y;
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

            var scale = TextSlice.Scale;
            Width = scale.X;
            Height = scale.Y;
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

        var scale = TextSlice.Scale;

        Width = scale.X;
        Height = scale.Y;
        Layout();
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