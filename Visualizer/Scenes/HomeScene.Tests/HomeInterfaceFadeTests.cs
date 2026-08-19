using HomeScene.Scenes;
using Sundex.Components.Panels;

namespace HomeScene.Tests;

/// <summary>
///     The entrance fade the loading screen drives. Two things here are easy to break and
///     invisible until launch: the fade has to be re-applied at the <em>end</em> of Update
///     (the sweep's SetClass and the hovered-state overrides both re-run the stylesheet,
///     which puts the styled alpha straight back), and landing on 1 has to restore the
///     styled alpha exactly rather than forcing everything opaque.
/// </summary>
public class HomeInterfaceFadeTests
{
    private readonly HomeTestContext _context = new();

    private static float StageAlpha(HomeInterface home)
    {
        return home.RootPanel.Background!.Color.W;
    }

    private HomeInterface Build()
    {
        return new HomeInterface(_context, () => { }, () => { }, () => { }, () => { });
    }

    [Fact]
    public void Update_AppliesTheAlphaAfterTheSweepHasRestyled()
    {
        var home = Build();
        var styled = StageAlpha(home);
        Assert.True(styled > 0f, "the stage resolved to a fully transparent background");

        // The sweep is what re-runs the stylesheet mid-frame, so it has to be running for
        // this to test anything: without it the ordering bug cannot show up.
        home.PlayIntro();
        home.Alpha = 0f;
        home.Update(_context);

        Assert.Equal(0f, StageAlpha(home), 4);
        Assert.All(home.Steps, step => Assert.Equal(0f, ((Panel)step).Background!.Color.W, 4));
    }

    [Fact]
    public void Update_ScalesAgainstTheStyledAlphaRatherThanOverwritingIt()
    {
        var home = Build();
        var styled = StageAlpha(home);

        home.Alpha = 0.5f;
        home.Update(_context);
        Assert.Equal(styled * 0.5f, StageAlpha(home), 4);

        home.Alpha = 1f;
        home.Update(_context);
        Assert.Equal(styled, StageAlpha(home), 4);
    }

    /// <summary>
    ///     The loader transitions to Home twice - once to fade it up over itself, once to
    ///     drop itself - and TransitionedTo calls PlayIntro each time. Restarting on the
    ///     second would snap the playhead back to the left edge just as the loading screen
    ///     clears, which is exactly the seam the hand-off exists to hide.
    /// </summary>
    [Fact]
    public void PlayIntro_DoesNotRestartASweepAlreadyRunning()
    {
        var home = Build();
        home.PlayIntro();
        home.Update(_context);

        Thread.Sleep(60);
        home.Update(_context);
        var advanced = home.Playhead.X.Value;
        Assert.True(advanced > 0f, "the playhead never left the left edge");

        home.PlayIntro();
        home.Update(_context);
        Assert.True(home.Playhead.X.Value >= advanced, "PlayIntro restarted a running sweep");
    }
}
