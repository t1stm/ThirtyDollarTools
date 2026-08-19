using System.Diagnostics;
using JetBrains.Annotations;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using Shared.Animations;
using Sundex.Markup.Attributes;
using Image = Sundex.Components.Panels.Image;

namespace HomeScene.Scenes;

public class HomeInterface
{
    /// <summary>Steps drawn under each tool's name - one bar's worth.</summary>
    public const int StepsPerCell = 8;

    /// <summary>
    ///     How long the playhead takes to cross the band, once, on entry.
    ///     <para>
    ///         This is the last beat of the boot transition and what sets its length: the
    ///         loading screen starts the sweep 0.5s into its exit (Loader.ExitFadeStart) and
    ///         is gone at 1.5s, so 2.0s here puts the playhead landing at 2.5s, a full second
    ///         after the screen it was handed off from has cleared.
    ///     </para>
    /// </summary>
    private const float SweepSeconds = 2.0f;

    /// <summary>
    ///     How long one idle crossing takes. Eight times the arrival's, because it never
    ///     stops: the band is a transport, and a transport that has finished playing is a
    ///     screen with nothing left alive on it. Slow enough to read as ambient.
    /// </summary>
    private const float IdleSweepSeconds = 16.0f;

    /// <summary>
    ///     How far past a step the playhead travels before that step goes dark again,
    ///     in steps. Wide enough that the row reads as a trail rather than a single blink.
    /// </summary>
    private const float LitTrailSteps = 2.5f;

    /// <summary>
    ///     What fraction of its own height a bounced thing travels. Lifted from
    ///     SoundRenderable, which sets its BounceAnimation's FinalY the same way: a moai
    ///     popping over a step here moves like a sound popping over a note there.
    /// </summary>
    private const float BounceHeightDivisor = 4.26666667f;

    /// <summary>
    ///     How long the tool's colour takes to drain back out of the head, and how far
    ///     towards that colour the hit pushes it. Longer than the nod: the movement is the
    ///     hit, the colour is which tool was hit, and that is worth reading after.
    /// </summary>
    private const float TintSeconds = 0.9f;

    private const float TintStrength = 0.75f;

    /// <summary>Owns the entrance fade - see <see cref="Alpha" />.</summary>
    private readonly ElementAlpha _alpha = new();

    private readonly Stopwatch _sweep = new();

    /// <summary>Time since the head was last hit - see <see cref="UpdateMoai" />.</summary>
    private readonly Stopwatch _hit = new();

    /// <summary>
    ///     The head's hop, on the visualizer's own bounce curve: up fast, eased back down,
    ///     400ms. Held and read per frame rather than handed to the element's animation
    ///     list, which is how SoundRenderable drives the same animation.
    /// </summary>
    private readonly BounceAnimation _bounce = new();

    /// <summary>
    ///     Each cell's resting step colour, scaled so its brightest channel is 1. Read off
    ///     the built steps rather than repeated here: the stylesheet owns what a tool's
    ///     colour is, and a tint mixed from the unscaled value would darken the head
    ///     instead of colouring it.
    /// </summary>
    private readonly List<Vector3> _cellTints = [];

    /// <summary>True once the arrival sweep has landed and the quiet loop has taken over.</summary>
    private bool _idle;

    /// <summary>The cell the head has already reacted to, so it reacts once per entry.</summary>
    private int _lastCell = -1;

    /// <summary>The colour the current hit is draining from.</summary>
    private Vector3 _hitTint = Vector3.One;

    /// <summary>The moai's styled y, which the nod is measured from.</summary>
    private float _moaiRestY;

    public HomeInterface(UIContext context, Action visualizer, Action drumMaster, Action editor, Action settings)
    {
        var sundexContext = new SundexContext(context);
        var componentSource = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/HomeInterface.snx.xml"
            }
        });

        UI = context;
        OnVisualizer = visualizer;
        OnEditor = editor;
        OnDrumMaster = drumMaster;
        OnSettings = settings;

        Component = sundexContext.NewComponent(componentSource.Value);
        sundexContext.RunLogicAndVerify(Component, () => RootPanel);
        RootPanel.DrawTo(context);

        _moaiRestY = Moai.Y.Value;
        _bounce.FinalY = Moai.Height.Value / BounceHeightDivisor;
        for (var cell = 0; cell < Steps.Count / StepsPerCell; cell++)
            _cellTints.Add(Brighten(Steps[cell * StepsPerCell].Background?.Color.Xyz ?? Vector3.One));
    }

    /// <summary>Scales a colour so its brightest channel is 1, leaving its hue alone.</summary>
    private static Vector3 Brighten(Vector3 color)
    {
        var peak = Math.Max(color.X, Math.Max(color.Y, color.Z));
        return peak > 0f ? color / peak : Vector3.One;
    }

    /// <summary>The context the logic block builds the step panels against.</summary>
    public UIContext UI { get; }

    public Action OnVisualizer { get; }
    public Action OnEditor { get; }
    public Action OnDrumMaster { get; }
    public Action OnSettings { get; }

    [UsedImplicitly] public SundexComponent Component { get; }

    [SetFromLogic] public Panel RootPanel { get; set; } = null!;
    [SetFromLogic] public Panel Band { get; set; } = null!;
    [SetFromLogic] public Image Moai { get; set; } = null!;
    [SetFromLogic] public Panel Playhead { get; set; } = null!;
    [SetFromLogic] public Label VersionLabel { get; set; } = null!;
    [SetFromLogic] public Label UpdateLabel { get; set; } = null!;

    /// <summary>Every cell's steps, left to right, as one row - the sweep reads them in order.</summary>
    public List<Panel> Steps { get; } = [];

    /// <summary>
    ///     Scene-wide opacity. The loading screen drives this from 0 to 1 as it fades itself
    ///     off over this one; on every later entry it is already 1 and costs nothing.
    /// </summary>
    public float Alpha { get; set; } = 1f;

    /// <summary>
    ///     The last value <see cref="Update" /> pushed into the tree. Kept so the frame
    ///     the fade lands on 1 still gets applied - stopping at the guard there would
    ///     strand the whole screen on the last faded value - and so the helper lets go of
    ///     the tree's colours afterwards, leaving hover states to own their own alpha.
    /// </summary>
    private float _appliedAlpha = 1f;

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    /// <summary>
    ///     Runs the bright playhead across the band once, then hands over to the idle loop.
    ///     Called on every entry to the scene, so coming back from a tool replays the
    ///     arrival. An arrival already running is left alone: on boot the loading screen
    ///     hands off to this one mid-fade, and the scene is then transitioned to a second
    ///     time when the loader is dropped - restarting there would break the one motion
    ///     the hand-off exists to make. The idle loop is not an arrival, so it is replaced.
    /// </summary>
    public void PlayIntro()
    {
        if (_sweep.IsRunning && !_idle) return;
        _idle = false;
        _lastCell = -1;
        _sweep.Restart();
    }

    public void Update(UIContext context)
    {
        UpdateSweep();
        RootPanel.Update(context);
        RootPanel.Layout();

        // Last, and every frame while fading: UpdateSweep's SetClass and the hovered-state
        // overrides both re-run the stylesheet, which puts the styled alpha straight back.
        if (Alpha >= 1f && _appliedAlpha >= 1f) return;
        _alpha.Apply(RootPanel, Alpha);
        _appliedAlpha = Alpha;
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
    }

    /// <summary>
    ///     Advances the playhead and lights the steps it has just passed. The steps carry no
    ///     animation of their own: what they show is a function of where the head is, which
    ///     is the same thing playback means everywhere else in this program.
    ///     <br /><br />
    ///     The sweep does not end, it drops in volume. The arrival crosses the band once
    ///     with the playhead lit, and then the same read loops on at
    ///     <see cref="IdleSweepSeconds" /> with the playhead gone and only the trail left -
    ///     a sequencer idling rather than a screen that has stopped.
    /// </summary>
    private void UpdateSweep()
    {
        if (!_sweep.IsRunning) return;

        var progress = (float)_sweep.Elapsed.TotalSeconds / (_idle ? IdleSweepSeconds : SweepSeconds);
        if (progress >= 1f)
        {
            _idle = true;
            _lastCell = -1;
            _sweep.Restart();
            progress = 0f;
        }

        // The playhead itself belongs to the arrival: a line crossing the screen forever is
        // something to watch, and this screen is something to click past.
        Playhead.Visible = !_idle;
        if (!_idle) Playhead.X = Band.Computed.Width * progress - Playhead.Computed.Width / 2f;

        var reached = progress * Steps.Count;
        for (var i = 0; i < Steps.Count; i++)
        {
            var distance = reached - i;
            var lit = distance >= 0 && distance < LitTrailSteps;

            // Both every frame: the pass that switches to idle has to take the bright class
            // back off the steps the arrival left behind it.
            Steps[i].SetClass("lit", lit && !_idle);
            Steps[i].SetClass("lit-idle", lit && _idle);
        }

        UpdateMoai(progress);
    }

    /// <summary>
    ///     Hits the head each time the playhead enters a new cell: a bounce, and that tool's
    ///     colour washing through the stone and draining out again. Driven off the sweep
    ///     rather than a clock of its own, so the one thing on this screen that moves is
    ///     answering the one thing that plays.
    /// </summary>
    private void UpdateMoai(float progress)
    {
        if (_cellTints.Count == 0 || Moai.Background is not { } stone) return;

        var cell = Math.Clamp((int)(progress * _cellTints.Count), 0, _cellTints.Count - 1);
        if (cell != _lastCell)
        {
            _lastCell = cell;
            _hitTint = _cellTints[cell];
            _hit.Restart();
            _bounce.Start();
        }

        // Negative y is up, and the animation returns zero once it has landed, so this is
        // also what puts the head back on its rest line.
        Moai.Y = _moaiRestY + _bounce.GetTransform_Add(stone).Y;

        var drain = (float)_hit.Elapsed.TotalSeconds / TintSeconds;
        var strength = drain < 1f ? (1f - drain) * TintStrength : 0f;
        var tint = Vector3.Lerp(Vector3.One, _hitTint, strength);

        // RGB only. The entrance fade owns w, and writing it here would fight the helper
        // that remembers what the stone was styled with.
        stone.Color = new Vector4(tint, stone.Color.W);
    }
}
