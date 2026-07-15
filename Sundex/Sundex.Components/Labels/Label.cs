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
    }

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
        if (TextBuffer != null)
            Context.DequeueRender(TextBuffer, Index);
    }

    public void SetTextContents(ReadOnlySpan<char> text)
    {
        if (TextSlice == null) return;
        if (TextBuffer == null) return;

        if (text.Length == TextSlice.Value.Length && text.SequenceEqual(TextSlice.Value))
            return;

        if (text.Length > TextSlice.Length)
        {
            TextSlice.Dispose();
            var newSlice = TextBuffer.GetTextSlice(text);
            newSlice.UpdateManually = true;
            newSlice.FontSize = FontSizePx.Resolve(ReferenceFontSize);
            newSlice.Color = Color;
            newSlice.UpdateManually = false;
            TextSlice = newSlice; // setter reads Scale — FontSize is already correct at this point
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