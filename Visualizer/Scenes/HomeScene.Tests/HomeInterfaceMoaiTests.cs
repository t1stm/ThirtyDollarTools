using HomeScene.Scenes;
using OpenTK.Mathematics;

namespace HomeScene.Tests;

/// <summary>
///     The two things that keep the home screen alive after the arrival lands: the band
///     keeps being read, and the moai answers it. Neither is visible in this environment -
///     the app is a GLFW window that can't be screenshotted here - so a sweep that quietly
///     stops, or a head that never gets its texture, would only surface on launch.
/// </summary>
public class HomeInterfaceMoaiTests
{
    private readonly HomeTestContext _context = new();

    private HomeInterface Build()
    {
        return new HomeInterface(_context, () => { }, () => { }, () => { }, () => { });
    }

    /// <summary>Runs the moai's fetch to completion and hands the pixels to the element.</summary>
    private async Task SettleMoai(HomeInterface home)
    {
        if (home.Moai.LoadTask is { } task) await task;
        // Rethrows anything the worker threw, so a bad src or storage fails loudly here
        // rather than leaving an empty box next to the wordmark.
        _context.AssetProvider.ThreadRunner.Update();
        home.Moai.Update(_context);
    }

    [Fact]
    public async Task Moai_ResolvesItsTextureFromTheSharedAssembly()
    {
        var home = Build();
        await SettleMoai(home);

        Assert.True(home.Moai.IsLoaded, "the masthead moai never got a texture");
        Assert.Equal(256, home.Moai.Texture!.Width);
    }

    /// <summary>
    ///     The arrival used to stop the sweep and hide the playhead, which left the screen
    ///     with nothing moving on it at all. It now drops to the idle loop instead.
    /// </summary>
    [Fact]
    public void AfterTheArrivalLands_TheBandKeepsBeingRead()
    {
        var home = Build();
        home.PlayIntro();

        // Past SweepSeconds, so the pass under test is one the arrival has already ended.
        Thread.Sleep(2100);
        home.Update(_context);

        Assert.False(home.Playhead.Visible, "the playhead is still crossing after the arrival");
        Assert.Contains(home.Steps, step => step.Classes.Contains("lit-idle"));
        Assert.DoesNotContain(home.Steps, step => step.Classes.Contains("lit"));
    }

    /// <summary>
    ///     Coming back from a tool has to replay the arrival. The guard that stops the
    ///     loader's second TransitionTo from restarting a running sweep must not also
    ///     swallow this one, which is what a bare IsRunning check would do now that the
    ///     sweep never stops.
    /// </summary>
    [Fact]
    public void PlayIntro_OverAnIdleLoop_ReplaysTheArrival()
    {
        var home = Build();
        home.PlayIntro();

        Thread.Sleep(2100);
        home.Update(_context);
        Assert.False(home.Playhead.Visible);

        home.PlayIntro();
        home.Update(_context);
        Assert.True(home.Playhead.Visible, "returning to the home screen no longer replays the arrival");
    }

    /// <summary>
    ///     The hit: the head hops off its rest line and takes on the cell's colour, then
    ///     both drain back out. Asserted at the start of the arrival, the one point whose
    ///     timing is known without controlling the clock.
    /// </summary>
    [Fact]
    public async Task TheFirstCell_BouncesTheHeadAndWashesItsColourThroughIt()
    {
        var home = Build();
        await SettleMoai(home);

        var restY = home.Moai.Y.Value;
        var stone = home.Moai.Background!;
        Assert.Equal(Vector4.One, stone.Color);

        home.PlayIntro();
        home.Update(_context);

        // The bounce starts from zero, so it is still on the rest line on the frame the hit
        // lands. Read a beat later, inside BounceAnimation's 400ms and the first cell.
        Thread.Sleep(120);
        home.Update(_context);

        // Up, the way the visualizer bounces a sound - never down into the wordmark.
        Assert.True(home.Moai.Y.Value < restY, "the head never left its rest line");

        // The first cell is the blue one, so blue is the channel left standing.
        Assert.True(stone.Color.Z > stone.Color.X, "the head took on no colour from the cell");
        Assert.Equal(1f, stone.Color.W, 4);
    }

    /// <summary>
    ///     The moai draws through TexturedPlane, whose shader used to ignore Color entirely -
    ///     the head would have punched through the loading screen's hand-off at full opacity
    ///     while every other element on the screen was still fading up.
    /// </summary>
    [Fact]
    public async Task TheEntranceFade_ReachesTheMoai()
    {
        var home = Build();
        await SettleMoai(home);

        home.Alpha = 0f;
        home.Update(_context);
        Assert.Equal(0f, home.Moai.Background!.Color.W, 4);

        home.Alpha = 1f;
        home.Update(_context);
        Assert.Equal(1f, home.Moai.Background!.Color.W, 4);
    }
}
