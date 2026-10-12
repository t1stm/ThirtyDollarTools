using Serilog.Core;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace EditorScene.Tests;

public class InstrumentPreviewTests
{
    private const uint SampleRate = 44100;

    private static PcmDataHolder Constant(float level, int length)
    {
        var data = AudioData<float>.WithLength(2, length);
        for (var i = 0; i < length; i++)
        {
            data.Samples[0][i] = level;
            data.Samples[1][i] = level;
        }

        return new PcmDataHolder { FloatData = data, SampleRate = SampleRate, Channels = 2 };
    }

    private static Task<AudioData<float>> Render(params (string, double, double?, float)[] combo)
    {
        var holder = new SampleHolder(Logger.None);
        holder.SampleList.Add(new Sound { Id = "long" }, Constant(0.25f, 4410));
        holder.SampleList.Add(new Sound { Id = "short" }, Constant(0.5f, 2205));
        var settings = new EncoderSettings { SampleRate = SampleRate, Channels = 2 };

        return EditorPlayback.RenderCombo(new PcmEncoder(holder, settings), new PlacementCalculator(settings),
            SampleRate, combo);
    }

    [Fact]
    public async Task RenderCombo_LayersEverySoundFromTheFirstSample()
    {
        // The instrument preview mixes its sounds the way a render does - layered on one step -
        // but from sample 0, without the lead-in step a full render opens with.
        var combo = await Render(("long", 0, null, 0), ("short", 0, null, 0));
        var longAlone = await Render(("long", 0, null, 0));
        var shortAlone = await Render(("short", 0, null, 0));

        Assert.True(longAlone.Samples[0][100] > 0); // sounding right away, no silent step in front
        Assert.True(combo.Samples[0][100] > longAlone.Samples[0][100]);
        Assert.Equal(longAlone.Samples[0][100] + shortAlone.Samples[0][100], combo.Samples[0][100], 4);
        Assert.Equal(longAlone.Samples[0][3000], combo.Samples[0][3000], 4); // past the short one
    }
}
