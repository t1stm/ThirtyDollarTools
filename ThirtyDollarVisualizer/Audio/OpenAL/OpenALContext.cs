using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio;

public class OpenALContext : AudioContext
{
    public ALContext context;
    private ALDevice device;
    public int UpdateRate = 48000; // Hz

    public override string Name => "OpenAL";

    /// <summary>
    ///     Creates a global audio context.
    /// </summary>
    public override bool Create()
    {
        try
        {
            device = ALC.OpenDevice(null);
            if (device == ALDevice.Null) return false;
            context = ALC.CreateContext(device,
                new ALContextAttributes(SampleRate, null, 1024, UpdateRate, false));

            ALC.MakeContextCurrent(context);
            AL.DistanceModel(ALDistanceModel.LinearDistanceClamped);

            AL.Listener(ALListenerf.Gain, GlobalVolume);
            AL.Listener(ALListener3f.Position, 0f, 0f, 0f);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[OpenAL Error]: {e}");
            return false;
        }
    }

    /// <summary>
    ///     Destroys the global audio context.
    /// </summary>
    public override void Destroy()
    {
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(context);
        ALC.CloseDevice(device);
    }

    /// <summary>
    ///     Checks if there are any OpenAL errors.
    /// </summary>
    public override bool CheckErrors()
    {
        var has_error = false;
        ALError error;
        while ((error = AL.GetError()) != ALError.NoError)
        {
            has_error = true;
            Console.WriteLine(
                $"({DateTime.Now:G}): [OpenAL Error]: (0x{(int)error:x8}) \'{AL.GetErrorString(error)}\'");
        }

        AlcError alc_error;
        while ((alc_error = ALC.GetError(device)) != AlcError.NoError)
        {
            has_error = true;
            Console.WriteLine($"({DateTime.Now:G}): [OpenALC Error]: (0x{(int)error:x8}) \'{alc_error}\'");
        }

        return has_error;
    }

    public override AudibleBuffer GetBufferObject(AudioData<float> sample_data, int sample_rate)
    {
        return new OpenALBuffer(this, sample_data, sample_rate);
    }
}