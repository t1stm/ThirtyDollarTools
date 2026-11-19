using System.Diagnostics;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Components.Labels;

namespace Sundex.Components.Panels;

/// <summary>
///     A hover hint around its content: after <see cref="DelayMs" /> on the content, <see cref="Text" />
///     shows in a bubble above it (below when there is no room), in <see cref="UIContext.OverlayHost" />
///     so nothing clips it. The bubble is a <c>tooltip-bubble</c> panel holding a <c>tooltip-text</c>
///     label, styled by the tooltip's own sheet.
/// </summary>
[MarkupTag("tooltip")]
public class Tooltip : FlexPanel
{
    private readonly Stopwatch _hovered = new();
    private FlexPanel? _bubble;

    public Tooltip(UIContext context) : base(context)
    {
        AlignItems = Align.Center;
    }

    public override string Tag => "tooltip";

    public string Text
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (_bubble?.Children.FirstOrDefault() is Label label) label.Value = value;
        }
    } = "";

    /// <summary>How long the pointer rests before the hint shows.</summary>
    public double DelayMs { get; set; } = 400;

    /// <summary>The widest the bubble gets before its text wraps.</summary>
    public float MaxWidth { get; set; } = 280;

    protected override void HoverChanged(bool hovered)
    {
        base.HoverChanged(hovered);
        if (hovered)
        {
            _hovered.Restart();
            Context.RegisterUpdate(this);
            return;
        }

        Context.UnregisterUpdate(this);
        Hide();
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        if (_bubble != null || _hovered.Elapsed.TotalMilliseconds < DelayMs || Text.Length == 0) return;
        Show();
    }

    private void Show()
    {
        if (Context.OverlayHost is not { } host) return;
        var label = new Label(Context, Text) { Wrap = true };
        label.Classes.Add("tooltip-text");
        _bubble = new FlexPanel(Context)
        {
            Position = Placement.Absolute,
            PointerEvents = PointerEvents.None,
            MaxWidth = MaxWidth,
            Padding = 6,
            BorderRadius = 4,
            Fill = new OpenTK.Mathematics.Vector4(0.08f, 0.07f, 0.12f, 0.96f)
        };
        _bubble.Classes.Add("tooltip-bubble");
        _bubble.AddChild(label);
        if (StoredStyleSheet is { } sheet) _bubble.ApplyStyleSheet(sheet);
        host.AddChild(_bubble);

        // Above the content, or below it when the top of the window is in the way; kept on screen.
        var (width, height) = _bubble.Measure(MaxWidth, Context.ViewportHeight);
        var x = Math.Clamp(Computed.AbsoluteX + Computed.Width / 2 - width / 2, 4, Math.Max(4, Context.ViewportWidth - width - 4));
        var y = Computed.AbsoluteY - height - 6;
        if (y < 4) y = Computed.AbsoluteY + Computed.Height + 6;
        _bubble.Left = x - host.Computed.AbsoluteX;
        _bubble.Top = y - host.Computed.AbsoluteY;
    }

    private void Hide()
    {
        _bubble?.Dispose();
        _bubble = null;
    }

    protected override void OnDispose()
    {
        Context.UnregisterUpdate(this);
        Hide();
        base.OnDispose();
    }
}
