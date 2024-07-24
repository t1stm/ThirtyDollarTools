using System.Runtime.InteropServices;

namespace ThirtyDollarEncoder.PCM;

public static class DataHolderExtensions // i love duplicating code (false statement)
{
    public static byte[]? ReadAsInt8Array(this PcmDataHolder holder)
    {
        throw new Exception("Why would anyone want to use 8 bit integers for music. I won't implement this for now.");
    }

    /// <summary>
    /// Reads / returns cached audio data from this data holder as short (16 bit) AudioData.
    /// </summary>
    /// <param name="holder">The current PcmDataHolder.</param>
    /// <param name="monoToStereo">Whether to return mono source data as stereo.</param>
    /// <returns>An AudioData object that contains the converted data.</returns>
    /// <exception cref="Exception">Exception thrown when an error occurs.</exception>
    public static AudioData<short>? ReadAsInt16Array(this PcmDataHolder holder, bool monoToStereo)
    {
        // lock that disallows multiple writing to the parsed channels
        holder.Semaphore.Wait();
        try
        {
            // basic cache checks
            if (holder.ShortData != null) return holder.ShortData;
            if (holder.AudioData == null) return null;

            // extract important variables
            var audio_span = holder.AudioData.AsSpan();
            var channels_count = (int)holder.Channels;
            var length = audio_span.Length;
            var source_encoding = holder.Encoding;

            // add different pre-casted arrays made using c# magic
            // also known as "casting pointers" in languages like c
            var short_span = ReadAsShortArray(audio_span);
            var int24_span = ReadAsInt24Array(audio_span);
            var float_span = ReadAsFloatArray(audio_span);

            // get each channel's length
            var destination_length = length / ((int)source_encoding / 8) / channels_count;
            
            // create arrays that store the audio data and handle mono to stereo storage
            var export_channels_count = monoToStereo && channels_count < 2 ? 2 : channels_count;
            var export_channels = new short[export_channels_count][];

            // allocates all channels
            for (var i = 0; i < export_channels_count; i++)
            {
                export_channels[i] = new short[destination_length];
            }

            // convert known source channels
            for (var current_channel = 0; current_channel < channels_count; current_channel++)
            {
                var channel = export_channels[current_channel];
                FillChannel_Short(channel, channels_count, current_channel, 
                    source_encoding, audio_span, short_span, int24_span, float_span);
            }

            // handle mono to stereo conversion
            if (export_channels_count != channels_count)
            {
                FillChannel_Short(export_channels[1], 1, 0, 
                    source_encoding, audio_span, short_span, int24_span, float_span);
            }

            unchecked // unchecked due to "possible" overflow
            {
                var audio_data = new AudioData<short>((uint)export_channels_count);
                audio_data.Samples = export_channels;
                return audio_data;
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to read sample holder as float array. {e}");
        }
        finally
        {
            holder.Semaphore.Release();
        }
    }

    public static Int24[]? ReadAsInt24Array(this PcmDataHolder holder)
    {
        throw new Exception("Working with Int24 isn't supported (yet.)");
    }

    /// <summary>
    /// Reads / returns cached audio data from this data holder as float (32 bit) AudioData.
    /// </summary>
    /// <param name="holder">The current PcmDataHolder.</param>
    /// <param name="monoToStereo">Whether to return mono source data as stereo.</param>
    /// <returns>An AudioData object that contains the converted data.</returns>
    /// <exception cref="Exception">Exception thrown when an error occurs.</exception>
    public static AudioData<float>? ReadAsFloat32Array(this PcmDataHolder holder, bool monoToStereo)
    {
        // lock that disallows multiple writing to the parsed channels
        holder.Semaphore.Wait();
        try
        {
            // basic cache checks
            if (holder.FloatData != null) return holder.FloatData;
            if (holder.AudioData == null) return null;

            // extract important variables
            var audio_span = holder.AudioData.AsSpan();
            var channels_count = (int)holder.Channels;
            var length = audio_span.Length;
            var source_encoding = holder.Encoding;

            // add different pre-casted arrays made using c# magic
            // also known as "casting pointers" in languages like c
            var short_span = ReadAsShortArray(audio_span);
            var int24_span = ReadAsInt24Array(audio_span);
            var float_span = ReadAsFloatArray(audio_span);

            // get each channel's length
            var destination_length = length / ((int)source_encoding / 8) / channels_count;
            
            // create arrays that store the audio data and handle mono to stereo storage
            var export_channels_count = monoToStereo && channels_count < 2 ? 2 : channels_count;
            var export_channels = new float[export_channels_count][];

            // allocates all channels
            for (var i = 0; i < export_channels_count; i++)
            {
                export_channels[i] = new float[destination_length];
            }

            // convert known source channels
            for (var current_channel = 0; current_channel < channels_count; current_channel++)
            {
                var channel = export_channels[current_channel];
                FillChannel_Float(channel, channels_count, current_channel, 
                    source_encoding, audio_span, short_span, int24_span, float_span);
            }

            // handle mono to stereo conversion
            if (export_channels_count != channels_count)
            {
                FillChannel_Float(export_channels[1], 1, 0, 
                    source_encoding, audio_span, short_span, int24_span, float_span);
            }

            unchecked // unchecked due to "possible" overflow
            {
                var audio_data = new AudioData<float>((uint)export_channels_count);
                audio_data.Samples = export_channels;
                return audio_data;
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Failed to read sample holder as float array. {e}");
        }
        finally
        {
            holder.Semaphore.Release();
        }
    }
    
    /// <summary>
    /// Converts a given sample array to a float (32 bit) array.
    /// </summary>
    /// <param name="channel">The current channel data.</param>
    /// <param name="channels_count">How many channels the destination has.</param>
    /// <param name="current_channel">The current channel.</param>
    /// <param name="source_encoding">The source encoding.</param>
    /// <param name="audio_span">The source's raw data.</param>
    /// <param name="short_span">The source data casted to 16 bit.</param>
    /// <param name="int24_span">The source data casted to 24 bit.</param>
    /// <param name="float_span">The source data casted to 32 bit float.</param>
    /// <exception cref="ArgumentOutOfRangeException">Exception when the given encoding isn't handled.</exception>
    private static void FillChannel_Float(Span<float> channel, int channels_count, int current_channel, Encoding source_encoding, Span<byte> audio_span,
        ReadOnlySpan<short> short_span, ReadOnlySpan<Int24> int24_span, ReadOnlySpan<float> float_span)
    {
        for (var i = 0; i < channel.Length; i++)
        {
            var index = i * channels_count + current_channel;
            channel[i] = source_encoding switch
            {
                Encoding.Int8 => audio_span[index] / 256f,
                Encoding.Int16 => short_span[index] / 32768f,
                Encoding.Int24 => int24_span[index].ToFloat(),
                Encoding.Float32 => float_span[index],
                _ => throw new ArgumentOutOfRangeException(nameof(source_encoding),
                    "Given PCM data holder has invalid encoding.")
            };
        }
    }
    
    /// <summary>
    /// Converts a given sample array to a short (16 bit) array.
    /// </summary>
    /// <param name="channel">The current channel data.</param>
    /// <param name="channels_count">How many channels the destination has.</param>
    /// <param name="current_channel">The current channel.</param>
    /// <param name="source_encoding">The source encoding.</param>
    /// <param name="audio_span">The source's raw data.</param>
    /// <param name="short_span">The source data casted to 16 bit.</param>
    /// <param name="int24_span">The source data casted to 24 bit.</param>
    /// <param name="float_span">The source data casted to 32 bit float.</param>
    /// <exception cref="ArgumentOutOfRangeException">Exception when the given encoding isn't handled.</exception>
    private static void FillChannel_Short(Span<short> channel, int channels_count, int current_channel, Encoding source_encoding, Span<byte> audio_span,
        ReadOnlySpan<short> short_span, ReadOnlySpan<Int24> int24_span, ReadOnlySpan<float> float_span)
    {
        for (var i = 0; i < channel.Length; i++)
        {
            var index = i * channels_count + current_channel;
            channel[i] = source_encoding switch
            {
                Encoding.Int8 => (short)(audio_span[index] * 256),
                Encoding.Int16 => short_span[index],
                Encoding.Int24 => (short)(int24_span[index].ToFloat() * 32768f),
                Encoding.Float32 => (short)(float_span[index] * 32768f),
                _ => throw new ArgumentOutOfRangeException(nameof(source_encoding),
                    "Given PCM data holder has invalid encoding.")
            };
        }
    }

    private static ReadOnlySpan<short> ReadAsShortArray(Span<byte> bytes)
    {
        return MemoryMarshal.Cast<byte, short>(bytes);
    }
    
    private static ReadOnlySpan<float> ReadAsFloatArray(Span<byte> bytes)
    {
        return MemoryMarshal.Cast<byte, float>(bytes);
    }

    private static ReadOnlySpan<Int24> ReadAsInt24Array(Span<byte> bytes)
    {
        return MemoryMarshal.Cast<byte, Int24>(bytes);
    }
}