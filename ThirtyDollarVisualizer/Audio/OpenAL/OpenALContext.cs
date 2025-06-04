using OpenTK.Audio.OpenAL;
using ThirtyDollarEncoder.PCM;

namespace ThirtyDollarVisualizer.Audio.OpenAL;

public class OpenALContext : AudioContext
{
    private ALContext _context;
    private ALDevice _device;
    public int UpdateRate = 48000; // Hz

    public override string Name => "OpenAL";

    /// <summary>
    ///     Creates a global audio context.
    /// </summary>
    public override bool Create()
    {
        try
        {
            _device = ALC.OpenDevice(null);
            if (_device == ALDevice.Null) return false;
            _context = ALC.CreateContext(_device,
                new ALContextAttributes(SampleRate, null, 1024, UpdateRate, false));

            ALC.MakeContextCurrent(_context);
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
        ALC.DestroyContext(_context);
        ALC.CloseDevice(_device);
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
        while ((alc_error = ALC.GetError(_device)) != AlcError.NoError)
        {
            has_error = true;
            Console.WriteLine($"({DateTime.Now:G}): [OpenALC Error]: (0x{(int)error:x8}) \'{alc_error}\'");
        }

        return has_error;
    }

    public override AudibleBuffer GetBufferObject(AudioData<float> sampleData, int sampleRate)
    {
        return new OpenALBuffer(this, sampleData, sampleRate);
    }
}