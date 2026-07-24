using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The code-built transport block docked in the track column: progress row,
///     play/stop, back button. Lifted out of <see cref="EditorInterface" />.
/// </summary>
public sealed class TransportSection : FlexPanel
{
    // Subtle-filled look for code-built buttons - code-built children never receive
    // the stylesheet (ApplyStyleSheet runs on the XML tree only), so the fill/hover
    // is set inline here rather than via the .ss class.
    private static readonly Vector4 MenuFillColor = EditorPalette.Divider;
    private static readonly Vector4 MenuFillHoverColor = EditorPalette.DividerHover;
    private static readonly Vector4 TimeColor = EditorPalette.TextMuted;
    private static readonly Vector4 ProgressBackColor = new(0.251f, 0.251f, 0.376f, 1f); // #404060
    private static readonly Vector4 ProgressForeColor = EditorPalette.Header;

    private readonly EditorPlayback _playback;
    private readonly Button _playButton;
    private readonly ProgressBar _progress;
    private readonly Label _elapsedLabel;
    private readonly Label _totalLabel;

    public TransportSection(UIContext context, EditorPlayback playback, Action onBack) : base(context)
    {
        _playback = playback;
        Direction = LayoutDirection.Vertical;
        Width = LiteralOrComputable.Percent(100);
        Padding = 8;
        Spacing = 8;

        _elapsedLabel = new Label(context, "0:00") { FontSizePx = 12f, Color = TimeColor };
        _totalLabel = new Label(context, "0:00") { FontSizePx = 12f, Color = TimeColor };
        _progress = new ProgressBar(context,
            new ColoredPlane { Color = ProgressBackColor }, new ColoredPlane { Color = ProgressForeColor })
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 8,
            BorderRadius = 4
        };
        var progressRow = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Spacing = 8,
            VerticalAlign = Align.Center,
            Children = [_elapsedLabel, _progress, _totalLabel]
        };

        _playButton = TransportButton(context, "Play", playback.PlayPause);
        _playButton.Width = LiteralOrComputable.Percent(50);
        var stopButton = TransportButton(context, "Stop", playback.Stop);
        stopButton.Width = LiteralOrComputable.Percent(50);
        var transportButtonsRow = new FlexPanel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Spacing = 8,
            Children = [_playButton, stopButton]
        };

        var backButton = TransportButton(context, "Back", onBack);
        backButton.Width = LiteralOrComputable.Percent(100);

        Children = [Divider(context), progressRow, transportButtonsRow, Divider(context), backButton];
    }

    /// <summary>Per-frame: play/pause label always, progress/elapsed/total only while a session exists.</summary>
    public void Refresh()
    {
        if (_playback.HasSession)
        {
            var elapsed = _playback.ElapsedMs;
            var total = _playback.TotalMs;
            _progress.Progress = total > 0 ? (float)elapsed / total : 0;
            _elapsedLabel.SetTextContents(TimeString(elapsed));
            _totalLabel.SetTextContents(TimeString(total));
        }

        _playButton.Label.SetTextContents(_playback.IsPlaying ? "Pause" : "Play");
    }

    private static string TimeString(long ms)
    {
        return $"{ms / 60000}:{ms / 1000 % 60:00}";
    }

    /// <summary>Subtle-filled button matching the menu bar/"+ Add track" look - code-built
    /// children get no stylesheet, so the fill/hover swap is wired here instead of via the
    /// .ss <c>menu-button</c> class.</summary>
    private static Button TransportButton(UIContext context, string label, Action onClick)
    {
        var fill = new ColoredPlane { Color = MenuFillColor };
        return new Button(context, label, fill)
        {
            FontSizePx = 13f,
            BorderRadius = 6,
            OnClick = _ => onClick(),
            OnHoverEnter = _ => fill.Color = MenuFillHoverColor,
            OnHoverExit = _ => fill.Color = MenuFillColor
        };
    }

    /// <summary>A 1px full-width rule, same color as the header/menu dividers.</summary>
    private static Panel Divider(UIContext context)
    {
        return new Panel(context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 1,
            Background = new ColoredPlane { Color = MenuFillColor }
        };
    }
}
