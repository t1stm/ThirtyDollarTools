using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The M/S gutter to the left of the arrangement: one mute and one solo toggle per
///     lane (channel semantics, FL-style — muting a lane silences whatever clips sit on
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
            var mute = NewToggle(context, "M", () => _state.ToggleMute(index));
            var solo = NewToggle(context, "S", () => _state.ToggleSolo(index));
            _rows.Add((mute, solo));
            AddChild(mute);
            AddChild(solo);
        }

        RefreshChannels();
    }

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
        for (var lane = 0; lane < _rows.Count; lane++)
        {
            var (mute, solo) = _rows[lane];
            var visible = lane < lanes &&
                          ArrangementView.RulerHeight + (lane + 1) * ArrangementView.LaneHeight <= Computed.Height;
            var y = ArrangementView.RulerHeight + lane * ArrangementView.LaneHeight +
                    (ArrangementView.LaneHeight - 24) / 2;

            mute.Visible = solo.Visible = visible;
            mute.X = 4;
            solo.X = 32;
            mute.Y = solo.Y = y;
        }

        base.DoLayout();
    }

    private static Button NewToggle(UIContext context, string text, Action toggle)
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
        };
        return button;
    }
}
