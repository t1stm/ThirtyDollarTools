using Serilog.Core;
using Shared.Audio;
using Shared.Audio.Gpu;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Voices;

namespace Shared.Tests;

/// <summary>
///     The GPU cursor's slot ring, with a fake GPU that mixes on the CPU: whatever mixes a block
///     - the GPU on time, the CPU taking a late one over, or the CPU alone - the feeder puts the
///     offline mix bit for bit, a seek or swap never lets a stale block through, and steady state
///     allocates nothing.
/// </summary>
public class GpuVoiceCursorTests
{
    private const int SampleRate = 48000;
    private const string First = "a|b|!cut@a|a|b@4|!cut|a";
    private const string Second = "b|a|a@3|!cut@a|b";

    private readonly PcmEncoder _encoder;
    private readonly float[] _left = new float[VoicePlayback.MaxBlockFrames];
    private readonly float[] _right = new float[VoicePlayback.MaxBlockFrames];

    public GpuVoiceCursorTests()
    {
        var holder = new SampleHolder(Logger.None);
        Add(holder, "a", 440, 0.3);
        Add(holder, "b", 330, 0.5);
        _encoder = new PcmEncoder(holder, new EncoderSettings { SampleRate = SampleRate, Channels = 2 });
    }

    private static void Add(SampleHolder holder, string id, double frequency, double seconds)
    {
        var samples = new float[(int)(SampleRate * seconds)];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / SampleRate));

        var sound = new Sound { Id = id };
        holder.SampleList.Add(sound, new PcmDataHolder
        {
            FloatData = new AudioData<float>(2) { Samples = [samples, samples] }, SampleRate = SampleRate, Channels = 1
        });
        holder.StringToSoundReferences.Add(id, sound);
    }

    private VoiceRender Build(string song)
    {
        return _encoder.BuildVoices([Sequence.FromString(song)]).GetAwaiter().GetResult();
    }

    /// <summary>The fake GPU: mixes up to <paramref name="blocks" /> requested blocks on the CPU into their slots.</summary>
    private void RunGpu(GpuVoiceCursor cursor, int blocks = int.MaxValue)
    {
        while (blocks-- > 0 && cursor.TakeNext() is { } slot)
        {
            if (!GpuVoiceCursor.IsAbandoned(slot))
            {
                var left = _left.AsSpan(0, slot.Frames);
                var right = _right.AsSpan(0, slot.Frames);
                slot.List!.MixBlock(slot.Active.AsSpan(0, slot.Count), slot.Start, left, right);
                for (var i = 0; i < slot.Frames; i++)
                {
                    slot.Result[2 * i] = left[i];
                    slot.Result[2 * i + 1] = right[i];
                }
            }

            cursor.Complete(slot);
        }
    }

    /// <summary>Feeds the whole rest of the song as a player would, the fake GPU - if any - running between blocks.</summary>
    private float[] Drain(GpuVoiceCursor cursor, int block, bool gpu = true)
    {
        var list = cursor.List!;
        var output = new float[2 * (list.Length - cursor.Position)];
        var left = new float[block];
        var right = new float[block];
        var at = 0;

        while (cursor.Position < list.Length)
        {
            var frames = Math.Min(block, list.Length - cursor.Position);
            // never forced: the block comes once the GPU has run
            var tries = 0;
            while (!cursor.TryRender(left.AsSpan(0, frames), right.AsSpan(0, frames), false))
            {
                Assert.True(gpu && ++tries < 3, "the GPU ran and the block still isn't back");
                RunGpu(cursor);
            }

            for (var i = 0; i < frames; i++, at += 2)
            {
                output[at] = left[i];
                output[at + 1] = right[i];
            }
        }

        return output;
    }

    [Theory]
    [InlineData(VoicePlayback.DefaultBlockFrames)]
    [InlineData(333)]
    public void TheGpuOnTime_PutsTheOfflineMix(int block)
    {
        using var render = Build(First);
        using var cursor = new GpuVoiceCursor();
        cursor.Attach(render.Voices, 0);

        Assert.Equal(Interleave(VoiceMixer.MixAll(render.Voices)), Drain(cursor, block));
        Assert.Equal(0, cursor.Takeovers);
        cursor.Attach(null, 0);
    }

    [Fact]
    public void ALateBlock_WaitsUntilForced_ThenTheCpuMixesIt()
    {
        using var render = Build(First);
        using var cursor = new GpuVoiceCursor();
        cursor.Attach(render.Voices, 0);
        var left = new float[1024];
        var right = new float[1024];

        Assert.False(cursor.TryRender(left, right, false));
        Assert.Equal(0, cursor.Position);

        Assert.True(cursor.TryRender(left, right, true)); // a cheap block: mixed at once
        Assert.Equal(1, cursor.Takeovers);
        Assert.Equal(Interleave(VoiceMixer.MixAll(render.Voices))[..2048], Interleave(left, right));

        // the GPU never sees the block the CPU took; the next one comes from the GPU
        RunGpu(cursor);
        Assert.True(cursor.TryRender(left, right, false));
        Assert.Equal(Interleave(VoiceMixer.MixAll(render.Voices))[2048..4096], Interleave(left, right));
        cursor.Attach(null, 0);
    }

    [Theory]
    [InlineData(VoicePlayback.DefaultBlockFrames)]
    [InlineData(333)]
    public void TheCpuAlone_PutsTheOfflineMix_ThroughSeeksAndSwaps(int block)
    {
        using var first = Build(First);
        using var second = Build(Second);
        var stream = new CaptureStream();
        using var playback = new VoicePlayback(stream, SampleRate, 1, false, new GpuVoiceCursor());
        playback.BlockFrames = block;

        // nothing pumps the GPU: every block is forced, as the capture never fills
        first.Voices.Retain();
        playback.Publish(first.Voices);
        playback.Pump();
        Assert.Equal(Interleave(VoiceMixer.MixAll(first.Voices)), stream.Take());

        playback.SeekTime_Milliseconds(350);
        playback.Pump();
        Assert.Equal(Interleave(VoiceMixer.MixAll(first.Voices))[(2 * 350 * 48)..], stream.Take());

        stream.Played = 1000;
        second.Voices.Retain();
        playback.Publish(second.Voices);
        playback.Pump();
        Assert.Equal(Interleave(VoiceMixer.MixAll(second.Voices))[(2 * (350 * 48 + 1000))..], stream.Take());
    }

    [Fact]
    public void ASeekOrSwap_DropsBlocksStillOnTheGpu()
    {
        using var first = Build(First);
        using var second = Build(Second);
        using var cursor = new GpuVoiceCursor();
        cursor.Attach(first.Voices, 0);
        var left = new float[1024];
        var right = new float[1024];

        // blocks requested and one taken by the GPU, then a seek: the taken one is abandoned to it
        Assert.False(cursor.TryRender(left, right, false));
        var taken = cursor.TakeNext()!;
        cursor.Seek(7000);
        Assert.True(GpuVoiceCursor.IsAbandoned(taken));
        Assert.Null(cursor.TakeNext()); // nothing else is left queued
        cursor.Complete(taken); // the GPU finishing the abandoned block frees it

        Assert.Equal(Interleave(VoiceMixer.MixAll(first.Voices))[(2 * 7000)..], Drain(cursor, 1024));

        // a swap mid-song, with blocks of the old list done and waiting
        cursor.Seek(0);
        Assert.False(cursor.TryRender(left, right, false));
        RunGpu(cursor);
        Assert.Same(first.Voices, cursor.Attach(second.Voices, 5000));

        Assert.Equal(Interleave(VoiceMixer.MixAll(second.Voices))[(2 * 5000)..],
            Drain(cursor, 1024));
        cursor.Attach(null, 0);
    }

    [Fact]
    public void SteadyState_AllocatesNothing()
    {
        using var first = Build(First);
        using var second = Build(Second);
        using var cursor = new GpuVoiceCursor();
        var left = new float[512];
        var right = new float[512];

        void Run()
        {
            for (var i = 0; i < 400; i++)
            {
                if (i % 40 == 13) cursor.Seek(i * 37 % 20000);
                if (i % 100 == 50) cursor.Attach(cursor.List == first.Voices ? second.Voices : first.Voices, i * 11);
                if (i % 3 != 0) RunGpu(cursor, 2);
                if (cursor.Position + 512 > cursor.List!.Length) cursor.Seek(0);
                cursor.TryRender(left, right, i % 5 == 0);
            }
        }

        cursor.Attach(first.Voices, 0);
        Run(); // warm-up: the slots rent their snapshots and size their results

        var before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        cursor.Attach(null, 0);
    }

    [Fact]
    public void SwitchedOff_TheCpuMixes_AndOnAgainTheGpuDoes()
    {
        using var render = Build(First);
        using var cursor = new GpuVoiceCursor();
        cursor.Attach(render.Voices, 0);
        var reference = Interleave(VoiceMixer.MixAll(render.Voices));
        var left = new float[1024];
        var right = new float[1024];

        Assert.False(cursor.TryRender(left, right, false));
        var taken = cursor.TakeNext()!;

        // off: the next block comes from the CPU unforced, and the block on the GPU is dropped
        cursor.Enabled = false;
        Assert.True(cursor.TryRender(left, right, false));
        Assert.Equal(reference[..2048], Interleave(left, right));
        Assert.True(GpuVoiceCursor.IsAbandoned(taken));
        cursor.Complete(taken);
        Assert.Equal(0, cursor.Takeovers); // the user's choice isn't the GPU failing

        cursor.Enabled = true;
        Assert.Equal(reference[2048..], Drain(cursor, 1024));
        cursor.Attach(null, 0);
    }

    [Fact]
    public void AfterAFailure_TheCpuMixesEverything()
    {
        using var render = Build(First);
        using var cursor = new GpuVoiceCursor();
        cursor.Attach(render.Voices, 0);
        var left = new float[1024];
        var right = new float[1024];

        Assert.False(cursor.TryRender(left, right, false));
        cursor.Fail();

        Assert.Equal(Interleave(VoiceMixer.MixAll(render.Voices)), Drain(cursor, 1024, false));
        cursor.Attach(null, 0);
    }

    private static float[] Interleave(float[] left, float[] right)
    {
        var interleaved = new float[2 * left.Length];
        for (var i = 0; i < left.Length; i++)
        {
            interleaved[2 * i] = left[i];
            interleaved[2 * i + 1] = right[i];
        }

        return interleaved;
    }

    private static float[] Interleave(AudioData<float> audio)
    {
        return Interleave(audio.Samples[0], audio.Samples[1]);
    }

    private sealed class CaptureStream : AudioStream
    {
        private readonly List<float> _put = [];
        private long _base;
        public long Played { get; set; }

        public override long QueuedFrames => 0;
        public override long PlayedPosition => _base + Played;

        public float[] Take()
        {
            var taken = _put.ToArray();
            _put.Clear();
            return taken;
        }

        public override void Put(ReadOnlySpan<float> interleaved)
        {
            _put.AddRange(interleaved);
        }

        public override void Flush(long basePosition)
        {
            _put.Clear();
            _base = basePosition;
            Played = 0;
        }

        public override void Play()
        {
        }

        public override void SetPause(bool paused)
        {
        }

        public override void SetVolume(float volume)
        {
        }

        public override void Delete()
        {
        }
    }
}
