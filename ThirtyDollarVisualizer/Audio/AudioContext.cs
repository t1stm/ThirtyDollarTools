using OpenTK.Audio.OpenAL;

namespace ThirtyDollarVisualizer.Audio;

public static class AudioContext
{
    private static ALDevice device;
    private static ALContext context;
    public static float GlobalVolume { get; set; } = 1f;
    public static int SampleRate = 48000;
    public static int UpdateRate = 1000; // Hz
    public static void Create()
    {
        device = ALC.OpenDevice(null);
        context = ALC.CreateContext(device, 
            new ALContextAttributes(SampleRate, null, 256 * 4, UpdateRate, false));
        
        ALC.MakeContextCurrent(context);
        
    }

    public static void Destroy()
    {
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(context);
        ALC.CloseDevice(device);
    }
}