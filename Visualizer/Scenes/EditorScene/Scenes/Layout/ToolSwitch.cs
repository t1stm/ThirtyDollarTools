using System.Diagnostics;
using EditorScene.Scenes.Components;
using EditorScene.State;
using OpenTK.Mathematics;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using VisualizerScene.Settings;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     The header's Draw/Select switch. Each tool shows its key chip and an icon; the tool's
///     name sits folded away beside the icon and opens out for a moment whenever the tool is
///     picked - a click or its key, even when it is the tool already active - then folds back.
///     Built in code: a markup button holds one label, and these hold four parts.
///     <para>
///         The motion is the design canvas's CSS, with its numbers: the name opens over 0.22 s
///         and fades in over 0.18 s after a 0.06 s wait, stays until 1.3 s after the pick, then
///         closes over 0.26 s while fading out over 0.12 s. It opens towards a 48 px cap rather
///         than the name's own width, and the gap in front of it rides the same curve, as the
///         canvas's max-width and margin did - so "Draw" finishes opening sooner and starts
///         closing later than "Select", just as it does there.
///     </para>
/// </summary>
public sealed class ToolSwitch : FlexPanel
{
    /// <summary>Between the icon and the name, once it is open.</summary>
    private const float NameGap = 6;

    /// <summary>The canvas's max-width for the name.</summary>
    private const float NameCap = 48;

    private const float HoldSeconds = 1.3f;

    private static readonly CubicBezier Opening = new(0.2f, 0.8f, 0.2f, 1f);
    private static readonly CubicBezier Closing = new(0.4f, 0f, 0.2f, 1f);

    private readonly Func<double> _clock;
    private readonly Segment[] _segments;

    /// <param name="context">The editor's UI context.</param>
    /// <param name="clock">Seconds, for the animation; a running Stopwatch when null. Tests pass their own.</param>
    public ToolSwitch(UIContext context, Func<double>? clock = null) : base(context)
    {
        var stopwatch = Stopwatch.StartNew();
        _clock = clock ?? (() => stopwatch.Elapsed.TotalSeconds);

        Classes = ["tool-switch"];
        // X and Y name the switch's centre, so it widens both ways as a name opens.
        AnchorX = Anchor.Center;
        AnchorY = Anchor.Center;
        _segments =
        [
            new Segment(this, EditorTool.Draw, Icons.Pencil, "Draw", Bind.EditorDrawTool),
            new Segment(this, EditorTool.Select, Icons.MousePointer2, "Select", Bind.EditorSelectTool)
        ];
        Children = [_segments[0].Button, _segments[1].Button];

        // Never unsubscribed, like the hint bar's: the editor lives as long as the process.
        Keybinds.Changed += () =>
        {
            foreach (var segment in _segments) segment.RefreshKey();
        };
    }

    /// <summary>A tool's button was clicked.</summary>
    public Action<EditorTool>? OnPick { get; set; }

    /// <summary>Hover text for the hint bar; null when the pointer leaves.</summary>
    public Action<string?>? OnHint { get; set; }

    private float Now => (float)_clock();

    /// <summary>Lights the active tool's button: blue for Draw, yellow for Select.</summary>
    public void SetActive(EditorTool tool)
    {
        foreach (var segment in _segments) segment.SetActive(segment.Tool == tool);
    }

    /// <summary>Opens <paramref name="tool" />'s name for a moment; the other tool's closes at once.</summary>
    public void Peek(EditorTool tool)
    {
        var now = Now;
        foreach (var segment in _segments)
            if (segment.Tool == tool) segment.Open(now);
            else segment.Close(now);
    }

    /// <summary>Per frame, after the update pass and before layout.</summary>
    public void Animate()
    {
        var now = Now;
        foreach (var segment in _segments) segment.Animate(now);
    }

    /// <summary>How wide <paramref name="tool" />'s name currently shows, gap included. For tests.</summary>
    public float NameWidth(EditorTool tool)
    {
        return _segments.Single(segment => segment.Tool == tool).Reveal.Width.Value;
    }

    private sealed class Segment
    {
        private static readonly string[] Fills = ["tool-segment-idle", "tool-segment-draw", "tool-segment-select"];

        private readonly ElementAlpha _alpha = new();
        private readonly Bind _bind;
        private readonly FlexPanel _chip;
        private readonly Label _chipLabel;
        private readonly Transition _fade = new(0);
        private readonly Label _icon;
        private readonly Label _name;
        private readonly Transition _open = new(0);

        /// <summary>When the held name closes; null while it is closed or closing.</summary>
        private float? _closeAt;

        /// <summary>Whether the name still has a frame to draw: it was mid-move last frame, or was just sent somewhere.</summary>
        private bool _moving;

        public Segment(ToolSwitch owner, EditorTool tool, string icon, string name, Bind bind)
        {
            Tool = tool;
            _bind = bind;
            var context = owner.Context;

            _icon = new Label(context, icon, Icons.Font) { Classes = ["tool-segment-icon", "tool-segment-text"] };
            _chipLabel = new Label(context, Keybinds.Get(bind).ToString()) { Classes = ["key-chip-label"] };
            _chip = new FlexPanel(context) { Classes = ["key-chip"], Children = [_chipLabel] };
            _name = new Label(context, name) { Classes = ["tool-segment-name", "tool-segment-text"] };
            Reveal = new RevealPanel(context) { Width = 0, Height = 18, Children = [_name] };

            Button = new Button(context, _icon)
            {
                Classes = ["tool-segment"],
                // Chip, the chip-to-icon gap, icon, name, and a tail: padding is one value on
                // every side, so the tail is what makes the right 8 against the left's 4.
                Children =
                [
                    _chip, new Panel(context) { Width = 6, Height = 1 }, _icon, Reveal,
                    new Panel(context) { Width = 4, Height = 1 }
                ],
                OnClick = _ => owner.OnPick?.Invoke(tool),
                OnHoverEnter = _ => owner.OnHint?.Invoke($"{name} tool ({Keybinds.Get(bind)})"),
                OnHoverExit = _ => owner.OnHint?.Invoke(null)
            };
        }

        public EditorTool Tool { get; }
        public Button Button { get; }
        public RevealPanel Reveal { get; }

        public void SetActive(bool active)
        {
            var draw = Tool == EditorTool.Draw;
            var fill = !active ? "tool-segment-idle" : draw ? "tool-segment-draw" : "tool-segment-select";
            foreach (var each in Fills) Button.SetClass(each, each == fill);

            // A chip on the transparent idle fill needs the light veil to show at all; on the
            // two fills it takes the usual dark one, and its text the fill's own shade.
            _chip.SetClass("key-chip-on-card", !active);
            _chipLabel.SetClass("key-chip-label-on-quiet", !active);
            _chipLabel.SetClass("key-chip-label-on-accent", active && draw);
            foreach (var label in new[] { _icon, _name })
            {
                label.SetClass("light-label", active && draw);
                label.SetClass("dark-label", active && !draw);
            }
        }

        public void RefreshKey()
        {
            _chipLabel.SetTextContents(Keybinds.Get(_bind).ToString());
        }

        public void Open(float now)
        {
            _moving = true; // drawn next frame even if the run is over by then
            _open.Set(1, now, 0.22f, Opening);
            _fade.Set(1, now, 0.18f, CubicBezier.Ease, 0.06f);
            _closeAt = now + HoldSeconds;
        }

        public void Close(float now)
        {
            _moving = true;
            _closeAt = null;
            _open.Set(0, now, 0.26f, Closing);
            _fade.Set(0, now, 0.12f, CubicBezier.Ease);
        }

        public void Animate(float now)
        {
            // From the moment the hold ran out, not this frame's, so a slow frame doesn't
            // stretch it.
            if (_closeAt is { } at && now >= at) Close(at);

            var moving = _open.Running(now) || _fade.Running(now);
            if (!moving && !_moving) return;
            _moving = moving;

            var open = _open.Value(now);
            Reveal.Width = NameGap * open + Math.Min(_name.Width.Value, NameCap * open);
            _name.X = NameGap * open;
            _name.Y = (Reveal.Height.Value - _name.Height.Value) / 2;
            // Re-applied every moving frame: a restyle (the tool changing, a hover) puts the
            // styled alpha back.
            _alpha.Apply(_name, _fade.Value(now));
        }
    }

    /// <summary>A panel that clips its children to its own box - the folded name's window.</summary>
    private sealed class RevealPanel(UIContext context) : Panel(context)
    {
        protected override void DoLayout()
        {
            base.DoLayout();

            // Nothing above a header element clips, so nothing would call ApplyClip from above;
            // the clip is this panel's own box, set at the end of its own layout.
            var x = (int)Computed.AbsoluteX;
            var y = (int)Computed.AbsoluteY;
            var box = new Vector4i(x, y, x + (int)Computed.Width, y + (int)Computed.Height);
            foreach (var child in Children) child.ApplyClip(box);
        }
    }
}
