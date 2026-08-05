using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     The code-built transport block docked in the track column: progress row,
///     play/stop, back button. Lifted out of <see cref="EditorInterface" />.
/// </summary>
public sealed class TransportSection : FlexPanel
{
    private readonly Label _elapsedLabel;
    private readonly Button _playButton;

    private readonly EditorPlayback _playback;
    private readonly ProgressBar _progress;
    private readonly Label _totalLabel;

    public TransportSection(UIContext context, EditorPlayback playback, Action onBack) : base(context)
    {
        _playback = playback;
        ID = "transport";

        _elapsedLabel = new Label(context, "0:00") { Classes = ["caption-label"] };
        _totalLabel = new Label(context, "0:00") { Classes = ["caption-label"] };
        // The bar's two planes stay code-built: ProgressBar takes them as constructor
        // arguments, so there is nothing for the sheet to assign until it exists. Their
        // colors come from the sheet-loaded palette; the box comes from the sheet.
        _progress = new ProgressBar(context,
            new ColoredPlane { Color = EditorPalette.ProgressTrack },
            new ColoredPlane { Color = EditorPalette.Header })
        {
            ID = "transport-progress"
        };
        var progressRow = new FlexPanel(context)
        {
            Classes = ["transport-progress-row"],
            Children = [_elapsedLabel, _progress, _totalLabel]
        };

        _playButton = TransportButton(context, "Play", playback.PlayPause, "transport-half");
        var stopButton = TransportButton(context, "Stop", playback.Stop, "transport-half");
        var transportButtonsRow = new FlexPanel(context)
        {
            Classes = ["transport-buttons-row"],
            Children = [_playButton, stopButton]
        };

        var backButton = TransportButton(context, "Back", onBack, "transport-full");

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

    /// <summary>
    ///     Subtle-filled button matching the menu bar/"+ Add track" look.
    ///     <paramref name="widthClass" /> is how wide it sits in its row.
    /// </summary>
    private static Button TransportButton(UIContext context, string label, Action onClick, string widthClass)
    {
        return new Button(context, label)
        {
            Classes = ["transport-button", widthClass],
            OnClick = _ => onClick()
        };
    }

    /// <summary>A 1px full-width rule, same color as the header/menu dividers.</summary>
    private static Panel Divider(UIContext context)
    {
        return new Panel(context) { Classes = ["rule"] };
    }
}