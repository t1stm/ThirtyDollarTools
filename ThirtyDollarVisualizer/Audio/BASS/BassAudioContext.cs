using ManagedBass;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarVisualizer.Helpers.Logging;

namespace ThirtyDollarVisualizer.Audio.BASS;

public class BassAudioContext : AudioContext
{
    public override string Name => "BASS";

    /// <summary>
    /// Creates a global audio context.
    /// </summary>
    public override bool Create()
    {
        try
        {
            var successful_init = Bass.Init(-1, SampleRate);

            Bass.DeviceBufferLength = 16;
            Bass.PlaybackBufferLength = 128;

            Bass.GlobalSampleVolume = (int)(GlobalVolume * 10000);
            Bass.Configure(Configuration.UpdateThreads, Environment.ProcessorCount * 2);
            Bass.Configure(Configuration.TruePlayPosition, 0);

            if (!successful_init) CheckErrors();

            return successful_init;
        }
        catch (Exception e)
        {
            DefaultLogger.Log("Bass Error", e.ToString());
            return false;
        }
    }

    /// <summary>
    /// Destroys the global audio context.
    /// </summary>
    public override void Destroy()
    {
        Bass.Free();
    }

    /// <summary>
    /// Checks if there are any BASS errors.
    /// </summary>
    public override bool CheckErrors()
    {
        Errors error;
        var has_error = false;

        while ((error = Bass.LastError) != Errors.OK)
        {
            DefaultLogger.Log("Bass Error", error.ToString());
            has_error = true;
        }

        return has_error;
    }

    public override BassBuffer GetBufferObject(AudioData<float> sampleData, int sampleRate)
    {
        return new BassBuffer(this, sampleData, sampleRate);
    }
}