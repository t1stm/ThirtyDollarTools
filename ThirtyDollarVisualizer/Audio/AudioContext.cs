using ManagedBass;
using Configuration = ManagedBass.Configuration;

namespace ThirtyDollarVisualizer.Audio;

public class AudioContext
{
    public float GlobalVolume { get; set; } = .5f;
    public int SampleRate = 48000;

    /// <summary>
    /// Creates a global audio context.
    /// </summary>
    public void Create()
    {
        Bass.Init(-1, SampleRate);
        Bass.Volume = GlobalVolume;
        Bass.Configure(Configuration.UpdateThreads, Environment.ProcessorCount * 2);
    }
    /// <summary>
    /// Destroys the global audio context.
    /// </summary>
    public static void Destroy()
    {
        Bass.Free();
    }
    
    /// <summary>
    /// Checks if there are any OpenAL errors.
    /// </summary>
    public bool CheckErrors()
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
}