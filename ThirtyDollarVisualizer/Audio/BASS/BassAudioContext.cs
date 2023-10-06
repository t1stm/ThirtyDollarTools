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
        var successful_init = Bass.Init(-1, SampleRate);
        Bass.Volume = GlobalVolume;
        Bass.Configure(Configuration.UpdateThreads, Environment.ProcessorCount * 2);

        return successful_init;
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