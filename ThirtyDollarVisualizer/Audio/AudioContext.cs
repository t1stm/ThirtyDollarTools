using OpenTK.Audio.OpenAL;

namespace ThirtyDollarVisualizer.Audio;

public static class AudioContext
{
    private static ALDevice device;
    private static ALContext context;
    public static float GlobalVolume { get; set; } = 1f;
    public static int SampleRate = 48000;
    public static int UpdateRate = 1000; // Hz
    
    /// <summary>
    /// Creates a global audio context.
    /// </summary>
    /// <param name="sources">The maxiumum sound sources at a given moment.</param>
    public static void Create(int sources = 1024)
    {
        device = ALC.OpenDevice(null);
        context = ALC.CreateContext(device, 
            new ALContextAttributes(SampleRate, null, sources, UpdateRate, false));
        
        ALC.MakeContextCurrent(context);
        
    }

    public static void Destroy()
    {
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(context);
        ALC.CloseDevice(device);
    }
}