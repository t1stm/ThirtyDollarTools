using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The M/S gutter to the left of the arrangement: one mute and one solo toggle per
///     lane (channel semantics, FL-style - muting a lane silences whatever clips sit on
///     it). Lives outside <see cref="ArrangementView" /> so clips can never cover or
///     out-hit the buttons.
/// </summary>
public sealed class LaneHeader : Panel
{
    public const float GutterWidth = 60f;

    private readonly ArrangementView _arrangement;
    private readonly List<(Button Mute, Button Solo)> _rows = [];
    private readonly EditorState _state;
    private Vector4i? _inheritedClip;

    public LaneHeader(UIContext context, EditorState state, ArrangementView arrangement) : base(context)
    {
        _state = state;
        _arrangement = arrangement;
        ID = "lane-header";

        for (var lane = 0; lane < ArrangementView.LaneLinePool; lane++)
        {
            var index = lane;
            var mute = NewToggle(context, "M", () => _state.ToggleMute(index), "Mute this track");
            var solo = NewToggle(context, "S", () => _state.ToggleSolo(index), "Solo this track (mutes every other track)");
            _rows.Add((mute, solo));
            AddChild(mute);
            AddChild(solo);
        }

        RefreshChannels();
    }

    // Test seam (internal - see EditorAssembly's InternalsVisibleTo("EditorScene.Tests")).
    internal IReadOnlyList<(Button Mute, Button Solo)> Rows => _rows;

    /// <inheritdoc cref="ApplyClip" />
    internal Vector4i? RowClip { get; private set; }

    /// <summary>Hover hint text for the M/S toggles; null on hover exit. See EditorInterface.SetHint.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>Re-colors the toggles from the state. Call when mute/solo changes.</summary>
    public void RefreshChannels()
    {
        for (var lane = 0; lane < _rows.Count; lane++)
        {
            var (mute, solo) = _rows[lane];
            mute.Label.Color = _state.IsMuted(lane) ? EditorPalette.DangerAccent : EditorPalette.TextMuted;
            solo.Label.Color = _state.IsSoloed(lane) ? EditorPalette.AccentYellow : EditorPalette.TextMuted;
        }
    }

    protected override void DoLayout()
    {
        var lanes = _arrangement.Channels;
        var scrollY = _arrangement.ScrollY;
        for (var lane = 0; lane < _rows.Count; lane++)
        {
            var (mute, solo) = _rows[lane];
            var rowTop = ArrangementView.RulerHeight + lane * ArrangementView.LaneHeight - scrollY;
            var rowBottom = rowTop + ArrangementView.LaneHeight;
            // Scroll-aware: the row's own band must overlap the visible strip below the
            // ruler, matching ArrangementView's divider-line visibility for the same lane.
            var visible = lane < lanes && rowBottom > ArrangementView.RulerHeight && rowTop < Computed.Height;
            var y = rowTop + (ArrangementView.LaneHeight - 24) / 2;

            mute.Visible = solo.Visible = visible;
            mute.X = 4;
            solo.X = 32;
            mute.Y = solo.Y = y;
        }

        base.DoLayout();
        ApplyClip(_inheritedClip);
    }

    /// <summary>
    ///     Confines the toggles to the lane strip. The gutter spans the arrangement's full
    ///     height, but <c>Visible</c> above only culls whole rows: one straddling an edge
    ///     stayed shown and its 24px buttons hung past the panel - over the hint bar below
    ///     the grid, or up beside the ruler's bar numbers. Nothing cut them off, because
    ///     this panel never applied a clip rect at all.
    /// </summary>
    public override void ApplyClip(Vector4i? clip)
    {
        _inheritedClip = clip;
        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        RowClip = IntersectClip(new Vector4i(x, y + (int)ArrangementView.RulerHeight,
            x + (int)Computed.Width, y + (int)Computed.Height), clip);

        Background?.ClipRect = clip;
        foreach (var child in Children) child.ApplyClip(RowClip);
    }

    private Button NewToggle(UIContext context, string text, Action toggle, string hint)
    {
        // The label's color is code-owned - RefreshChannels tints it from the mute/solo
        // state - so the sheet gives it a size only.
        var button = new Button(context, new Label(context, text)
        {
            Classes = ["lane-toggle-label"],
            Color = EditorPalette.TextMuted
        })
        {
            Classes = ["lane-toggle"],
            OnClick = _ => toggle(),
            OnHoverEnter = _ => OnHint?.Invoke(hint),
            OnHoverExit = _ => OnHint?.Invoke(null)
        };
        return button;
    }
}