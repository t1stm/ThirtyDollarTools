using Serilog.Core;
using Shared.Audio.Null;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Encoder.Wave;

namespace EditorScene.Tests;

/// <summary>
///     <see cref="WavePlayback" /> driven over a real file with the null audio context, which
///     hands out silent buffers that still keep time. Covers the parts the arithmetic tests
///     can't: decoding an actual RIFF file, the clip a placement gets, and what mute, tempo and
///     removal do to it.
/// </summary>
public class WavePlaybackSyncTests : IDisposable
{
    private const uint SampleRate = 8000;
    private const double Seconds = 4;

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"wave-playback-{Guid.NewGuid():N}.wav");
    private readonly List<string> _errors = [];

    public WavePlaybackSyncTests()
    {
        // A flat, quiet tone - the content is irrelevant, the header and the length are not.
        var data = AudioData<float>.WithLength(2, (int)(SampleRate * Seconds));
        for (var channel = 0; channel < 2; channel++)
        for (var i = 0; i < data.GetLength(); i++)
            data.Samples[channel][i] = 0.1f * MathF.Sin(i / 40f);

        WaveEncoder.WriteAsWavFloat32File(_path, data, 2, SampleRate);
    }

    public void Dispose()
    {
        File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    private WavePlayback NewPlayback()
    {
        return new WavePlayback(new NullAudioContext(), Logger.None, _errors.Add);
    }

    private (ThirtyDollarProject Project, TrackPlacement Placement) ProjectWithClip(double startQuarters = 0)
    {
        var project = new ThirtyDollarProject { RootTiming = { BPM = 120 } };
        var track = (WaveTrack)project.NewTrack(TrackKind.Wave);
        track.Path = _path;
        track.DurationSeconds = Seconds;
        return (project, project.Place(track, 0, startQuarters));
    }

    /// <summary>Decodes the file and hands the result to the sync, as the editor's frame loop would.</summary>
    private WavePlayback Ready(ThirtyDollarProject project)
    {
        var playback = NewPlayback();
        Assert.Equal(Seconds, playback.Prepare(_path).GetAwaiter().GetResult()!.Value, 3);
        playback.Sync(project, _ => true, 0, false, 1f);
        return playback;
    }

    [Fact]
    public void APlacedClip_GetsABuffer_AndKeepsIt()
    {
        var (project, placement) = ProjectWithClip();
        var playback = Ready(project);

        Assert.Equal(1, playback.ClipCount);
        playback.Sync(project, _ => true, 1, true, 1f);
        Assert.Equal(1, playback.ClipCount); // the same clip, not a second buffer per frame
        Assert.Empty(_errors);

        playback.Dispose();
    }

    [Fact]
    public void TheClipFollowsTheTransport_OffsetByTheLeadIn()
    {
        var (project, placement) = ProjectWithClip();
        var playback = Ready(project);

        playback.Sync(project, _ => true, EditorPlayback.LeadInSeconds + 2, true, 1f);
        Assert.True(playback.IsSounding(placement));
        // The null buffer's clock runs on, so this is "about where it was seeked", not an exact
        // sample - what matters is that it landed 2 s in rather than at 2.2 s or at 0.
        Assert.Equal(2, playback.PositionSeconds(placement)!.Value, 1);

        playback.Dispose();
    }

    [Fact]
    public void TwoClipsOfOneFile_KeepTheirOwnPlace()
    {
        // The second clip starts 2 s (4 quarters at 120 BPM) after the first.
        var (project, first) = ProjectWithClip();
        var second = project.Place(first.Track, 1, 4);
        var playback = Ready(project);

        playback.Sync(project, _ => true, EditorPlayback.LeadInSeconds + 3, true, 1f);
        Assert.Equal(2, playback.ClipCount);
        Assert.Equal(3, playback.PositionSeconds(first)!.Value, 1);
        Assert.Equal(1, playback.PositionSeconds(second)!.Value, 1);

        // Muting one lane pauses only its own clip.
        playback.Sync(project, channel => channel != 1, EditorPlayback.LeadInSeconds + 3.1, true, 1f);
        Assert.True(playback.IsSounding(first));
        Assert.False(playback.IsSounding(second));

        playback.Dispose();
    }

    [Fact]
    public void OutsideItsOwnSpan_TheClipIsSilent()
    {
        var (project, placement) = ProjectWithClip(8); // 8 quarters at 120 BPM = 4 s in
        var playback = Ready(project);

        playback.Sync(project, _ => true, 1, true, 1f);
        Assert.False(playback.IsSounding(placement));

        playback.Sync(project, _ => true, 4.2 + 1, true, 1f);
        Assert.True(playback.IsSounding(placement));

        playback.Sync(project, _ => true, 4.2 + Seconds + 0.5, true, 1f);
        Assert.False(playback.IsSounding(placement));

        playback.Dispose();
    }

    [Fact]
    public void MutingTheChannel_AndStopping_SilenceTheClip()
    {
        var (project, placement) = ProjectWithClip();
        var playback = Ready(project);
        var playing = EditorPlayback.LeadInSeconds + 1;

        playback.Sync(project, _ => true, playing, true, 1f);
        Assert.True(playback.IsSounding(placement));

        playback.Sync(project, _ => false, playing, true, 1f); // muted, or another channel soloed
        Assert.False(playback.IsSounding(placement));

        playback.Sync(project, _ => true, playing, true, 1f);
        playback.StopAll();
        Assert.False(playback.IsSounding(placement));

        playback.Dispose();
    }

    [Fact]
    public async Task A16BitMonoFile_IsTimedAndDrawnFromItsOwnSamples()
    {
        // Two seconds of mono 16-bit: quiet, then loud. Nothing is widened on the way in, so
        // the length has to come from 2-byte single-channel frames and the peaks from shorts.
        var path = Path.Combine(Path.GetTempPath(), $"wave-int16-{Guid.NewGuid():N}.wav");
        var samples = Enumerable.Range(0, (int)SampleRate * 2).Select(i => (short)(i < SampleRate ? 1000 : -20000))
            .ToArray();
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + samples.Length * 2);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1); // integer PCM
            writer.Write((short)1); // mono
            writer.Write((int)SampleRate);
            writer.Write((int)SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(samples.Length * 2);
            foreach (var sample in samples) writer.Write(sample);
        }

        try
        {
            var playback = NewPlayback();
            Assert.Equal(2, (await playback.Prepare(path))!.Value, 3);
            playback.Sync(new ThirtyDollarProject(), _ => true, 0, false, 1f);

            var peaks = playback.Peaks(path)!;
            Assert.Equal(1000 / 20000f, peaks[0], 3);
            Assert.Equal(1, peaks[^1], 3);
            Assert.Empty(_errors);
            playback.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AFileWithNoAudio_IsAFailure_NotAZeroSecondReference()
    {
        // A header the decoder cannot follow leaves an empty buffer behind. Reporting that as
        // a 0 s file would add a sliver of a clip that plays nothing and says nothing about why.
        var path = Path.Combine(Path.GetTempPath(), $"wave-empty-{Guid.NewGuid():N}.wav");
        File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes("RIFF\u0004\u0000\u0000\u0000WAVE"));
        try
        {
            var playback = NewPlayback();

            Assert.Null(await playback.Prepare(path));
            Assert.Single(_errors);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RemovingThePlacement_DropsItsBuffer()
    {
        var (project, placement) = ProjectWithClip();
        var playback = Ready(project);
        Assert.Equal(1, playback.ClipCount);

        project.RemovePlacement(placement);
        playback.Sync(project, _ => true, 0, false, 1f);

        Assert.Equal(0, playback.ClipCount);
        playback.Dispose();
    }

    [Fact]
    public async Task AMissingFile_IsReportedOnce_AndPlaysNothing()
    {
        var project = new ThirtyDollarProject();
        var track = (WaveTrack)project.NewTrack(TrackKind.Wave);
        track.Path = Path.Combine(Path.GetTempPath(), "no-such-reference.wav");
        track.DurationSeconds = 3;
        project.Place(track, 0, 0);

        var playback = NewPlayback();
        Assert.Null(await playback.Prepare(track.Path));

        for (var frame = 0; frame < 5; frame++) playback.Sync(project, _ => true, frame, true, 1f);

        Assert.Equal(0, playback.ClipCount);
        Assert.Single(_errors); // one failure, one dialog - not one per frame
        playback.Dispose();
    }
}
