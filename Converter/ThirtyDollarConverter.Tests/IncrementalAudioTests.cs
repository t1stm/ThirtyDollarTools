using Serilog.Core;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;

namespace ThirtyDollarConverter.Tests;

public class IncrementalAudioTests
{
    private readonly PcmEncoder _encoder;
    private readonly SampleHolder _sampleHolder;
    private readonly EncoderSettings _settings;

    public IncrementalAudioTests()
    {
        _sampleHolder = new SampleHolder(Logger.None);
        AddExampleSounds();
        _settings = new EncoderSettings
        {
            SampleRate = 44100,
            Channels = 2
        };
        _encoder = new PcmEncoder(_sampleHolder, _settings);
    }

    private void AddExampleSounds()
    {
        _sampleHolder.SampleList.Add(new Sound { Id = "test1" }, GenerateSineWave(440, 0.1f));
        _sampleHolder.SampleList.Add(new Sound { Id = "test2" }, GenerateSineWave(880, 0.1f));
        _sampleHolder.SampleList.Add(new Sound { Id = "test3" }, GenerateSineWave(1320, 0.1f));
    }

    private static PcmDataHolder GenerateSineWave(float frequency, float duration)
    {
        const uint sampleRate = 44100;
        var sampleCount = (int)(sampleRate * duration);
        var data = AudioData<float>.WithLength(2, sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (float)Math.Sin(2 * Math.PI * frequency * i / sampleRate);
            data.Samples[0][i] = sample;
            data.Samples[1][i] = sample;
        }

        return new PcmDataHolder
        {
            FloatData = data,
            SampleRate = sampleRate,
            Channels = 2
        };
    }

    private static Sequence Sequence(params BaseEvent[] events)
    {
        return new Sequence { Events = events };
    }

    private static NormalEvent Event(string soundEvent, float value = 0)
    {
        return new NormalEvent
        {
            SoundEvent = soundEvent,
            Value = value
        };
    }

    private static NormalEvent Combine()
    {
        return Event("!combine");
    }

    private static IndividualCutEvent Icut(params string[] sounds)
    {
        return new IndividualCutEvent([.. sounds]);
    }

    /// <summary>
    ///     A sequence carrying the separated per-sound channels its individual cuts target -
    ///     what the parser and SequenceBuilder both populate, and what the encoder needs to
    ///     pre-allocate a cut's track.
    /// </summary>
    private static Sequence CutSequence(params BaseEvent[] events)
    {
        var sequence = new Sequence { Events = events };
        foreach (var ev in events)
        {
            if (ev.SoundEvent is not null) sequence.UsedSounds.Add(ev.SoundEvent);
            if (ev is IndividualCutEvent cut)
                foreach (var sound in cut.CutSounds)
                    sequence.SeparatedChannels.Add(sound);
        }

        return sequence;
    }

    private static NormalEvent VolumeEvent(float value)
    {
        return new NormalEvent
        {
            SoundEvent = "!volume",
            Value = value,
            ValueScale = ValueScale.None
        };
    }

    private static void CompareAudio(AudioData<float> audio1, AudioData<float> audio2)
    {
        Assert.Equal(audio1.ChannelCount, audio2.ChannelCount);
        for (var channel = 0; channel < audio1.ChannelCount; channel++)
        {
            var a1Channel = audio1.Samples[channel];
            var a2Channel = audio2.Samples[channel];
            Assert.Equal(a1Channel, a2Channel, (f, f1) => MathF.Abs(f - f1) < 0.001f);
        }
    }

    [Fact]
    public async Task ShouldAddSoundsCorrectly()
    {
        var sequence = Sequence(Event("test1"));
        var new_sequence = Sequence(Event("test1"), Combine(), Event("test2"), Combine(), Event("test3"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldRemoveSoundsCorrectly()
    {
        var sequence = Sequence(Event("test1"), Combine(), Event("test2"), Combine(), Event("test3"));
        var new_sequence = Sequence(Event("test1"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldReplaceSoundsCorrectly()
    {
        var sequence = Sequence(Event("test1"), Combine(), Event("test3"));
        var new_sequence = Sequence(Event("test1"), Combine(), Event("test2"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldRemoveSoundsCorrectly_WithCutEvent()
    {
        var sequence = Sequence(Event("test1"), Combine(), Event("test2"), Combine(), Event("test3"), Event("!cut"));
        var new_sequence = Sequence(Event("test1"), Combine(), Event("test2"), Event("!cut"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldReplaceSoundsCorrectly_WithCutEvent()
    {
        var sequence = Sequence(Event("test1"), Combine(), Event("test3"), Event("!cut"));
        var new_sequence = Sequence(Event("test1"), Combine(), Event("test2"), Event("!cut"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    /// <summary>
    ///     An individual cut that merely exists must not force a full re-render: editing a
    ///     sound on the cut's own target track has to route through the overlay's copy of
    ///     that track, get cut there the same way, and land on the real track when summed.
    /// </summary>
    [Fact]
    public async Task ShouldEditSoundOnCutTargetTrackIncrementally()
    {
        var sequence = CutSequence(Event("test1"), Icut("test1"), Event("test2"));
        var new_sequence = CutSequence(Event("test1", 6), Icut("test1"), Event("test2"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    /// <summary>Same, for a sound the cut does not target - it stays on the default track.</summary>
    [Fact]
    public async Task ShouldEditSoundBesideIndividualCutIncrementally()
    {
        var sequence = CutSequence(Event("test1"), Icut("test1"), Event("test2"));
        var new_sequence = CutSequence(Event("test1"), Icut("test1"), Event("test3"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    /// <summary>
    ///     A cut that gains a target compares equal to the old one (Placement equality ignores
    ///     CutSounds), so the diff can't catch it - but the new target's track doesn't exist on
    ///     the old mixer yet. The encoder must notice and re-render, or the cut silently no-ops.
    /// </summary>
    [Fact]
    public async Task ShouldRenderCutThatGainedATarget()
    {
        var sequence = CutSequence(Event("test1"), Combine(), Event("test2"), Icut("test1"), Event("test3"));
        var new_sequence = CutSequence(Event("test1"), Combine(), Event("test2"), Icut("test1", "test2"),
            Event("test3"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldHandleChangingValuesCorrectly()
    {
        var sequence = Sequence(Event("test1"), Combine(), Event("test3"));
        var new_sequence = Sequence(Event("test1"), Combine(), Event("test3", 6));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldHandleChangingValuesCorrectly_WithCutEvent()
    {
        var sequence = Sequence(Event("test1"), Combine(), Event("test3"), Event("!cut"));
        var new_sequence = Sequence(Event("test1"), Combine(), Event("test3", 6), Event("!cut"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldRemoveOneOfTwoIdenticalStackedSounds()
    {
        var sequence = Sequence(Event("test1"), Combine(), Event("test1"));
        var new_sequence = Sequence(Event("test1"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldHandleVolumeEventChanges()
    {
        var sequence = Sequence(VolumeEvent(50), Event("test1"));
        var new_sequence = Sequence(VolumeEvent(100), Event("test1"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldHandleChangesAfterCutEvent()
    {
        var sequence = Sequence(Event("test1"), Event("!cut"), Event("test3"));
        var new_sequence = Sequence(Event("test1"), Event("!cut"), Event("test2"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldHandleGrowingSequence()
    {
        var sequence = Sequence(Event("test1"));
        var new_sequence = Sequence(Event("test1"), Event("test2"), Event("test3"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }

    [Fact]
    public async Task ShouldKeepAudioWhenNothingChanged()
    {
        var sequence = Sequence(Event("test1"), Event("test2"));
        var new_sequence = Sequence(Event("test1"), Event("test2"));

        var initial_rendered = await _encoder.GetMultipleSequencesAudio([sequence]);
        var incremental_rendered = await _encoder.ComputeIncrementalAudio(initial_rendered, [new_sequence]);

        var pure_rendered = await _encoder.GetMultipleSequencesAudio([new_sequence]);
        CompareAudio(incremental_rendered.Audio, pure_rendered.Audio);
    }
}