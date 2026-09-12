using Serilog.Core;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     Auto offset is the seamless sustain: every retrigger starts the sound where the
///     instance it replaces had reached, so the rendered waveform is continuous across the
///     splice. The spec is exactly that - a sustained note has to render like the plain
///     note it is made of, everywhere except the cut's fade window.
/// </summary>
public class AutoOffsetTests
{
    private const uint SampleRate = 48000;

    // 120 BPM at 4 steps a beat = 480 steps a minute = 0.125 s = 6000 samples a step.
    private const int SamplesPerStep = 6000;

    private readonly PcmEncoder _encoder;

    public AutoOffsetTests()
    {
        var holder = new SampleHolder(Logger.None);
        holder.SampleList.Add(new Sound { Id = "tone" }, Sine(440, 2f));
        _encoder = new PcmEncoder(holder, new EncoderSettings { SampleRate = SampleRate, Channels = 2 });
    }

    private static PcmDataHolder Sine(float frequency, float seconds)
    {
        var count = (int)(SampleRate * seconds);
        var data = AudioData<float>.WithLength(2, count);
        for (var i = 0; i < count; i++)
        {
            var sample = (float)Math.Sin(2 * Math.PI * frequency * i / SampleRate);
            data.Samples[0][i] = sample;
            data.Samples[1][i] = sample;
        }

        return new PcmDataHolder { FloatData = data, SampleRate = SampleRate, Channels = 2 };
    }

    private async Task<float[]> Render(double value, AudioKeyframeManager? automation)
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var instrument = project.NewInstrument("tone");
        instrument.AddSound("tone");
        track.Segments[0].Notes.Add(new Note
            { Step = 0, Instrument = instrument, Value = value, Automation = automation });
        project.Place(track, 0, 0);

        var rendered = await _encoder.GetSequenceAudio(project.ToSequence());
        return rendered.Audio.Samples[0];
    }

    /// <summary>
    ///     At value 0 the sound plays at its own rate, at 12 it plays twice as fast and eats
    ///     twice as much of itself per second - the two pin the exponent in the per-sound
    ///     scaling, which a plain seconds offset would get wrong at one of them.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public async Task AutoOffsetSustain_RendersLikeTheUnretriggeredSound(double value)
    {
        var sustain = new AudioKeyframeManager
        {
            CutAtEnd = false, // the plain note has nothing to cut it either
            Gap = 1,
            Template = new AudioKeyframe { AutoOffset = true },
            End = 8
        };

        var sustained = await Render(value, sustain);
        var plain = await Render(value, null);

        // The render has a lead-in before the first note; every retrigger is one step later.
        var noteStart = Array.FindIndex(plain, sample => sample != 0);
        Assert.True(noteStart > 0);

        // The cut fades the old instance out over CutFadeLengthMs (4 ms = 192 samples) while
        // the new one starts at full level, so the splice is only equal outside that window.
        const int fade = 192 + 8;
        var compared = 0;
        for (var i = 0; i < Math.Min(sustained.Length, plain.Length); i++)
        {
            var sinceStart = i - noteStart;
            var stepPosition = sinceStart % SamplesPerStep;
            if (sinceStart >= SamplesPerStep && sinceStart < 8 * SamplesPerStep &&
                (stepPosition < fade || stepPosition > SamplesPerStep - 8))
                continue;

            Assert.True(Math.Abs(sustained[i] - plain[i]) < 1e-4f,
                $"sample {i} ({sinceStart} into the note): {sustained[i]} vs {plain[i]}");
            compared++;
        }

        Assert.True(compared > 40000, $"only {compared} samples compared");
    }
}
