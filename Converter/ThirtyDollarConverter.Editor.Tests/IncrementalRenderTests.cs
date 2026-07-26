using Serilog.Core;
using ThirtyDollarConverter.Objects;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarParser;

namespace ThirtyDollarConverter.Editor.Tests;

/// <summary>
///     The editor's playback loop: render once, then feed every edit through
///     <see cref="PcmEncoder.ComputeIncrementalAudio" /> against the previous render. The
///     incremental result has to be sample-identical to a full render of the edited project,
///     or an edit leaves audible leftovers of what it replaced.
/// </summary>
public class IncrementalRenderTests
{
    private readonly PcmEncoder _encoder;

    public IncrementalRenderTests()
    {
        var holder = new SampleHolder(Logger.None);
        holder.SampleList.Add(new Sound { Id = "kick" }, Sine(440));
        holder.SampleList.Add(new Sound { Id = "snare" }, Sine(880));
        _encoder = new PcmEncoder(holder, new EncoderSettings { SampleRate = 44100, Channels = 2 });
    }

    private static PcmDataHolder Sine(float frequency, float seconds = 0.5f)
    {
        const uint sample_rate = 44100;
        var count = (int)(sample_rate * seconds);
        var data = AudioData<float>.WithLength(2, count);
        for (var i = 0; i < count; i++)
        {
            var sample = (float)Math.Sin(2 * Math.PI * frequency * i / sample_rate);
            data.Samples[0][i] = sample;
            data.Samples[1][i] = sample;
        }

        return new PcmDataHolder { FloatData = data, SampleRate = sample_rate, Channels = 2 };
    }

    /// <summary>A project with one placed track: kicks on 0 and 8, a snare on 4.</summary>
    private static (ThirtyDollarProject Project, Note Note) Project()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var kick = project.NewInstrument("kick");
        kick.Sounds.Add("kick");
        var snare = project.NewInstrument("snare");
        snare.Sounds.Add("snare");

        var note = new Note { Step = 0, Instrument = kick };
        track.Segments[0].Notes.Add(note);
        track.Segments[0].Notes.Add(new Note { Step = 4, Instrument = snare });
        track.Segments[0].Notes.Add(new Note { Step = 8, Instrument = kick });
        project.Place(track, 0, 0);

        return (project, note);
    }

    private async Task AssertMatchesFullRender(ThirtyDollarProject project, Action[] edits)
    {
        var rendered = await _encoder.GetSequenceAudio(project.ToSequence());
        foreach (var edit in edits)
        {
            edit();
            rendered = await _encoder.ComputeIncrementalAudio(rendered, [project.ToSequence()]);
        }

        var full = await _encoder.GetSequenceAudio(project.ToSequence());

        Assert.Equal(full.Audio.ChannelCount, rendered.Audio.ChannelCount);
        for (var channel = 0; channel < full.Audio.ChannelCount; channel++)
            Assert.Equal(full.Audio.Samples[channel], rendered.Audio.Samples[channel],
                (a, b) => MathF.Abs(a - b) < 0.001f);
    }

    /// <summary>
    ///     Retuning a note without moving it: the old pitch has to be subtracted, not left
    ///     ringing under the new one.
    /// </summary>
    [Fact]
    public async Task ChangingANoteValue_LeavesNoTraceOfTheOldPitch()
    {
        var (project, note) = Project();
        await AssertMatchesFullRender(project, [() => note.Value = 5]);
    }

    /// <summary>
    ///     The same retune done by walking the note off its step and back. Moving it changes
    ///     its cuts' placements too, which forces the full re-render the in-place edit skips -
    ///     this path stayed clean while the one above didn't.
    /// </summary>
    [Fact]
    public async Task MovingAndRetuningANote_MatchesAFullRender()
    {
        var (project, note) = Project();
        project.Tracks[0].Segments[0].Notes.Add(new Note { Step = 2, Instrument = note.Instrument, IsCut = true });

        await AssertMatchesFullRender(project, [
            () => note.Step = 1,
            () => note.Value = 5,
            () => note.Step = 0
        ]);
    }

    /// <summary>Same edit while a cut note silences the same instrument later on.</summary>
    [Fact]
    public async Task ChangingANoteValue_WithACutOnTheSameInstrument()
    {
        var (project, note) = Project();
        var segment = project.Tracks[0].Segments[0];
        segment.Notes.Add(new Note { Step = 2, Instrument = note.Instrument, IsCut = true });

        await AssertMatchesFullRender(project, [() => note.Value = 5]);
    }

    /// <summary>Same edit on a note whose automation retriggers it through cuts.</summary>
    [Fact]
    public async Task ChangingANoteValue_WithCutAutomation()
    {
        var (project, note) = Project();
        note.Automation = new AudioKeyframeManager { Repeats = 2 };
        note.Automation.Keyframes.Add(new AudioKeyframe { Gap = 1, Cut = true });

        await AssertMatchesFullRender(project, [() => note.Value = 5]);
    }
}
