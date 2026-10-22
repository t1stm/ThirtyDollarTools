using System.ComponentModel;
using System.Diagnostics;
using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;

namespace Sundex.Components.Labels;

/// <summary>
///     A label that opens <see cref="Url" /> with the OS handler when clicked: http(s) in the
///     browser, file:// (or a plain path) in the file manager or its default app, any other
///     registered scheme (mailto:, steam://, ...) in whatever claims it. Underlined while hovered.
///     Without a URL it is a plain label: no pointer, no underline, a click does nothing.
/// </summary>
public class Link : Label
{
    private readonly ColoredPlane _underline = new() { Scale = Vector3.Zero };
    private Vector3 _underlinePosition;
    private Vector3 _underlineScale;

    public Link(UIContext context, ReadOnlySpan<char> text, string url, string? font = null) : base(context, text,
        font)
    {
        Url = url;
        OnClick = _ => Open();
    }

    public string Url
    {
        get;
        set
        {
            field = value;
            UpdateCursorOnHover = CanOpen;
        }
    }

    public bool CanOpen => !string.IsNullOrWhiteSpace(Url);

    public override string Tag => "link";

    /// <summary>
    ///     Hands <see cref="Url" /> to the shell: ShellExecute on Windows, <c>open</c> on macOS,
    ///     <c>xdg-open</c> on Linux. False when there is no URL or the OS had nothing to open it with.
    /// </summary>
    public bool Open()
    {
        if (!CanOpen) return false;

        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Win32Exception)
        {
            // ponytail: a dead link shouldn't take the app down; nothing in Sundex logs yet.
            return false;
        }
    }

    /// <summary>
    ///     Places the underline from the font's own underline metrics. A line's baseline sits one
    ///     em below its top (FlexLinePositioningProvider), and the metrics are Y-up from it.
    /// </summary>
    protected override void DoLayout()
    {
        base.DoLayout();
        if (TextSlice == null) return;

        var metrics = TextBuffer!.TextProvider.GlyphProvider.GetFontMetrics();
        var fontSize = TextSlice.FontSize;
        var thickness = Math.Max(1, (float)(metrics.UnderlineThickness / metrics.EmSize) * fontSize);
        var top = Computed.AbsoluteY + fontSize * (1 - (float)(metrics.UnderlineY / metrics.EmSize));

        _underlinePosition = new Vector3(Computed.AbsoluteX, top, 0);
        _underlineScale = new Vector3(Computed.Width, thickness, 1);
        _underline.Position = _underlinePosition;
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);

        // Per frame rather than on hover change: Color carries ElementAlpha fades too.
        _underline.Color = Color;
        var scale = IsHovered && CanOpen ? _underlineScale : Vector3.Zero;
        if (_underline.Scale != scale) _underline.Scale = scale;
    }

    public override void ApplyClip(Vector4i? clip)
    {
        base.ApplyClip(clip);
        _underline.ClipRect = clip;
    }

    protected override void DrawSelf(UIContext context)
    {
        base.DrawSelf(context);
        context.QueueRender(_underline, Index);
    }

    public override void StopRendering()
    {
        base.StopRendering();
        Context.DequeueRender(_underline, Index);
    }
}
