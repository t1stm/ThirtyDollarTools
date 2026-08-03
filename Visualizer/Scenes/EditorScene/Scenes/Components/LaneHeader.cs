using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The M/S gutter to the left of the arrangement: one mute and one solo toggle per
///     lane (channel semantics, FL-style - muting a lane silences whatever clips sit on
///     it). Lives outside <see cref="ArrangementView" /> so clips can never cover or
///     out-hit the buttons.
/// </summary>
public sealed class LaneHeader : Panel
{
    public const float GutterWidth = 60f;

    private static readonly Vector4 Inactive = EditorPalette.TextMuted;
    private static readonly Vector4 MuteOn = EditorPalette.DangerAccent;
    private static readonly Vector4 SoloOn = new(0.878f, 0.686f, 0.408f, 1f); // #e0af68

    private readonly ArrangementView _arrangement;
    private readonly List<(Button Mute, Button Solo)> _rows = [];
    private readonly EditorState _state;

    public LaneHeader(UIContext context, EditorState state, ArrangementView arrangement) : base(context)
    {
        _state = state;
        _arrangement = arrangement;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

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

    /// <summary>Hover hint text for the M/S toggles; null on hover exit. See EditorInterface.SetHint.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>Re-colors the toggles from the state. Call when mute/solo changes.</summary>
    public void RefreshChannels()
    {
        for (var lane = 0; lane < _rows.Count; lane++)
        {
            var (mute, solo) = _rows[lane];
            mute.Label.Color = _state.IsMuted(lane) ? MuteOn : Inactive;
            solo.Label.Color = _state.IsSoloed(lane) ? SoloOn : Inactive;
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
    }

    private Button NewToggle(UIContext context, string text, Action toggle, string hint)
    {
        var button = new Button(context, new Label(context, text)
        {
            FontSizePx = 12f,
            Color = Inactive
        })
        {
            Width = 24,
            Height = 24,
            OnClick = _ => toggle(),
            OnHoverEnter = _ => OnHint?.Invoke(hint),
            OnHoverExit = _ => OnHint?.Invoke(null)
        };
        return button;
    }
}