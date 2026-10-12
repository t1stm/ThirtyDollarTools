using Serilog.Core;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

public class InstrumentPreviewTests
{
    private const uint SampleRate = 44100;

    private static PcmDataHolder Samples(int length, Func<int, float> level)
    {
        var data = AudioData<float>.WithLength(2, length);
        for (var i = 0; i < length; i++)
        {
            data.Samples[0][i] = level(i);
            data.Samples[1][i] = level(i);
        }

        return new PcmDataHolder { FloatData = data, SampleRate = SampleRate, Channels = 2 };
    }

    private static SampleHolder Holder()
    {
        var holder = new SampleHolder(Logger.None);
        holder.SampleList.Add(new Sound { Id = "long" }, Samples(4410, _ => 0.25f));
        holder.SampleList.Add(new Sound { Id = "short" }, Samples(2205, _ => 0.5f));
        // Each sample says where it is, so a skipped start shows as the level it begins on.
        holder.SampleList.Add(new Sound { Id = "ramp" }, Samples(4410, i => i / 4410f));
        return holder;
    }

    private static EncoderSettings Settings => new() { SampleRate = SampleRate, Channels = 2 };

    private static Task<AudioData<float>> Render(
        params (string, double, double?, float, double)[] combo)
    {
        return EditorPlayback.RenderCombo(new PcmEncoder(Holder(), Settings), new PlacementCalculator(Settings),
            SampleRate, combo);
    }

    private static Instrument Layered(params string[] sounds)
    {
        var instrument = new Instrument();
        foreach (var sound in sounds) instrument.AddSound(sound);
        return instrument;
    }

    [Fact]
    public async Task RenderCombo_LayersEverySoundFromTheFirstSample()
    {
        // The instrument preview mixes its sounds the way a render does - layered on one step -
        // but from sample 0, without the lead-in step a full render opens with.
        var combo = await Render(("long", 0, null, 0, 0), ("short", 0, null, 0, 0));
        var longAlone = await Render(("long", 0, null, 0, 0));
        var shortAlone = await Render(("short", 0, null, 0, 0));

        Assert.True(longAlone.Samples[0][100] > 0); // sounding right away, no silent step in front
        Assert.True(combo.Samples[0][100] > longAlone.Samples[0][100]);
        Assert.Equal(longAlone.Samples[0][100] + shortAlone.Samples[0][100], combo.Samples[0][100], 4);
        Assert.Equal(longAlone.Samples[0][3000], combo.Samples[0][3000], 4); // past the short one
    }

    [Fact]
    public async Task SoundWithAnOffset_StartsThatFarIntoItsSample()
    {
        var instrument = Layered("ramp");
        instrument.Sounds[0].Offset = 0.05; // 2205 samples at 44.1 kHz

        var mixed = await Render(EditorPlayback.ComboOf(instrument));

        Assert.Equal(2205 / 4410f, mixed.Samples[0][0], 2);

        // The one-sound preview skips the same stretch PcmEncoder does.
        var processed = new SampleProcessor(Holder().SampleList, Settings)
            .ProcessEvent(new NormalEvent { SoundEvent = "ramp", Value = 0 });
        var skipped = EditorPlayback.SkipOffset(processed, 0.05, 0, SampleRate);
        Assert.Equal(processed.GetLength() - 2205, skipped.GetLength());
        Assert.Equal(mixed.Samples[0][0], skipped.Samples[0][0], 2);
    }

    [Fact]
    public async Task InstrumentWideVolume_ScalesTheWholeMix()
    {
        var full = Layered("long", "short");
        var half = Layered("long", "short");
        half.Volume = 50;

        var loud = await Render(EditorPlayback.ComboOf(full));
        var quiet = await Render(EditorPlayback.ComboOf(half));

        Assert.Equal(loud.Samples[0][100] / 2, quiet.Samples[0][100], 4);
        Assert.Equal(loud.Samples[0][3000] / 2, quiet.Samples[0][3000], 4);
    }

    [Theory]
    [InlineData("value")]
    [InlineData("volume")]
    [InlineData("pan")]
    [InlineData("offset")]
    [InlineData("sound offset")]
    public void ChangingAnyField_ChangesTheComboKey(string field)
    {
        var instrument = Layered("long", "short");
        var before = EditorPlayback.ComboOf(instrument);

        switch (field)
        {
            case "value": instrument.Value = 1; break;
            case "volume": instrument.Volume = 80; break;
            case "pan": instrument.Pan = -10; break;
            case "offset": instrument.Offset = 0.01; break;
            case "sound offset": instrument.Sounds[1].Offset = 0.01; break;
        }

        Assert.False(before.SequenceEqual(EditorPlayback.ComboOf(instrument)));
    }
}
