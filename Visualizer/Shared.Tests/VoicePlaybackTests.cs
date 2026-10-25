using System.Diagnostics;
using Serilog.Core;
using Shared.Audio;
using Shared.Audio.Null;
using Shared.Objects;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Voices;

namespace Shared.Tests;

/// <summary>
///     The streaming player: what the feeder puts is the offline mix, it allocates nothing once
///     warm, and its clock is the stream's.
/// </summary>
public class VoicePlaybackTests
{
    private const int SampleRate = 48000;

    private readonly PcmEncoder _encoder;

    public VoicePlaybackTests()
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

    [Fact]
    public void PutsTheOfflineMix_ThroughSeeksAndSwaps()
    {
        using var first = Build("a|b|!cut@a|a|b@4|!cut|a");
        using var second = Build("b|a|a@3|!cut@a|b");
        var stream = new CaptureStream();
        using var playback = new VoicePlayback(stream, SampleRate, 1, false);

        first.Voices.Retain();
        playback.Publish(first.Voices);
        Pump(playback);
        Assert.Equal(Interleave(VoiceMixer.MixAll(first.Voices)), stream.Take());
        Assert.InRange(playback.Load, float.Epsilon, 1f); // a whole song mixed, far faster than it plays

        playback.SeekTime_Milliseconds(350);
        Pump(playback);
        Assert.Equal(Interleave(VoiceMixer.MixAll(first.Voices))[(2 * 350 * 48)..], stream.Take());
        Assert.Equal(350 * 48, stream.Base);

        // a new list takes over at the playhead, which the capture says is where it was flushed to
        stream.Played = 1000;
        second.Voices.Retain();
        playback.Publish(second.Voices);
        Pump(playback);
        Assert.Equal(Interleave(VoiceMixer.MixAll(second.Voices))[(2 * (350 * 48 + 1000))..], stream.Take());
    }

    [Fact]
    public void PumpSeekAndSwap_AllocateNothing()
    {
        using var first = Build("a|b|!cut@a|a|b@4|!cut|a");
        using var second = Build("b|a|a@3|!cut@a|b");
        using var playback = new VoicePlayback(new NullStream(SampleRate, 2), SampleRate, 0.05, false);

        var left = new float[VoicePlayback.Block];
        var right = new float[VoicePlayback.Block];
        var interleaved = new float[2 * VoicePlayback.Block];

        void Run()
        {
            for (var i = 0; i < 300; i++)
            {
                if (i % 3 == 0) playback.SeekTime_Milliseconds(i % 700);
                if (i % 50 == 25)
                {
                    var next = i % 100 == 25 ? second.Voices : first.Voices;
                    next.Retain();
                    playback.Publish(next);
                }

                playback.Pump(left, right, interleaved);
            }
        }

        first.Voices.Retain();
        playback.Publish(first.Voices);
        Run(); // warm-up: the cursor rents its active set, the lock and pool settle

        var before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void TheClockRunsWithTheStreamAndStopsAtTheEnd()
    {
        using var render = Build("a|b");
        using var playback = new VoicePlayback(new NullAudioContext(), SampleRate, 2);

        render.Voices.Retain();
        playback.Publish(render.Voices);
        playback.Play();
        Assert.True(playback.IsRunning);

        var length = render.Voices.Length * 1000L / SampleRate;
        var watch = Stopwatch.StartNew();
        while (playback.GetTime_Milliseconds() < length && watch.ElapsedMilliseconds < 5000) Thread.Yield();

        Thread.Sleep(50);
        Assert.Equal(length, playback.GetTime_Milliseconds());

        // a seek reads back at once, before the feeder has flushed to it
        playback.SeekTime_Milliseconds(120);
        Assert.Equal(120, playback.GetTime_Milliseconds());
    }

    /// <summary>A render-ahead setting moved mid-song reaches the playing stream, from whoever uploaded it.</summary>
    [Fact]
    public void RefreshAheadSeconds_RereadsTheUploadersValue()
    {
        using var render = Build("a|b");
        var player = new SequencePlayer(Logger.None, new NullAudioContext());
        var ahead = 2.0;

        render.Voices.Retain();
        player.UpdateSequence(render.TimedEvents, new SequenceIndices(), render.Voices, () => ahead);
        var playback = (VoicePlayback)((TimingStopwatchWrapper)player.GetTimingStopwatch()).AudibleBuffer!;
        Assert.Equal(2, playback.AheadSeconds, 3);

        ahead = 5;
        player.RefreshAheadSeconds();
        Assert.Equal(5, playback.AheadSeconds, 3);

        player.Die();
    }

    private static void Pump(VoicePlayback playback)
    {
        playback.Pump(new float[VoicePlayback.Block], new float[VoicePlayback.Block],
            new float[2 * VoicePlayback.Block]);
    }

    private static float[] Interleave(AudioData<float> audio)
    {
        var interleaved = new float[audio.GetLength() * 2];
        for (var i = 0; i < audio.GetLength(); i++)
        {
            interleaved[2 * i] = audio.Samples[0][i];
            interleaved[2 * i + 1] = audio.Samples[1][i];
        }

        return interleaved;
    }

    /// <summary>Keeps what it is put; never fills up, so one pump writes out the rest of the song.</summary>
    private sealed class CaptureStream : AudioStream
    {
        private readonly List<float> _put = [];
        public long Base { get; private set; }
        public long Played { get; set; }

        public override long QueuedFrames => 0;
        public override long PlayedPosition => Base + Played;

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
            Base = basePosition;
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
