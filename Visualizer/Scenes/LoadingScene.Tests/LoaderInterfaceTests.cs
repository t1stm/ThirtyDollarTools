using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using LoadingScene.Reports;
using LoadingScene.Scenes;
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

namespace LoadingScene.Tests;

/// <summary>Headless UIContext over the LoadingScene assembly's embedded assets.</summary>
public class LoaderTestContext : UIContext
{
    [SetsRequiredMembers]
    public LoaderTestContext()
    {
        InjectForTesting(
            new AssetProvider(new LoggerConfiguration().CreateLogger(),
                [typeof(LoaderInterface).Assembly, Assembly.GetExecutingAssembly()], new GLInfo()),
            new MockFontProvider(),
            new MockTextProvider());
        Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1600, 900));
    }
}

public class FakeReport : IProgressReport
{
    public required string Message { get; init; }
    public required double Percentage { get; init; }
    public string? Detail { get; init; }
}

/// <summary>
///     Headless cover for the loading screen: its layout and stylesheet parse, the ids its
///     logic block looks up are all there, and the first-run setup walks from the greeting
///     to the download. The app is a GLFW window that can't be screenshotted in this
///     environment, so nothing else would catch a renamed id or a broken step order.
/// </summary>
public class LoaderInterfaceTests
{
    private readonly LoaderTestContext _context = new();

    private static FakeReport Report(double percentage)
    {
        return new FakeReport { Message = "Downloading sounds", Percentage = percentage };
    }

    private string LoadMarkup()
    {
        return _context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/LoaderInterface.snx.xml",
                Storage = StorageLocation.Assembly
            }
        }).Value;
    }

    /// <summary>Every id LoaderInterface.snx.csx reaches for.</summary>
    [Theory]
    [InlineData("stage")]
    [InlineData("strip")]
    [InlineData("strip-rule")]
    [InlineData("ticks")]
    [InlineData("status")]
    [InlineData("status-message")]
    [InlineData("status-detail")]
    [InlineData("status-percent")]
    [InlineData("pane-welcome")]
    [InlineData("pane-updates")]
    [InlineData("pane-sounds")]
    [InlineData("include-prereleases")]
    [InlineData("include-nightlies")]
    [InlineData("welcome-next")]
    [InlineData("updates-decline")]
    [InlineData("updates-accept")]
    [InlineData("sounds-start")]
    public void LoaderMarkup_RegistersEveryWiredId(string id)
    {
        var component = new SundexContext(_context).NewComponent(LoadMarkup());
        Assert.True(component.RegisteredIDs.ContainsKey(id), $"markup lost id \"{id}\"");
    }

    /// <summary>
    ///     A class in markup that no rule defines is silent - the element keeps its bare
    ///     framework defaults and renders wrong.
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

    /// <summary>The meter's classes, which only ever reach an element from code.</summary>
    [Theory]
    [InlineData("tick")]
    [InlineData("tick-lit")]
    [InlineData("tick-next")]
    public void StylesheetDefines_TheClassesSetFromCode(string cls)
    {
        var component = new SundexContext(_context).NewComponent(LoadMarkup());
        Assert.True(component.StyleSheet?.Classes.ContainsKey(cls), $"stylesheet lost class \"{cls}\"");
    }

    /// <summary>
    ///     The loader stopped importing this sheet when the sound field became its background,
    ///     but SettingsScene and DrumMasterScene still do - by path, from other assemblies, so
    ///     nothing in this project mentions it. Dropping it from the build takes both of those
    ///     screens down at their first frame and no compiler notices.
    /// </summary>
    [Fact]
    public void LoaderGradients_StaysEmbedded_ForTheSheetsInOtherAssembliesThatImportIt()
    {
        var names = typeof(LoaderInterface).Assembly.GetManifestResourceNames();
        Assert.Contains("LoadingScene.Scenes.Layout.LoaderGradients.snx.ss", names);
    }

    /// <summary>
    ///     The atmosphere behind the setup. The sound field is empty until the download
    ///     starts, so on a first run these gradients are the only thing on screen behind
    ///     three panes of text - and they only reach the tree if the import in
    ///     LoaderInterface.snx.ss resolves, which nothing else in this suite exercises.
    /// </summary>
    [Theory]
    [InlineData("grad-1")]
    [InlineData("grad-2")]
    [InlineData("grad-3")]
    [InlineData("grad-4")]
    [InlineData("grad-5")]
    public void TheAtmosphere_IsStyledByTheImportedGradientSheet(string id)
    {
        var component = new SundexContext(_context).NewComponent(LoadMarkup());

        Assert.True(component.RegisteredIDs.ContainsKey(id), $"markup lost id \"{id}\"");
        Assert.True(component.StyleSheet?.IDTags.ContainsKey(id),
            $"\"{id}\" has no rule - the LoaderGradients import did not resolve");
    }

    /// <summary>
    ///     Siblings paint in declaration order, so the atmosphere has to come first or the
    ///     gradients cover the strip and every setup pane with it.
    /// </summary>
    [Fact]
    public void TheAtmosphere_PaintsUnderTheStrip()
    {
        var loader = new LoaderInterface(_context);
        var order = loader.RootPanel.Children.ToList();

        var atmosphere = order.FindIndex(child => child.ID == "atmosphere");
        var strip = order.FindIndex(child => child.ID == "strip");

        Assert.True(atmosphere >= 0, "the stage lost the atmosphere");
        Assert.True(atmosphere < strip, $"atmosphere paints over the strip: {atmosphere} vs {strip}");
    }

    /// <summary>
    ///     The gradients are full-bleed panels laid over the whole stage, including the strip.
    ///     Hit testing keeps the deepest, last-declared element, so the setup's buttons still
    ///     win the pointer - if they ever stopped, the first run would be unclickable and no
    ///     other test here would notice, because they all call OnClick directly.
    /// </summary>
    [Fact]
    public void TheAtmosphere_DoesNotSwallowTheSetupButtons()
    {
        var loader = new LoaderInterface(_context);
        loader.BeginSetup();
        loader.Update(Report(0), _context);

        var button = loader.WelcomeNext;
        var x = button.Computed.AbsoluteX + button.Computed.Width / 2f;
        var y = button.Computed.AbsoluteY + button.Computed.Height / 2f;

        var hit = loader.RootPanel.HitTest(x, y);
        Assert.NotNull(hit);

        for (var element = hit; element != null; element = element.Parent)
            if (ReferenceEquals(element, button))
                return;

        Assert.Fail($"the atmosphere took the pointer at ({x}, {y}): hit \"{hit.ID}\"");
    }

    /// <summary>
    ///     A boot past the first: no setup, the status strip straight away, and the meter at
    ///     the download's resolution with everything behind the progress lit.
    /// </summary>
    [Fact]
    public void ReturningBoot_ShowsTheStatusStripAtTheDownloadResolution()
    {
        var loader = new LoaderInterface(_context);
        loader.BeginLoading();
        loader.Update(Report(0.5), _context);

        Assert.True(loader.Loading);
        Assert.True(loader.Status.Visible);
        Assert.Null(loader.CurrentPane);
        Assert.Equal(LoaderInterface.LoadTicks, loader.Ticks.Children.Count);

        var lit = loader.Ticks.Children.Count(tick => tick.Classes.Contains("tick-lit"));
        Assert.Equal(LoaderInterface.LoadTicks / 2, lit);
    }

    /// <summary>
    ///     The first run, click by click: greeting, update question, download. The answer
    ///     reaches the scene and the last button hands the strip over to loading.
    /// </summary>
    [Fact]
    public void FirstRun_WalksTheSetupAndHandsOverToLoading()
    {
        bool? answer = null;
        var started = false;

        var loader = new LoaderInterface(_context)
        {
            OnUpdateAnswer = optIn => answer = optIn,
            OnStartLoading = () => started = true
        };

        loader.BeginSetup();
        loader.Update(Report(0), _context);

        Assert.Same(loader.PaneWelcome, loader.CurrentPane);
        Assert.True(loader.PaneWelcome.Visible);
        Assert.Equal(3, loader.SetupTicks);
        Assert.Equal(loader.SetupTicks, loader.Ticks.Children.Count);
        Assert.Equal(1, loader.Ticks.Children.Count(tick => tick.Classes.Contains("tick-lit")));

        loader.WelcomeNext.OnClick!(loader.WelcomeNext);
        Assert.Same(loader.PaneUpdates, loader.CurrentPane);
        Assert.True(loader.PaneUpdates.Visible);

        loader.UpdatesAccept.OnClick!(loader.UpdatesAccept);
        Assert.True(answer);
        Assert.Same(loader.PaneSounds, loader.CurrentPane);

        loader.SoundsStart.OnClick!(loader.SoundsStart);
        Assert.True(started);
        Assert.True(loader.Loading);
        Assert.Null(loader.CurrentPane);
    }

    /// <summary>
    ///     A build with no release date behind it - a local build, which is every build a
    ///     developer runs. It still gets the setup; only the update question is dropped, and
    ///     the meter counts two. Gating the whole setup on the date meant it never appeared
    ///     on any machine that hadn't downloaded a release.
    /// </summary>
    [Fact]
    public void WithoutAReleaseDate_TheSetupSkipsTheUpdateQuestion()
    {
        var answered = false;
        var loader = new LoaderInterface(_context) { OnUpdateAnswer = _ => answered = true };

        loader.BeginSetup(false);
        loader.Update(Report(0), _context);

        Assert.Equal(2, loader.SetupTicks);
        Assert.Equal(2, loader.Ticks.Children.Count);
        Assert.Same(loader.PaneWelcome, loader.CurrentPane);

        loader.WelcomeNext.OnClick!(loader.WelcomeNext);
        Assert.Same(loader.PaneSounds, loader.CurrentPane);
        Assert.False(loader.PaneUpdates.Visible);
        Assert.False(answered);

        loader.SoundsStart.OnClick!(loader.SoundsStart);
        Assert.True(loader.Loading);
    }

    /// <summary>
    ///     The strip is grown from zero height by the update loop - the one thing that would
    ///     leave the whole screen blank if it ever stopped running.
    /// </summary>
    [Fact]
    public void TheStrip_RisesWhileTheLoopRuns()
    {
        var loader = new LoaderInterface(_context);
        loader.BeginLoading();

        loader.Update(Report(0), _context);
        var first = loader.Strip.Height.Value;

        Thread.Sleep(80);
        loader.Update(Report(0), _context);

        Assert.True(loader.Strip.Height.Value > first,
            $"the strip did not grow: {first} -> {loader.Strip.Height.Value}");
    }
}
