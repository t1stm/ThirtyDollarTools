using ManagedBass;
using Serilog;
using ThirtyDollarEncoder.PCM;

namespace Shared.Audio.BASS;

public class BassAudioContext(ILogger logger) : AudioContext
{
    public override string Name => "BASS";
    private readonly ILogger _logger = logger.ForContext<BassAudioContext>(); 

    /// <summary>
    ///     Creates a global audio context.
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
            _logger.Error("Creation Exception: {@Exception}", e);
            return false;
        }
    }

    /// <summary>
    ///     Destroys the global audio context.
    /// </summary>
    public override void Destroy()
    {
        Bass.Free();
    }

    /// <summary>
    ///     Checks if there are any BASS errors.
    /// </summary>
    public override bool CheckErrors()
    {
        Errors error;
        var has_error = false;

        while ((error = Bass.LastError) != Errors.OK)
        {
            _logger.Error("Check Error: {@Exception}", error);
            has_error = true;
        }

        return has_error;
    }

    public override BassBuffer GetBufferObject(AudioData<float> sampleData, int sampleRate)
    {
        return new BassBuffer(this, _logger, sampleData, sampleRate);
    }
}