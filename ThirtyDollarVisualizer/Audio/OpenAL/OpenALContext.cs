using OpenTK.Audio.OpenAL;
using OpenTK.Audio.OpenAL.ALC;
using ThirtyDollarEncoder.PCM;
using ThirtyDollarVisualizer.Helpers.Logging;
using ErrorCodeAl = OpenTK.Audio.OpenAL.ErrorCode;
using ErrorCodeAlc = OpenTK.Audio.OpenAL.ALC.ErrorCode;

namespace ThirtyDollarVisualizer.Audio.OpenAL;

public class OpenALContext : AudioContext
{
    private ALCContext _context;
    private ALCDevice _device;

    public int UpdateRate
    {
        get;
        set
        {
            field = value;
            Create();
        }
    } = 48000;

    public override string Name => "OpenAL";

    /// <summary>
    ///     Creates a global audio context.
    /// </summary>
    public override unsafe bool Create()
    {
        try
        {
            if (_device != ALCDevice.Null || _context != ALCContext.Null)
                Destroy();

            _device = ALC.OpenDevice((byte*)0);
            if (_device == ALCDevice.Null) return false;

            var contextAttributes = new ALCContextAttributes(SampleRate, null, 1024, UpdateRate, false);
            var attributeArray = contextAttributes.CreateAttributeArray();
            _context = ALC.CreateContext(_device, attributeArray);

            ALC.MakeContextCurrent(_context);
            AL.DistanceModel(DistanceModel.LinearDistanceClamped);

            AL.Listenerf(ListenerPNameF.Gain, GlobalVolume);
            AL.Listener3f(ListenerPName3F.Position, 0f, 0f, 0f);
            return true;
        }
        catch (Exception e)
        {
            DefaultLogger.Log("OpenAL Error", e.ToString());
            return false;
        }
    }

    /// <summary>
    ///     Destroys the global audio context.
    /// </summary>
    public override void Destroy()
    {
        ALC.MakeContextCurrent(ALCContext.Null);
        ALC.DestroyContext(_context);
        ALC.CloseDevice(_device);

        _device = ALCDevice.Null;
        _context = ALCContext.Null;
    }

    /// <summary>
    ///     Checks if there are any OpenAL errors.
    /// </summary>
    public override bool CheckErrors()
    {
        var has_error = false;
        ErrorCodeAl error;
        while ((error = AL.GetError()) != ErrorCodeAl.NoError)
        {
            has_error = true;
            DefaultLogger.Log("OpenAL Error", $"(0x{(int)error:x8}) \'{error}\'");
        }

        ErrorCodeAlc alc_error;
        while ((alc_error = ALC.GetError(_device)) != ErrorCodeAlc.NoError)
        {
            has_error = true;
            DefaultLogger.Log("OpenAL Error", $"(0x{(int)error:x8}) \'{alc_error}\'");
        }

        return has_error;
    }

    public override OpenALBuffer GetBufferObject(AudioData<float> sampleData, int sampleRate)
    {
        return new OpenALBuffer(this, sampleData, sampleRate);
    }
}