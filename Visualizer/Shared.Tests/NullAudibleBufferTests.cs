using Shared.Audio.Null;

namespace Shared.Tests;

/// <summary>
///     The silent buffer still has to keep time: under --no-audio it is what the editor
///     transport and the visualizer playhead read their position off.
/// </summary>
public class NullAudibleBufferTests
{
    [Fact]
    public void StartsStoppedAtZero()
    {
        var buffer = new NullAudibleBuffer();

        Assert.False(buffer.IsRunning);
        Assert.Equal(0, buffer.GetTime_Milliseconds());
    }

    [Fact]
    public void RunningFollowsSetPause()
    {
        var buffer = new NullAudibleBuffer();

        buffer.SetPause(false);
        Assert.True(buffer.IsRunning);

        buffer.SetPause(true);
        Assert.False(buffer.IsRunning);
    }

    /// <summary>
    ///     The gap between a project being loaded (which seeks to 0) and someone pressing
    ///     Play used to be counted as elapsed time, so the transport opened on however long
    ///     the editor had been sitting there.
    /// </summary>
    [Fact]
    public void StartingAfterASeekResumesFromTheSoughtTime()
    {
        var buffer = new NullAudibleBuffer();

        buffer.SeekTime_Milliseconds(5_000);
        Thread.Sleep(500);
        buffer.SetPause(false);

        Assert.InRange(buffer.GetTime_Milliseconds(), 5_000, 5_300);
    }

    [Fact]
    public void SeekMovesTheClockAndStopHoldsIt()
    {
        var buffer = new NullAudibleBuffer();

        buffer.SeekTime_Milliseconds(5_000);
        Assert.Equal(5_000, buffer.GetTime_Milliseconds());

        buffer.Stop();
        Assert.False(buffer.IsRunning);
        Assert.Equal(5_000, buffer.GetTime_Milliseconds());
    }
}
