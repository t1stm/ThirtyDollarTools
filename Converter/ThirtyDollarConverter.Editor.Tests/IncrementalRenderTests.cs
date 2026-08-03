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
        // Longer than the rest on purpose: it sets biggestEventLength, which is both how far
        // past an edit the re-rendered range has to reach and what sizes the render.
        holder.SampleList.Add(new Sound { Id = "pad" }, Sine(220, 1.5f));
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
        kick.AddSound("kick");
        var snare = project.NewInstrument("snare");
        snare.AddSound("snare");

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
    ///     Retuning a note without moving it: the old pitch has to be gone, not left ringing
    ///     under the new one.
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

    /// <summary>
    ///     Setting a volume on a note that had none (null follows the sequence volume): the
    ///     old loudness has to be gone, not left under the new one.
    /// </summary>
    [Fact]
    public async Task ChangingANoteVolume_LeavesNoTraceOfTheOldVolume()
    {
        var (project, note) = Project();
        await AssertMatchesFullRender(project, [() => note.Volume = 50]);
    }

    /// <summary>Same edit on a note whose automation fades it out.</summary>
    [Fact]
    public async Task ChangingANoteVolume_WithAutomation()
    {
        var (project, note) = Project();
        note.Automation = new AudioKeyframeManager { Repeats = 2 };
        note.Automation.Keyframes.Add(new AudioKeyframe
            { Gap = 1, Volume = new Modifier(0.5, ModifierKind.Multiply) });

        await AssertMatchesFullRender(project, [() => note.Volume = 50]);
    }

    /// <summary>Instrument-level volume: a per-sound volume scaling the note's.</summary>
    [Fact]
    public async Task ChangingAnInstrumentSoundVolume_LeavesNoTraceOfTheOldVolume()
    {
        var (project, note) = Project();
        await AssertMatchesFullRender(project,
            [() => note.Instrument.Sounds.Single(sound => sound.Sound == "kick").Volume = 40]);
    }

    /// <summary>The same instrument-level volume edit on notes that carry automation.</summary>
    [Fact]
    public async Task ChangingAnInstrumentSoundVolume_WithAutomation()
    {
        var (project, note) = Project();
        note.Automation = new AudioKeyframeManager { Repeats = 3 };
        note.Automation.Keyframes.Add(new AudioKeyframe
            { Gap = 1, Volume = new Modifier(0.5, ModifierKind.Multiply) });

        await AssertMatchesFullRender(project,
            [() => note.Instrument.Sounds.Single(sound => sound.Sound == "kick").Volume = 40]);
    }

    /// <summary>The same edit while the automation retriggers the note through cuts.</summary>
    [Fact]
    public async Task ChangingAnInstrumentSoundVolume_WithCutAutomation()
    {
        var (project, note) = Project();
        note.Automation = new AudioKeyframeManager { Repeats = 3 };
        note.Automation.Keyframes.Add(new AudioKeyframe
            { Gap = 1, Cut = true, Volume = new Modifier(0.5, ModifierKind.Multiply) });

        await AssertMatchesFullRender(project,
            [() => note.Instrument.Sounds.Single(sound => sound.Sound == "kick").Volume = 40]);
    }

    /// <summary>Volume edit on a note whose automation retriggers it through cuts.</summary>
    [Fact]
    public async Task ChangingANoteVolume_WithCutAutomation()
    {
        var (project, note) = Project();
        note.Automation = new AudioKeyframeManager { Repeats = 3 };
        note.Automation.Keyframes.Add(new AudioKeyframe
            { Gap = 1, Cut = true, Volume = new Modifier(0.5, ModifierKind.Multiply) });

        await AssertMatchesFullRender(project, [() => note.Volume = 50]);
    }

    /// <summary>
    ///     Four cut-automated notes on a two-sound instrument, then one sound gains a volume
    ///     (null -> 60) while the other keeps following the note. The diff holds only that
    ///     sound, so the range ends at its last placement - well before the render does - which
    ///     is where the chunked renderer used to drop its final sample: one sample kept its
    ///     pre-edit value while everything around it was rewritten. Sample-exact here, no
    ///     tolerance.
    /// </summary>
    [Fact]
    public async Task ChangingOneSoundVolume_OnACutAutomatedMultiSoundInstrument()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var instrument = project.NewInstrument("both");
        instrument.AddSound("kick");
        instrument.AddSound("pad");

        for (var step = 0; step < 4; step++)
        {
            var automation = new AudioKeyframeManager { Repeats = 6 };
            automation.Keyframes.Add(new AudioKeyframe { Gap = 1, Cut = true });
            track.Segments[0].Notes.Add(new Note { Step = step * 8, Instrument = instrument, Automation = automation });
        }

        project.Place(track, 0, 0);

        // An incremental render before the edit too - the app re-renders on every debounced
        // change, so the buffer the edit lands on is itself an incremental result.
        var rendered = await _encoder.GetSequenceAudio(project.ToSequence());
        rendered = await _encoder.ComputeIncrementalAudio(rendered, [project.ToSequence()]);

        instrument.Sounds.Single(sound => sound.Sound == "pad").Volume = 60;
        rendered = await _encoder.ComputeIncrementalAudio(rendered, [project.ToSequence()]);

        var full = await _encoder.GetSequenceAudio(project.ToSequence());
        for (var channel = 0; channel < full.Audio.ChannelCount; channel++)
            Assert.Equal(full.Audio.Samples[channel], rendered.Audio.Samples[channel]);
    }

    /// <summary>
    ///     A cut-automated instrument's sound list edited across four renders: two sounds
    ///     (one tuned -14, one plain), then a third is added, then tuned to -6 at 60% volume,
    ///     then removed again. The third sound is the longest one, so dropping it also shortens
    ///     what a full render produces - the incremental path has to notice that and rebuild
    ///     rather than hand back a buffer that still runs to the old length.
    /// </summary>
    [Fact]
    public async Task RemovingASoundFromACutAutomatedInstrument_LeavesNoUncutResidue()
    {
        var project = new ThirtyDollarProject();
        var track = project.NewTrack();
        var instrument = project.NewInstrument("layered");
        instrument.AddSound("kick");
        instrument.AddSound("snare");
        instrument.Sounds.Single(sound => sound.Sound == "kick").Value = -14;

        for (var step = 0; step < 4; step++)
        {
            var automation = new AudioKeyframeManager { Repeats = 6 };
            automation.Keyframes.Add(new AudioKeyframe { Gap = 1, Cut = true });
            track.Segments[0].Notes.Add(new Note { Step = step * 8, Instrument = instrument, Automation = automation });
        }

        project.Place(track, 0, 0);

        var rendered = await _encoder.GetSequenceAudio(project.ToSequence());

        // add a third sound, plain
        instrument.AddSound("pad");
        rendered = await _encoder.ComputeIncrementalAudio(rendered, [project.ToSequence()]);

        // tune it: -6 semitones at 60%
        var pad = instrument.Sounds.Single(sound => sound.Sound == "pad");
        (pad.Value, pad.Volume) = (-6, 60);
        rendered = await _encoder.ComputeIncrementalAudio(rendered, [project.ToSequence()]);

        // and take it back out
        instrument.Sounds.Remove(pad);
        rendered = await _encoder.ComputeIncrementalAudio(rendered, [project.ToSequence()]);

        var full = await _encoder.GetSequenceAudio(project.ToSequence());
        for (var channel = 0; channel < full.Audio.ChannelCount; channel++)
            Assert.Equal(full.Audio.Samples[channel], rendered.Audio.Samples[channel]);
    }

    /// <summary>Track automation: one keyframe manager applied to every note of the track.</summary>
    [Fact]
    public async Task ChangingANoteVolume_WithTrackAutomation()
    {
        var (project, note) = Project();
        var automation = new AudioKeyframeManager { Repeats = 2 };
        automation.Keyframes.Add(new AudioKeyframe { Gap = 1, Volume = new Modifier(0.5, ModifierKind.Multiply) });
        project.Tracks[0].AddTrackAutomation(automation);

        await AssertMatchesFullRender(project, [() => note.Volume = 50]);
    }
}