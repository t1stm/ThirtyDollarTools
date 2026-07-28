using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Sundex.Markup.Attributes;

namespace VisualizerScene.UI;

public class PlayerBar
{
    private const float FadeDelay = 2.0f;
    private const float FadeSpeed = 5.0f;
    private const float ZoneLow = 0.75f;
    private const float ZoneHigh = 0.85f;

    // Hover feedback colors (RGB only - W is owned by PropagateAlpha).
    // Keeping these here rather than in state[hovered] stylesheet blocks prevents
    // InvalidateStyle from restoring alpha=1 from _baseSnapshot on every state transition.
    private static readonly Vector3 ButtonBaseRgb = new(0x7a / 255f, 0xa2 / 255f, 0xf7 / 255f);
    private static readonly Vector3 ButtonHoverRgb = new(0x9a / 255f, 0xb8 / 255f, 1.0f);

    private float _inactivityTimer;
    private Vector2 _lastMousePos;

    public PlayerBar(
        UIContext context,
        Action onBack,
        Action onPlayPause,
        Action onRestart,
        Action<float> onSeek)
    {
        OnBack = onBack;
        OnPlayPause = onPlayPause;
        OnRestart = onRestart;
        OnSeek = onSeek;

        var sundexContext = new SundexContext(context);
        var source = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = "UI/Layout/PlayerBar.snx.xml" }
        });

        Component = sundexContext.NewComponent(source.Value);
        sundexContext.RunLogicAndVerify(Component,
            () => RootPanel,
            () => ProgressBar,
            () => CurrentTimeLabel,
            () => TotalTimeLabel,
            () => PlayPauseButton,
            () => BackButton,
            () => RestartButton);

        RootPanel.DrawTo(context);
    }

    public float CurrentAlpha { get; private set; }

    /// <summary>
    ///     When set, the bar fades out and stays hidden regardless of the mouse position.
    /// </summary>
    public bool Hidden { get; set; }

    public Action OnBack { get; }
    public Action OnPlayPause { get; }
    public Action OnRestart { get; }
    public Action<float> OnSeek { get; }

    [UsedImplicitly] public SundexComponent Component { get; }

    [SetFromLogic] public Panel RootPanel { get; set; } = null!;
    [SetFromLogic] public ProgressBar ProgressBar { get; set; } = null!;
    [SetFromLogic] public Label CurrentTimeLabel { get; set; } = null!;
    [SetFromLogic] public Label TotalTimeLabel { get; set; } = null!;
    [SetFromLogic] public Button PlayPauseButton { get; set; } = null!;
    [SetFromLogic] public Button BackButton { get; set; } = null!;
    [SetFromLogic] public Button RestartButton { get; set; } = null!;

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void Update(UIContext context)
    {
        if (CurrentAlpha < 0.01f) return;
        RootPanel.Update(context);
        RootPanel.Layout();
    }

    public void UpdateAlpha(MouseState mouse, Vector2i windowSize, float deltaTime, bool forceVisible = false)
    {
        if (forceVisible && !Hidden)
        {
            _inactivityTimer = 0f;
            _lastMousePos = mouse.Position;
            CurrentAlpha = 1f;
            PropagateAlpha(1f);
            return;
        }

        var mousePos = mouse.Position;
        var inWindow = mousePos.X >= 0 && mousePos.X <= windowSize.X &&
                       mousePos.Y >= 0 && mousePos.Y <= windowSize.Y;
        var moved = mousePos != _lastMousePos;
        _lastMousePos = mousePos;

        if (inWindow && (moved || mouse.IsAnyButtonDown)) _inactivityTimer = 0f;
        else _inactivityTimer += deltaTime;

        var normalizedY = windowSize.Y > 0 ? mousePos.Y / windowSize.Y : 0f;
        var inZone = inWindow && normalizedY > ZoneLow;
        var isActive = !Hidden && inZone && _inactivityTimer < FadeDelay;

        var targetAlpha = isActive
            ? Math.Clamp((normalizedY - ZoneLow) / (ZoneHigh - ZoneLow), 0f, 1f)
            : 0f;

        var step = FadeSpeed * deltaTime;
        CurrentAlpha = CurrentAlpha < targetAlpha
            ? Math.Min(CurrentAlpha + step, targetAlpha)
            : Math.Max(CurrentAlpha - step, targetAlpha);

        PropagateAlpha(CurrentAlpha);
    }

    public void MouseEvent(MouseState mouse, Vector2 scale)
    {
        if (CurrentAlpha < 0.01f) return;

        RootPanel.Test(mouse, scale);

        if (!mouse.IsButtonDown(MouseButton.Left)) return;
        if (mouse.Delta is { X: 0, Y: 0 } && mouse.WasButtonDown(MouseButton.Left)) return;

        var pb = ProgressBar.BackgroundPanel.Computed;
        var mx = mouse.Position.X / scale.X;
        var my = mouse.Position.Y / scale.Y;

        if (!(mx >= pb.AbsoluteX) || !(mx <= pb.AbsoluteX + pb.Width) ||
            !(my >= pb.AbsoluteY) || !(my <= pb.AbsoluteY + pb.Height)) return;

        var fraction = (mx - pb.AbsoluteX) / pb.Width;
        OnSeek(Math.Clamp(fraction, 0f, 1f));
    }

    private void PropagateAlpha(float a)
    {
        SetPanelAlpha(RootPanel, a);
        SetLabelAlpha(CurrentTimeLabel, a);
        SetLabelAlpha(TotalTimeLabel, a);
        SetButtonAlpha(BackButton, a);
        SetButtonAlpha(PlayPauseButton, a);
        SetButtonAlpha(RestartButton, a);
        SetPanelAlpha(ProgressBar.BackgroundPanel, a);
        SetPanelAlpha(ProgressBar.ForegroundPanel, a);
    }

    private static void SetLabelAlpha(Label label, float a)
    {
        label.Color = label.Color with { W = a };
    }

    private static void SetButtonAlpha(Button button, float a)
    {
        SetLabelAlpha(button.Label, a);
        SetPanelAlpha(button, a);
    }

    private static void SetPanelAlpha(Panel panel, float a)
    {
        panel.Background?.Color = panel.Background.Color with { W = a };
    }
}