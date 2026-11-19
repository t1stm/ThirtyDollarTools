using System.Diagnostics;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Attributes;
using Sundex.Components.Panels;

namespace Sundex.Components.Inputs;

/// <summary>
///     An on/off switch: a rounded track with a knob that slides across when <see cref="Value" />
///     flips. Clicking, Enter or Space flip it. <see cref="OnFill" />, <see cref="OffFill" /> and
///     <see cref="KnobColor" /> are settings, so a sheet colours it.
/// </summary>
[MarkupTag("switch")]
public class Switch : FlexPanel
{
    private const double SlideMs = 150;
    private readonly Panel _knob;
    private readonly Stopwatch _slide = new();
    private float _from, _to;

    public Switch(UIContext context) : base(context)
    {
        Width = 40;
        Height = 24;
        Shrink = 0;
        BorderRadius = 12;
        Focusable = true;
        UpdateCursorOnHover = true;
        _knob = new Panel(context)
        {
            Position = Placement.Absolute,
            Left = 2,
            Top = 2,
            Width = 20,
            Height = 20,
            BorderRadius = 10,
            Fill = KnobColor,
            PointerEvents = PointerEvents.None
        };
        AddChild(_knob);
        Fill = OffFill;
        OnClick = _ => Flip();
    }

    public override string Tag => "switch";

    public bool Value
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Fill = value ? OnFill : OffFill;
            _from = _knob.TranslateX;
            _to = value ? Travel : 0;
            if (Drawn)
            {
                _slide.Restart();
                Context.RegisterUpdate(this);
            }
            else
            {
                _knob.TranslateX = _to;
            }

            OnValueChanged?.Invoke(this);
        }
    }

    public Action<Switch>? OnValueChanged { get; set; }

    [NamedSetting("on-fill")]
    public Vector4 OnFill
    {
        get;
        set
        {
            field = value;
            if (Value) Fill = value;
        }
    } = new(0.42f, 0.34f, 0.96f, 1);

    [NamedSetting("off-fill")]
    public Vector4 OffFill
    {
        get;
        set
        {
            field = value;
            if (!Value) Fill = value;
        }
    } = new(0.24f, 0.22f, 0.3f, 1);

    [NamedSetting("knob-color")]
    public Vector4 KnobColor
    {
        get;
        set
        {
            field = value;
            _knob?.Fill = value;
        }
    } = new(0.91f, 0.9f, 0.95f, 1);

    /// <summary>How far the knob moves: the track's width less the knob and its two insets.</summary>
    private float Travel => Math.Max(0, (Computed.Width > 0 ? Computed.Width : 40) - 24);

    private void Flip()
    {
        if (!Disabled) Value = !Value;
    }

    public override bool HandleKeyDown(OpenTK.Windowing.Common.KeyboardKeyEventArgs args)
    {
        if (Disabled || args.IsRepeat || args.Key is not (OpenTK.Windowing.GraphicsLibraryFramework.Keys.Enter
                or OpenTK.Windowing.GraphicsLibraryFramework.Keys.KeyPadEnter
                or OpenTK.Windowing.GraphicsLibraryFramework.Keys.Space))
            return false;
        Flip();
        return true;
    }

    public override void Update(UIContext uiContext)
    {
        base.Update(uiContext);
        if (!_slide.IsRunning) return;
        var progress = Math.Clamp(_slide.Elapsed.TotalMilliseconds / SlideMs, 0, 1);
        _knob.TranslateX = _from + (_to - _from) * (float)(1 - Math.Pow(1 - progress, 3));
        if (progress < 1) return;
        _slide.Stop();
        if (!IsHovered) Context.UnregisterUpdate(this);
    }

    protected override void HoverChanged(bool hovered)
    {
        base.HoverChanged(hovered);
        // The base asks for the pointer cursor from Update, so stay registered while hovered.
        if (hovered) Context.RegisterUpdate(this);
        else if (!_slide.IsRunning) Context.UnregisterUpdate(this);
    }
}
