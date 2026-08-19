using System.Diagnostics;
using JetBrains.Annotations;
using LoadingScene.Reports;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Sundex.Markup.Attributes;

namespace LoadingScene.Scenes;

/// <summary>
///     The loading screen's strip: it rises on boot, stands tall while the first-run setup
///     asks its questions, and settles into a status bar once the sounds start coming down.
///     It owns the order of the setup panes and the motion between them;
///     <see cref="LoadingScene.Loader" /> owns what the answers mean and when loading begins.
/// </summary>
public class LoaderInterface
{
    /// <summary>
    ///     Segments in the meter while the sounds come down. Thirty, because that is what
    ///     this program is called.
    /// </summary>
    public const int LoadTicks = 30;

    private const float SetupHeight = 352f;
    private const float LoadHeight = 104f;

    private const float RiseSeconds = 0.45f;
    private const float PaneSeconds = 0.26f;
    private const float SettleSeconds = 0.38f;

    /// <summary>
    ///     How long the whole screen takes to fade off once the home screen is up. Matches
    ///     the window Loader gives it - its ExitSeconds less its ExitFadeStart - so the
    ///     strip finishes settling exactly as the home screen finishes arriving.
    /// </summary>
    private const float ExitSeconds = 1.0f;

    /// <summary>How far a pane travels as it swaps out, in pixels. Small: a nudge, not a carousel.</summary>
    private const float SlidePx = 28f;

    // Hover colours for the two button classes, RGB only. They live here rather than in
    // state[hovered] blocks because this class owns the buttons' alpha while a pane
    // cross-fades, and a state override would restore the base alpha mid-fade.
    private static readonly Vector3 ActionRgb = new(0x4c / 255f, 0x6b / 255f, 0xcc / 255f);
    private static readonly Vector3 ActionHoverRgb = new(0x62 / 255f, 0x80 / 255f, 0xdd / 255f);
    private static readonly Vector3 QuietRgb = new(0x19 / 255f, 0x1c / 255f, 0x2a / 255f);
    private static readonly Vector3 QuietHoverRgb = new(0x23 / 255f, 0x27 / 255f, 0x3a / 255f);

    /// <summary>Owns every fade in this tree - the pane swaps, and the exit.</summary>
    private readonly ElementAlpha _alpha = new();

    private readonly UIContext _context;

    /// <summary>The setup's panes in the order they are shown - see <see cref="BeginSetup" />.</summary>
    private readonly List<Panel> _panes = [];

    private readonly Stopwatch _exitClock = new();
    private readonly Stopwatch _paneClock = new();
    private readonly Stopwatch _stripClock = new();
    private readonly List<Panel> _ticks = [];

    private bool _loading;

    private Panel? _paneIn;
    private Panel? _paneOut;

    /// <summary>How full the meter is, 0-1. The setup counts steps with it; loading counts files.</summary>
    private float _progress;

    /// <summary>Which setup pane is up, or -1 once the setup is behind us (or was never shown).</summary>
    private int _setupStep = -1;

    private float _stripDelay;

    /// <summary>Rebuilt at the moment the strip animation starts, so the meter re-resolves with it.</summary>
    private int? _stripTickRebuild;

    /// <summary>Faded in step with the strip animation, or null when only the height moves.</summary>
    private UIElement? _stripFadeTarget;

    private float _stripFrom;
    private float _stripSeconds;
    private float _stripTo;

    public LoaderInterface(UIContext context)
    {
        _context = context;

        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/LoaderInterface.snx.xml"
            }
        });

        Component = sundexContext.NewComponent(componentSource.Value);
        sundexContext.RunLogicAndVerify(Component, () => RootPanel, () => Strip, () => Ticks, () => Status,
            () => StatusMessage, () => StatusDetail, () => StatusPercent, () => PaneWelcome, () => PaneUpdates,
            () => PaneSounds, () => IncludePrereleases, () => IncludeNightlies, () => WelcomeNext,
            () => UpdatesDecline, () => UpdatesAccept, () => SoundsStart);

        // Nothing is on screen until Begin* says so: the strip has no height and every pane
        // is off, so the first frame is the bare sound field.
        Strip.Height = 0f;
        Status.Visible = false;
        PaneWelcome.Visible = false;
        PaneUpdates.Visible = false;
        PaneSounds.Visible = false;

        WelcomeNext.OnClick = _ => Advance();
        UpdatesDecline.OnClick = _ => AnswerUpdates(false);
        UpdatesAccept.OnClick = _ => AnswerUpdates(true);
        SoundsStart.OnClick = _ =>
        {
            OnStartLoading?.Invoke();
            BeginLoading();
        };

        HookHover(WelcomeNext, ActionRgb, ActionHoverRgb);
        HookHover(UpdatesAccept, ActionRgb, ActionHoverRgb);
        HookHover(SoundsStart, ActionRgb, ActionHoverRgb);
        HookHover(UpdatesDecline, QuietRgb, QuietHoverRgb);

        RootPanel.DrawTo(context);
    }

    /// <summary>Raised with the user's answer when the update question is answered.</summary>
    public Action<bool>? OnUpdateAnswer { get; set; }

    /// <summary>Raised when the user starts the download on the last setup pane.</summary>
    public Action? OnStartLoading { get; set; }

    [UsedImplicitly] public SundexComponent Component { get; }

    [SetFromLogic] public Panel RootPanel { get; set; } = null!;
    [SetFromLogic] public Panel Strip { get; set; } = null!;
    [SetFromLogic] public FlexPanel Ticks { get; set; } = null!;

    [SetFromLogic] public Panel Status { get; set; } = null!;
    [SetFromLogic] public Label StatusMessage { get; set; } = null!;
    [SetFromLogic] public Label StatusDetail { get; set; } = null!;
    [SetFromLogic] public Label StatusPercent { get; set; } = null!;

    [SetFromLogic] public Panel PaneWelcome { get; set; } = null!;
    [SetFromLogic] public Panel PaneUpdates { get; set; } = null!;
    [SetFromLogic] public Panel PaneSounds { get; set; } = null!;

    [SetFromLogic] public Checkbox IncludePrereleases { get; set; } = null!;
    [SetFromLogic] public Checkbox IncludeNightlies { get; set; } = null!;

    [SetFromLogic] public Button WelcomeNext { get; set; } = null!;
    [SetFromLogic] public Button UpdatesDecline { get; set; } = null!;
    [SetFromLogic] public Button UpdatesAccept { get; set; } = null!;
    [SetFromLogic] public Button SoundsStart { get; set; } = null!;

    /// <summary>Segments in the meter while the setup is up - one per pane it will show.</summary>
    public int SetupTicks => _panes.Count;

    /// <summary>The pane currently up, or null once the setup is behind us.</summary>
    public Panel? CurrentPane => _setupStep >= 0 && _setupStep < _panes.Count ? _panes[_setupStep] : null;

    /// <summary>True once <see cref="BeginLoading" /> has run - the status line owns the strip.</summary>
    public bool Loading => _loading;

    /// <summary>
    ///     Opens the first-run setup: the strip rises to full height with the greeting on it.
    /// </summary>
    /// <param name="includeUpdates">
    ///     Whether to ask about update checking. False on a build with no release date behind
    ///     it, where there is nothing to call a release newer than - the setup is then the
    ///     greeting and the download, and the meter counts two instead of three.
    /// </param>
    public void BeginSetup(bool includeUpdates = true)
    {
        _panes.Clear();
        _panes.Add(PaneWelcome);
        if (includeUpdates) _panes.Add(PaneUpdates);
        _panes.Add(PaneSounds);

        _setupStep = 0;
        _progress = 1f / SetupTicks;
        PaneWelcome.Visible = true;

        _stripTickRebuild = SetupTicks;
        AnimateStrip(SetupHeight, RiseSeconds, 0f, Strip);
    }

    /// <summary>
    ///     Hands the strip over to the download readout. From the setup this is the settle -
    ///     the open pane fades out and the strip compacts onto the status line, the meter
    ///     re-resolving from three segments to thirty as it goes. On every later boot it is
    ///     just the rise, straight to the short strip.
    /// </summary>
    public void BeginLoading()
    {
        if (_loading) return;
        _loading = true;
        _progress = 0f;

        var fromSetup = _setupStep >= 0;
        if (fromSetup) FadeTo(CurrentPane, null);
        _setupStep = -1;

        Status.Visible = true;
        _stripTickRebuild = LoadTicks;

        if (!fromSetup)
        {
            AnimateStrip(LoadHeight, RiseSeconds, 0f, Strip);
            return;
        }

        // The status line rides the settle in, and the settle waits for the pane to clear.
        AnimateStrip(LoadHeight, SettleSeconds, PaneSeconds * 0.5f, Status);
    }

    /// <summary>
    ///     Runs the screen off: the strip settles back to the zero height it rose from on
    ///     boot, and everything on it fades with it. Called by <see cref="LoadingScene.Loader" />
    ///     once the home screen is up behind this one.
    /// </summary>
    public void BeginExit()
    {
        _exitClock.Restart();
        AnimateStrip(0f, ExitSeconds, 0f, null);
    }

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
    }

    public void Update(IProgressReport progressReport, UIContext context)
    {
        UpdateStrip();
        UpdatePanes();

        if (_loading)
        {
            StatusMessage.SetTextContents(progressReport.Message);
            StatusDetail.SetTextContents(progressReport.Detail ?? "");
            StatusPercent.SetTextContents($"{progressReport.Percentage * 100:0}%");
            _progress = (float)progressReport.Percentage;
        }

        PaintTicks();
        RootPanel.Update(context);
        RootPanel.Layout();

        // Last, and every frame: PaintTicks' SetClass and the hovered-state overrides both
        // re-run the stylesheet, which puts the styled alpha straight back.
        UpdateExit();
    }

    private void UpdateExit()
    {
        if (!_exitClock.IsRunning) return;

        var progress = (float)_exitClock.Elapsed.TotalSeconds / ExitSeconds;
        if (progress >= 1f)
        {
            progress = 1f;
            _exitClock.Stop();
        }

        SetAlpha(RootPanel, 1f - EaseOut(progress));
    }

    private void AnswerUpdates(bool optIn)
    {
        OnUpdateAnswer?.Invoke(optIn);
        Advance();
    }

    /// <summary>Moves to the next pane in <see cref="_panes" />, whatever this setup's shape is.</summary>
    private void Advance()
    {
        if (_setupStep < 0 || _setupStep + 1 >= _panes.Count) return;

        var previous = CurrentPane;
        _setupStep++;
        _progress = (_setupStep + 1f) / SetupTicks;
        FadeTo(previous, CurrentPane);
    }

    // ------------------------------------------------------------ the meter

    /// <summary>
    ///     Rebuilds the meter at a given resolution. The segments are equal percentages of
    ///     the row, so they stay the full width of the strip whatever the window size is.
    /// </summary>
    private void BuildTicks(int count)
    {
        // RemoveChild rather than replacing the list: the render queue is retained, so
        // segments dropped on the floor would keep painting under the new row forever.
        foreach (var old in _ticks) Ticks.RemoveChild(old);
        _ticks.Clear();

        for (var index = 0; index < count; index++)
        {
            var tick = new Panel(_context)
            {
                Width = new LiteralOrComputable(100f / count, true)
            };
            // Set before parenting: AddChild is what applies the stylesheet, and it needs
            // the class already on the element to pick up the tick rules.
            tick.SetClass("tick", true);
            Ticks.AddChild(tick);
            _ticks.Add(tick);
        }

        PaintTicks();
    }

    /// <summary>
    ///     Lights every segment the progress has passed and dims the one it is inside, so the
    ///     meter reads as an edge rather than a hard boundary.
    /// </summary>
    private void PaintTicks()
    {
        var reached = _progress * _ticks.Count;
        for (var index = 0; index < _ticks.Count; index++)
        {
            var passed = index < reached;
            _ticks[index].SetClass("tick-lit", passed);
            _ticks[index].SetClass("tick-next", !passed && index < reached + 1f);
        }
    }

    // ------------------------------------------------------------ motion

    private void AnimateStrip(float to, float seconds, float delay, UIElement? fadeTarget)
    {
        _stripFrom = Strip.Height.Value;
        _stripTo = to;
        _stripSeconds = seconds;
        _stripDelay = delay;
        _stripFadeTarget = fadeTarget;
        if (fadeTarget is not null) SetAlpha(fadeTarget, 0f);
        _stripClock.Restart();
    }

    private void UpdateStrip()
    {
        if (!_stripClock.IsRunning) return;

        var elapsed = (float)_stripClock.Elapsed.TotalSeconds - _stripDelay;
        if (elapsed < 0f) return;

        if (_stripTickRebuild is { } count)
        {
            _stripTickRebuild = null;
            BuildTicks(count);
        }

        var progress = _stripSeconds > 0f ? elapsed / _stripSeconds : 1f;
        if (progress >= 1f)
        {
            progress = 1f;
            _stripClock.Stop();
        }

        var eased = EaseOut(progress);
        Strip.Height = _stripFrom + (_stripTo - _stripFrom) * eased;
        if (_stripFadeTarget is { } target) SetAlpha(target, eased);
    }

    private void FadeTo(Panel? outgoing, Panel? incoming)
    {
        _paneOut = outgoing;
        _paneIn = incoming;
        _paneClock.Restart();

        if (incoming is null) return;
        incoming.Visible = true;
        SetAlpha(incoming, 0f);
    }

    /// <summary>
    ///     Runs the pane swap: the outgoing one fades and slides off over the first half,
    ///     the incoming one arrives over the second, so the two are never both readable.
    /// </summary>
    private void UpdatePanes()
    {
        if (!_paneClock.IsRunning) return;

        var progress = (float)_paneClock.Elapsed.TotalSeconds / PaneSeconds;
        var finished = progress >= 1f;
        if (finished) progress = 1f;

        if (_paneOut is { } outgoing)
        {
            var alpha = Math.Clamp(1f - progress * 2f, 0f, 1f);
            SetAlpha(outgoing, alpha);
            outgoing.X = -SlidePx * (1f - alpha);

            if (alpha <= 0f)
            {
                outgoing.X = 0f;
                outgoing.Visible = false;
                _paneOut = null;
            }
        }

        if (_paneIn is { } incoming)
        {
            var alpha = Math.Clamp(progress * 2f - 1f, 0f, 1f);
            SetAlpha(incoming, alpha);
            incoming.X = SlidePx * (1f - alpha);
        }

        if (!finished) return;
        _paneClock.Stop();
        _paneIn = null;
        _paneOut = null;
    }

    private static float EaseOut(float progress)
    {
        var inverse = 1f - progress;
        return 1f - inverse * inverse * inverse;
    }

    // ------------------------------------------------------------ alpha

    private void SetAlpha(UIElement element, float alpha)
    {
        _alpha.Apply(element, alpha);
    }

    private static void HookHover(Button button, Vector3 baseRgb, Vector3 hoverRgb)
    {
        button.OnHoverEnter = _ => SetRgb(button, hoverRgb);
        button.OnHoverExit = _ => SetRgb(button, baseRgb);
    }

    private static void SetRgb(Panel panel, Vector3 rgb)
    {
        if (panel.Background is not { } background) return;
        background.Color = new Vector4(rgb, background.Color.W);
    }
}
