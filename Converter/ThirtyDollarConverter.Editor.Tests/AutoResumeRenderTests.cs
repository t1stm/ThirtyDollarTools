using Serilog.Core;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     The spec for auto-resume, the same shape <see cref="AutoOffsetTests" /> uses for the
///     sustain it is built on: a long note crossed by another note's retrigger guards has to
///     render like the note nothing ever cut, everywhere except the cut's own fade window.
/// </summary>
public class AutoResumeRenderTests
{
    private const uint SampleRate = 48000;

    // 120 BPM at 4 steps a beat = 480 steps a minute = 0.125 s = 6000 samples a step.
    private const int SamplesPerStep = 6000;

    private readonly PcmEncoder _encoder;

    public AutoResumeRenderTests()
    {
        var holder = new SampleHolder(Logger.None);
        holder.SampleList.Add(new Sound { Id = "tone" }, Sine(50, 2f));
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

    /// <summary>
    ///     A note that declares a length and nothing else: no gap, so no retriggers and no
    ///     cuts of its own - it rings exactly like a plain note, and everything that happens
    ///     to it in the render is somebody else's doing.
    /// </summary>
    private static AudioKeyframeManager Held(float end)
    {
        return new AudioKeyframeManager
        {
            Cut = false,
            CutAtEnd = false,
            Template = new AudioKeyframe { AutoOffset = true },
            Gap = 0,
            End = end
        };
    }

    /// <summary>
    ///     The interfering voice: same sound, so its cuts silence the held note, and silent
    ///     itself so the two renders differ only in what happened to the held note.
    /// </summary>
    private static AudioKeyframeManager Interferer()
    {
        return new AudioKeyframeManager
        {
            Template = new AudioKeyframe { AutoOffset = true },
            Gap = 1,
            End = 6
        };
    }

    private async Task<float[]> Render(bool interfering, bool autoResume = true)
    {
        var project = new ThirtyDollarProject { AutoResume = autoResume };
        var track = project.NewTrack();
        var tone = project.NewInstrument("tone");
        tone.AddSound("tone");

        track.Segments[0].Notes.Add(new Note { Step = 0, Instrument = tone, Automation = Held(8) });
        if (interfering)
            track.Segments[0].Notes.Add(new Note
                { Step = 1, Instrument = tone, Volume = 0, Automation = Interferer() });

        project.Place(track, 0, 0);
        var rendered = await _encoder.GetSequenceAudio(project.ToSequence());
        return rendered.Audio.Samples[0];
    }

    [Fact]
    public async Task AResumedNote_RendersLikeTheNoteNothingCut()
    {
        var alone = await Render(false);
        var crossed = await Render(true);

        var noteStart = Array.FindIndex(alone, sample => sample != 0);
        Assert.True(noteStart > 0);

        // The interferer cuts at steps 2..6 and ends at 7. HandleCut fades the silenced
        // instance out over CutFadeLengthMs (4 ms = 192 samples) while the resume starts at
        // full level, so only outside those windows are the two renders the same sound.
        // The encoder truncates offset * sample_rate to a whole sample, so a splice can land
        // up to one sample early - the artefact the plan accepts. At 50 Hz that is 0.007 of
        // full scale, while an offset that is actually wrong moves the phase by cycles.
        const float tolerance = 0.01f;
        const int fade = 192 + 8;
        var compared = 0;
        for (var i = 0; i < Math.Min(alone.Length, crossed.Length); i++)
        {
            var sinceStart = i - noteStart;
            var stepPosition = sinceStart % SamplesPerStep;
            if (sinceStart >= 2 * SamplesPerStep && sinceStart < 8 * SamplesPerStep &&
                (stepPosition < fade || stepPosition > SamplesPerStep - 8))
                continue;

            Assert.True(Math.Abs(alone[i] - crossed[i]) < tolerance,
                $"sample {i} ({sinceStart} into the note): {alone[i]} vs {crossed[i]}");
            compared++;
        }

        Assert.True(compared > 40000, $"only {compared} samples compared");
    }

    [Fact]
    public async Task WithoutAutoResume_TheHeldNoteIsGoneAfterTheFirstForeignCut()
    {
        var alone = await Render(false);
        var crossed = await Render(true, false);

        var noteStart = Array.FindIndex(alone, sample => sample != 0);
        // A third of a step past the first foreign cut, well clear of its 4 ms fade.
        var afterTheCut = noteStart + 2 * SamplesPerStep + SamplesPerStep / 3;

        Assert.NotEqual(0, alone[afterTheCut]);
        Assert.Equal(0, crossed.Length > afterTheCut ? crossed[afterTheCut] : 0);
    }
}
