using System.Runtime.InteropServices;
using Sundex.Engine.Audio;
using ThirtyDollarConverter.Encoder.PCM;

namespace Shared.Audio;

/// <summary>The converter's sample containers, uploaded to a Sundex <see cref="AudioDevice" />.</summary>
public static class AudioUploads
{
    /// <summary>Planar float channels, interleaved into one native buffer.</summary>
    public static AudioBuffer CreateBuffer(this AudioDevice device, AudioData<float> data, int sampleRate)
    {
        var length = (int)data.GetLength();
        var channels = (int)data.ChannelCount;

        // Native scratch, freed as soon as the device has copied it. A pooled array stays with the
        // pool once returned, and an editor wave reference's is the size of the whole file,
        // uploaded from whichever thread decoded it - every such thread would keep one.
        unsafe
        {
            var count = length * channels;
            var samples = (float*)NativeMemory.Alloc((nuint)Math.Max(count, 1), sizeof(float));
            try
            {
                for (var i = 0; i < length; i++)
                for (var j = 0; j < channels; j++)
                    samples[i * channels + j] = data.Samples[j][i];

                return device.CreateBuffer(new ReadOnlySpan<float>(samples, count), channels, sampleRate);
            }
            finally
            {
                NativeMemory.Free(samples);
            }
        }
    }

    /// <summary>
    ///     The samples the way the source stores them - 8-bit, 16-bit or 32-bit float, in the
    ///     source's own channel count - rather than widened to stereo float, which doubles a
    ///     16-bit stereo file and quadruples a 16-bit mono one. 24-bit, which the device can't
    ///     hold, is widened to float: the smallest format that keeps it whole.
    /// </summary>
    public static AudioBuffer CreateBuffer(this AudioDevice device, PcmDataHolder pcm)
    {
        var data = pcm.AudioData ?? [];
        var channels = (int)pcm.Channels;
        var sampleRate = (int)pcm.SampleRate;

        if (pcm.Encoding == Encoding.Int24)
        {
            var count = data.Length / 3;
            unsafe
            {
                var widened = (float*)NativeMemory.Alloc((nuint)Math.Max(count, 1), sizeof(float));
                try
                {
                    var span = new Span<float>(widened, count);
                    DataHolderExtensions.Int24ToFloat(data, span);
                    return device.CreateBuffer(span, channels, sampleRate);
                }
                finally
                {
                    NativeMemory.Free(widened);
                }
            }
        }

        var format = pcm.Encoding switch
        {
            Encoding.Int8 => SampleFormat.UInt8, // unsigned, in a WAV and on the device alike
            Encoding.Int16 => SampleFormat.Int16,
            Encoding.Float32 => SampleFormat.Float32,
            _ => throw new InvalidDataException($"{(int)pcm.Encoding}-bit samples aren't supported.")
        };

        return device.CreateBuffer(data, format, channels, sampleRate);
    }
}
