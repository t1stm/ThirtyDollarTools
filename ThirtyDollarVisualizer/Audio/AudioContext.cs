using OpenTK.Audio.OpenAL;

namespace ThirtyDollarVisualizer.Audio;

public class AudioContext
{
    private ALDevice device;
    public ALContext context;
    public float GlobalVolume { get; set; } = .5f;
    public int SampleRate = 48000;
    public int UpdateRate = 48000; // Hz
    
    /// <summary>
    /// Creates a global audio context.
    /// </summary>
    /// <param name="sources">The maxiumum sound sources at a given moment.</param>
    public void Create(int sources = 1024)
    {
        device = ALC.OpenDevice(null);
        context = ALC.CreateContext(device, 
            new ALContextAttributes(SampleRate, null, sources, UpdateRate, false));
        
        ALC.MakeContextCurrent(context);
        
        AL.Listener(ALListenerf.Gain, GlobalVolume);
    }
    /// <summary>
    /// Destroys the global audio context.
    /// </summary>
    public void Destroy()
    {
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(context);
        ALC.CloseDevice(device);
    }
    
    /// <summary>
    /// Checks if there are any OpenAL errors.
    /// </summary>
    public bool CheckErrors()
    {
        var has_error = false;
        ALError error;
        while ((error = AL.GetError()) != ALError.NoError)
        {
            has_error = true;
            Console.WriteLine($"({DateTime.Now:G}): [OpenAL Error]: (0x{(int)error:x8}) \'{AL.GetErrorString(error)}\'");
        }

        AlcError alc_error;
        while ((alc_error = ALC.GetError(device)) != AlcError.NoError)
        {
            has_error = true;
            Console.WriteLine($"({DateTime.Now:G}): [OpenALC Error]: (0x{(int)error:x8}) \'{alc_error}\'");
        }

        return has_error;
    }
}