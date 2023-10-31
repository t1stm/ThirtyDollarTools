using ManagedBass;
using ThirtyDollarEncoder.PCM;
using Configuration = ManagedBass.Configuration;

namespace ThirtyDollarVisualizer.Audio;

public class BassAudioContext : AudioContext
{
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

            if (!successful_init)
            {
                CheckErrors();
            }

            return successful_init;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[BASS Error]: {e}");
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
            Console.WriteLine($"[BASS Error]: {error}");
            has_error = true;
        } 
        
        return has_error;
    }

    public override AudibleBuffer GetBufferObject(AudioData<float> sample_data, int sample_rate)
    {
        return new BassBuffer(this, sample_data, sample_rate);
    }
}