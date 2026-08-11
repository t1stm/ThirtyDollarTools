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
using Sundex.Markup.Attributes;

namespace HomeScene.Scenes;

public class HomeInterface
{
    /// <summary>Steps drawn under each tool's name - one bar's worth.</summary>
    public const int StepsPerCell = 8;

    /// <summary>How long the playhead takes to cross the band, once, on entry.</summary>
    private const float SweepSeconds = 1.6f;

    /// <summary>
    ///     How far past a step the playhead travels before that step goes dark again,
    ///     in steps. Wide enough that the row reads as a trail rather than a single blink.
    /// </summary>
    private const float LitTrailSteps = 2.5f;

    private readonly Stopwatch _sweep = new();

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
    [SetFromLogic] public Panel Playhead { get; set; } = null!;
    [SetFromLogic] public Label VersionLabel { get; set; } = null!;
    [SetFromLogic] public Label UpdateLabel { get; set; } = null!;

    /// <summary>Every cell's steps, left to right, as one row - the sweep reads them in order.</summary>
    public List<Panel> Steps { get; } = [];

    public void Resize()
    {
        RootPanel.InvalidateCoordinates();
        RootPanel.Layout();
    }

    /// <summary>
    ///     Runs the playhead across the band once. Called on every entry to the scene, so
    ///     coming back from a tool replays it.
    /// </summary>
    public void PlayIntro()
    {
        _sweep.Restart();
    }

    public void Update(UIContext context)
    {
        UpdateSweep();
        RootPanel.Update(context);
        RootPanel.Layout();
    }

    public void MouseEvent(MouseState mouseState, Vector2 scale)
    {
        RootPanel.Test(mouseState, scale);
    }

    /// <summary>
    ///     Advances the playhead and lights the steps it has just passed. The steps carry no
    ///     animation of their own: what they show is a function of where the head is, which
    ///     is the same thing playback means everywhere else in this program.
    /// </summary>
    private void UpdateSweep()
    {
        if (!_sweep.IsRunning) return;

        var progress = (float)_sweep.Elapsed.TotalSeconds / SweepSeconds;
        if (progress >= 1f)
        {
            _sweep.Stop();
            Playhead.Visible = false;
            foreach (var step in Steps) step.SetClass("lit", false);
            return;
        }

        Playhead.Visible = true;
        Playhead.X = Band.Computed.Width * progress - Playhead.Computed.Width / 2f;

        var reached = progress * Steps.Count;
        for (var i = 0; i < Steps.Count; i++)
        {
            var distance = reached - i;
            Steps[i].SetClass("lit", distance >= 0 && distance < LitTrailSteps);
        }
    }
}
