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

    /// <summary>
    ///     How far past the drawn timeline, above and below, a press still seeks. The line is
    ///     4 px, far too thin to aim at.
    /// </summary>
    private const float SeekSlop = 8f;

    /// <summary>
    ///     Applied every frame, not only mid-fade: the hovered/pressed state overrides re-run
    ///     the stylesheet, which puts the styled alpha back.
    /// </summary>
    private readonly ElementAlpha _alpha = new();

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
            () => Playhead,
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
    [SetFromLogic] public Panel Playhead { get; set; } = null!;
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
        Playhead.X = RootPanel.Computed.Width * Math.Clamp(ProgressBar.Progress, 0f, 1f) - Playhead.Computed.Width / 2f;
        RootPanel.Layout();
    }

    /// <param name="cursorInWindow">
    ///     <c>Game.IsCursorInWindow</c>. The position alone can't tell: it stays wherever the
    ///     pointer was last seen inside, so a pointer that left across the bottom edge would
    ///     keep the bar up until the inactivity delay ran out.
    /// </param>
    public void UpdateAlpha(MouseState mouse, Vector2i windowSize, bool cursorInWindow, float deltaTime,
        bool forceVisible = false)
    {
        if (forceVisible && !Hidden)
        {
            _inactivityTimer = 0f;
            _lastMousePos = mouse.Position;
            CurrentAlpha = 1f;
            _alpha.Apply(RootPanel, 1f);
            return;
        }

        var mousePos = mouse.Position;
        // The bounds still matter: a drag keeps reporting positions past the edges.
        var inWindow = cursorInWindow && mousePos.X >= 0 && mousePos.X <= windowSize.X &&
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

        _alpha.Apply(RootPanel, CurrentAlpha);
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
            !(my >= pb.AbsoluteY - SeekSlop) || !(my <= pb.AbsoluteY + pb.Height + SeekSlop)) return;

        var fraction = (mx - pb.AbsoluteX) / pb.Width;
        OnSeek(Math.Clamp(fraction, 0f, 1f));
    }
}