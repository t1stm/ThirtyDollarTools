using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HomeScene.Scenes;
using OpenTK.Mathematics;
using Serilog;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Tests;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

// UIContext stores its providers in static fields (see Sundex.Components.Tests),
// so this suite must also run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HomeScene.Tests;

/// <summary>Headless UIContext over the HomeScene assembly's embedded assets.</summary>
public class HomeTestContext : UIContext
{
    [SetsRequiredMembers]
    public HomeTestContext()
    {
        InjectForTesting(
            new AssetProvider(new LoggerConfiguration().CreateLogger(),
                // Shared is in here for the same reason the app has it: the masthead's moai
                // is an embedded resource of that assembly, not of HomeScene.
                [typeof(HomeInterface).Assembly, SharedAssembly.Assembly, Assembly.GetExecutingAssembly()],
                new GLInfo()),
            new MockFontProvider(),
            new MockTextProvider());
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

    private string LoadMarkup()
    {
        return _context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/HomeInterface.snx.xml",
                Storage = StorageLocation.Assembly
            }
        }).Value;
    }

    /// <summary>Every id HomeInterface.snx.csx reaches for, and the four click targets.</summary>
    [Theory]
    [InlineData("stage")]
    [InlineData("band")]
    [InlineData("playhead")]
    [InlineData("moai")]
    [InlineData("version-label")]
    [InlineData("update-label")]
    [InlineData("cell-visualizer")]
    [InlineData("cell-drum-master")]
    [InlineData("cell-editor")]
    [InlineData("settings-button")]
    [InlineData("steps-visualizer")]
    [InlineData("steps-drum-master")]
    [InlineData("steps-editor")]
    public void HomeMarkup_RegistersEveryWiredId(string id)
    {
        var component = new SundexContext(_context).NewComponent(LoadMarkup());
        Assert.True(component.RegisteredIDs.ContainsKey(id), $"markup lost id \"{id}\"");
    }

    /// <summary>
    ///     A class in markup that no rule defines is silent - the element keeps its bare
    ///     framework defaults and renders wrong. The step classes are excluded: the logic
    ///     block sets those on panels it builds itself, so they never reach this map.
    /// </summary>
    [Fact]
    public void EveryClassInMarkup_IsDefinedByTheStylesheet()
    {
        var component = new SundexContext(_context).NewComponent(LoadMarkup());

        var missing = component.RegisteredClasses.Keys
            .Where(cls => component.StyleSheet?.Classes.ContainsKey(cls) != true)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    ///     The classes the logic block and the sweep apply by name. They are set from code,
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
        var component = new SundexContext(_context).NewComponent(LoadMarkup());
        Assert.True(component.StyleSheet?.Classes.ContainsKey(cls), $"stylesheet lost class \"{cls}\"");
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
