using Serilog.Core;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Voices;

namespace ThirtyDollarConverter.Tests;

/// <summary>
///     The voice mixer against the legacy track renderer it replaces, and against itself: an
///     offline mix and a streamed one must produce the same floats however they are sliced.
/// </summary>
public class VoiceMixerTests
{
    private const uint SampleRate = 48000;

    private static readonly string[] Songs =
    [
        "a|b|c",
        "a|!cut|b",
        "a|!combine|c|!cut@a|b|c",
        "a|!combine|!cut|!combine|b|c",
        "a^-60|a^40|b^-30|b^70|c^100|b^-100",
        "a>0.05|b>0.1|c>1|b>0.02^-40",
        "a%150|b%250|c%40|a@7%300",
        "!speed@600|a|b@-5|!cut@b|c|!cut|a|!combine|b|!cut@b,a|c"
    ];

    private readonly SampleHolder _holder = new(Logger.None);

    public VoiceMixerTests()
    {
        Add("a", Tone(440, 0.3), Tone(440, 0.3), 1);
        Add("b", Tone(330, 0.3), Tone(550, 0.3), 2);
        Add("c", Tone(660, 0.5), Tone(660, 0.5), 1);

        // 0.1 s of tone, 0.15 s of silence - longer than the 4096 silent samples the legacy
        // cut stops zeroing at - then 0.1 s of tone again
        var gap = Concat(Tone(220, 0.1), new float[(int)(SampleRate * 0.15)], Tone(220, 0.1));
        Add("gap", gap, gap, 1);
    }

    private void Add(string id, float[] left, float[] right, uint sourceChannels)
    {
        var data = new AudioData<float>(2) { Samples = [left, right] };
        var sound = new Sound { Id = id };
        _holder.SampleList.Add(sound, new PcmDataHolder
            { FloatData = data, SampleRate = SampleRate, Channels = sourceChannels });
        _holder.StringToSoundReferences.Add(id, sound);
    }

    private static float[] Tone(double frequency, double seconds)
    {
        var samples = new float[(int)(SampleRate * seconds)];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / SampleRate));
        return samples;
    }

    private static float[] Concat(params float[][] parts)
    {
        return [.. parts.SelectMany(part => part)];
    }

    private PcmEncoder Encoder(RenderMethod renderer)
    {
        return new PcmEncoder(_holder,
            new EncoderSettings { SampleRate = SampleRate, Channels = 2, Renderer = renderer });
    }

    private async Task<AudioData<float>> Render(string song, RenderMethod renderer)
    {
        return (await Encoder(renderer).GetSequenceAudio(Sequence.FromString(song))).Audio;
    }

    private async Task<VoiceRender> Voices(string song)
    {
        return await Encoder(RenderMethod.Voice).BuildVoices([Sequence.FromString(song)]);
    }

    [Theory]
    [MemberData(nameof(SongData))]
    public async Task VoiceMatchesLegacy(string song)
    {
        var legacy = await Render(song, RenderMethod.LegacyTracks);
        var voice = await Render(song, RenderMethod.Voice);

        Assert.Equal(legacy.ChannelCount, voice.ChannelCount);
        Assert.Equal(legacy.GetLength(), voice.GetLength());

        var peak = 0f;
        for (var channel = 0; channel < legacy.ChannelCount; channel++)
        for (var i = 0; i < legacy.GetLength(); i++)
            peak = Math.Max(peak, Math.Abs(legacy.Samples[channel][i] - voice.Samples[channel][i]));

        Assert.True(peak < 1e-4f, $"peak difference {peak}");
        Assert.Contains(voice.Samples[0], sample => sample != 0);
    }

    public static TheoryData<string> SongData()
    {
        return [.. Songs];
    }

    [Fact]
    public async Task PanReachesBothChannelsDifferently()
    {
        var audio = await Render("b^-70|a^60", RenderMethod.Voice);
        Assert.NotEqual(audio.Samples[0], audio.Samples[1]);
    }

    [Theory]
    [MemberData(nameof(SongData))]
    public async Task MixAll_IsTheSameForEverySliceLength(string song)
    {
        using var render = await Voices(song);
        var reference = VoiceMixer.MixAll(render.Voices);

        foreach (var slice in new[] { 333, 1024, render.Voices.Length })
        {
            var mixed = VoiceMixer.MixAll(render.Voices, Math.Max(slice, 1));
            Assert.Equal(reference.Samples[0], mixed.Samples[0]);
            Assert.Equal(reference.Samples[1], mixed.Samples[1]);
        }
    }

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public async Task Streaming_IsBitIdenticalToMixAll_ThroughSeeks(int block)
    {
        using var render = await Voices(Songs[^1]);
        var list = render.Voices;
        var reference = VoiceMixer.MixAll(list);

        using var cursor = new VoiceCursor();
        cursor.Attach(list, 0);
        var random = new Random(block);
        var left = new float[block];
        var right = new float[block];

        for (var step = 0; step < 400; step++)
        {
            if (step % 25 == 24) cursor.Seek(random.Next(list.Length));

            var from = cursor.Position;
            cursor.Render(left, right);
            for (var i = 0; i < block && from + i < list.Length; i++)
            {
                Assert.Equal(reference.Samples[0][from + i], left[i]);
                Assert.Equal(reference.Samples[1][from + i], right[i]);
            }

            if (cursor.Position >= list.Length) cursor.Seek(0);
        }
    }

    /// <summary>
    ///     Behavior change, on purpose: the legacy cut zeroes a track only until it finds 4096
    ///     silent samples, so a sound with that long a pause in it comes back after a cut. A cut
    ///     voice stays stopped, as on the site.
    /// </summary>
    [Fact]
    public async Task ACutSoundStaysStopped_PastItsOwnSilence()
    {
        const string song = "!speed@1200|gap|!cut";
        var legacy = await Render(song, RenderMethod.LegacyTracks);
        var voice = await Render(song, RenderMethod.Voice);

        var start = Array.FindIndex(voice.Samples[0], sample => sample != 0);
        var second_tone = start + (int)(SampleRate * 0.25);
        var range = (int)(SampleRate * 0.1);

        Assert.Contains(legacy.Samples[0].Skip(second_tone).Take(range), sample => sample != 0);
        Assert.DoesNotContain(voice.Samples[0].Skip(second_tone).Take(range), sample => sample != 0);
    }

    /// <summary>
    ///     A legacy bug the voice mixer fixes: several cuts of one track inside one fade window (a
    ///     chord written as "!cut@x" before each of its notes, or the same "#icut" twice) multiply
    ///     the legacy track by every ramp, so a sound fades faster the more cuts land on it. A voice
    ///     fades once. The renders differ only inside that window.
    /// </summary>
    [Fact]
    public async Task StackedCuts_DifferOnlyInsideTheFadeWindow()
    {
        const string song = "a|!cut@a|!combine|a|!combine|!cut@a|!combine|a|!combine|!cut@a|!combine|b";
        var legacy = await Render(song, RenderMethod.LegacyTracks);
        var voice = await Render(song, RenderMethod.Voice);

        using var render = await Voices(song);
        var cut = (int)render.TimedEvents.Placement.First(p => p.Event.SoundEvent == "!cut").Index;
        var fade = render.Voices.FadeLength;

        var inside = 0f;
        for (var channel = 0; channel < 2; channel++)
        for (var i = 0; i < legacy.GetLength(); i++)
        {
            var delta = Math.Abs(legacy.Samples[channel][i] - voice.Samples[channel][i]);
            if (i >= cut && i < cut + fade) inside = Math.Max(inside, delta);
            else Assert.True(delta < 1e-4f, $"sample {i}: {delta}");
        }

        Assert.True(inside > 1e-3f, "the stacked cuts should fade the voices differently");
    }

    [Fact]
    public async Task CursorRenderSeekAndAttach_AllocateNothing()
    {
        using var first = await Voices(Songs[^1]);
        using var second = await Voices(Songs[^2]);

        using var cursor = new VoiceCursor();
        var left = new float[1024];
        var right = new float[1024];

        void Run()
        {
            for (var i = 0; i < 200; i++)
            {
                if (i % 50 == 10) cursor.Seek(i * 97);
                if (i % 50 == 30) cursor.Attach(cursor.List == first.Voices ? second.Voices : first.Voices, i * 31);
                cursor.Render(left, right);
            }
        }

        cursor.Attach(first.Voices, 0);
        Run(); // warm-up: the cursor rents its active set once per list size

        var before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public async Task ARetainedListOutlivesItsFirstOwner()
    {
        var render = await Voices(Songs[0]);
        var list = render.Voices;
        list.Retain();
        render.Dispose();

        Assert.True(list.Count > 0);
        Assert.NotEqual(0, VoiceMixer.MixAll(list).Samples[0].Max());
        list.Dispose();
    }
}
