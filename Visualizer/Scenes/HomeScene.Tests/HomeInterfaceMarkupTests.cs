using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HomeScene.Scenes;
using OpenTK.Mathematics;
using Serilog;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Testing;
using Sundex.Engine.AssetManagement.Extensions;
using Sundex.Engine;
using Sundex.Engine.AssetManagement;
using Sundex.Engine.AssetManagement.Types.Asset;
using Sundex.Markup;
using Sundex.Style.DSL;
using Sundex.Style.DSL.Abstract.Values;

// UIContext stores its providers in static fields (see Sundex.Components.Tests),
// so this suite must also run sequentially.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace HomeScene.Tests;

/// <summary>Headless UIContext over the HomeScene assembly's embedded assets.</summary>
public class HomeTestContext : TestUIContext
{
    [SetsRequiredMembers]
    public HomeTestContext()
        : base(new LoggerConfiguration().CreateLogger(),
            [typeof(HomeInterface).Assembly, SharedAssembly.Assembly, Assembly.GetExecutingAssembly()])
    {
        Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1600, 900));
    }
}

/// <summary>
///     Headless smoke test for the home screen: its layout and stylesheet parse, and the
///     ids its logic block looks up are all there. Nothing else can catch a stylesheet
///     typo or a renamed id - the app is a GLFW window that can't be screenshotted in
///     this environment, so a broken home screen would otherwise only surface on launch.
/// </summary>
public class HomeInterfaceMarkupTests
{
    private readonly HomeTestContext _context = new();

    private StyleSheet LoadSheet()
    {
        return new SundexContext<object>(_context, new object()).LoadStyleSheet("Scenes/Layout/HomeInterface.snx.ss");
    }

    /// <summary>
    ///     The classes HomeInterface and the sweep apply by name. They are set from code,
    ///     so nothing else notices when a rule is renamed out from under them.
    /// </summary>
    [Theory]
    [InlineData("step")]
    [InlineData("step-blue")]
    [InlineData("step-orange")]
    [InlineData("step-green")]
    [InlineData("lit")]
    [InlineData("lit-idle")]
    [InlineData("note-attention")]
    public void StylesheetDefines_TheClassesSetFromCode(string cls)
    {
        Assert.True(LoadSheet().Classes.ContainsKey(cls), $"stylesheet lost class \"{cls}\"");
    }

    /// <summary>The note's base colour must lose to this class, which an id's would not.</summary>
    [Fact]
    public void UpdateNote_TurnsAmberWhenThereIsSomethingToActOn()
    {
        var home = new HomeInterface(_context, () => { }, () => { }, () => { }, () => { });
        var muted = home.UpdateLabel.Color;

        home.UpdateLabel.SetClass("note-attention", true);

        Assert.NotEqual(muted, home.UpdateLabel.Color);
        Assert.Equal(new ColorValue("#e0af68").Vector, home.UpdateLabel.Color);
    }

    /// <summary>
    ///     The whole screen, wired the way the scene wires it: the logic block builds the
    ///     steps and the sweep lights them from the left. Asserted at the start of the
    ///     sweep, the only point whose timing is known without controlling the clock.
    /// </summary>
    [Fact]
    public void PlayIntro_ShowsThePlayheadAndLightsTheFirstStepsOnly()
    {
        var home = new HomeInterface(_context, () => { }, () => { }, () => { }, () => { });
        Assert.Equal(3 * HomeInterface.StepsPerCell, home.Steps.Count);

        home.PlayIntro();
        home.Update(_context);

        // The sweep is a fraction of this: a band that resolved to nothing would park the
        // playhead at zero and still pass every assertion below.
        Assert.True(home.Band.Computed.Width > 0, "band resolved to no width");
        Assert.True(home.Playhead.Visible);
        Assert.Contains("lit", home.Steps[0].Classes);
        Assert.DoesNotContain("lit", home.Steps[^1].Classes);
    }
}
