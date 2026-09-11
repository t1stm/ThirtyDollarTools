namespace EditorScene.Tests;

/// <summary>
///     Where a wave reference clip's file has to be for a given transport position. The buffers
///     themselves need an audio device, but this is the arithmetic everything else rides on:
///     get it wrong and the reference plays against the song by a fixed offset.
/// </summary>
public class WavePlaybackTests
{
    private const double Bpm = 120;

    [Fact]
    public void ClipAtZero_StartsWithTheFirstSound_NotWithTheBuffer()
    {
        // The rendered mix begins EditorPlayback.LeadInSeconds before arrangement time zero,
        // so a clip placed at quarter 0 is still silent while the buffer plays its lead-in.
        Assert.Null(WavePlayback.FilePosition(0, 0, Bpm, 30));
        Assert.Null(WavePlayback.FilePosition(EditorPlayback.LeadInSeconds - 0.01, 0, Bpm, 30));
        Assert.Equal(0, WavePlayback.FilePosition(EditorPlayback.LeadInSeconds, 0, Bpm, 30));
        Assert.Equal(1, WavePlayback.FilePosition(EditorPlayback.LeadInSeconds + 1, 0, Bpm, 30)!.Value, 9);
    }

    [Fact]
    public void ClipFurtherAlong_StartsWhereItWasPlaced()
    {
        // 8 quarters at 120 BPM is 4 s into the arrangement, so 4.2 s into the buffer.
        Assert.Null(WavePlayback.FilePosition(4.1, 8, Bpm, 30));
        Assert.Equal(0, WavePlayback.FilePosition(4.2, 8, Bpm, 30)!.Value, 9);
        Assert.Equal(2, WavePlayback.FilePosition(6.2, 8, Bpm, 30)!.Value, 9);
    }

    [Fact]
    public void Tempo_MovesTheClipsStart_NotItsContents()
    {
        // The arrangement is anchored in quarter notes: double the tempo and the same clip
        // starts half as far in - but one second into the file is still one second in.
        Assert.Equal(0, WavePlayback.FilePosition(2.2, 8, 240, 30)!.Value, 9);
        Assert.Equal(1, WavePlayback.FilePosition(3.2, 8, 240, 30)!.Value, 9);
    }

    [Fact]
    public void PastTheEndOfTheFile_IsSilent()
    {
        Assert.Equal(30, WavePlayback.FilePosition(30.2, 0, Bpm, 30)!.Value, 9);
        Assert.Null(WavePlayback.FilePosition(30.3, 0, Bpm, 30));
    }

    [Fact]
    public void NothingToPlay_IsSilentRatherThanThrowing()
    {
        // A clip whose file is missing keeps its saved length; one that never had a length,
        // and a project with a nonsense tempo, must not divide by zero into a seek.
        Assert.Null(WavePlayback.FilePosition(5, 0, Bpm, 0));
        Assert.Null(WavePlayback.FilePosition(5, 0, 0, 30));
    }
}
