using System.Runtime.InteropServices;
using ThirtyDollarConverter.Encoder.PCM;
using Encoding = System.Text.Encoding;

namespace ThirtyDollarConverter.Encoder.Wave;

/// <summary>
///     Writes <see cref="AudioData{T}" /> out as a RIFF WAVE file - the encode-side counterpart of
///     <see cref="WaveDecoder" />.
/// </summary>
public static class WaveEncoder
{
    /// <summary>
    ///     Exports an AudioData object as a WAVE file.
    /// </summary>
    /// <param name="location">The location you want to export to.</param>
    /// <param name="data">The AudioData object.</param>
    /// <param name="channels">The number of audio channels to write.</param>
    /// <param name="sampleRate">The sample rate to write into the header.</param>
    /// <param name="normalize">Whether to normalize the audio before writing.</param>
    /// <param name="indexReport">Action that receives write progress.</param>
    public static void WriteAsWavFile(string location, AudioData<float> data, uint channels, uint sampleRate,
        bool normalize = false, Action<ulong, ulong>? indexReport = null)
    {
        var stream = File.Open(location, FileMode.Create);
        WriteAsWavFile(stream, data, channels, sampleRate, normalize, indexReport);
    }

    public static void WriteAsWavFile(Stream stream, AudioData<float> data, uint channels, uint sampleRate,
        bool normalize = false, Action<ulong, ulong>? indexReport = null)
    {
        indexReport ??= (_, _) => { };

        if (normalize)
            data.Normalize();

        var samples = data.Samples;
        for (var i = 0; i < samples.Length; i++)
        {
            var arr = samples[i];
            samples[i] = arr.TrimEnd();
        }

        var writer = new BinaryWriter(stream);
        var maxLength = samples.Max(r => r.Length);
        AddWavHeader<float>(writer, maxLength, channels, sampleRate);

        var every_n_report = maxLength / 200; // 200 calls.
        for (var i = 0; i < maxLength; i++)
        {
            if (i % every_n_report == 0) indexReport((ulong)i, (ulong)maxLength);
            for (var j = 0; j < channels; j++)
                writer.Write(samples[j].Length > i ? samples[j][i] : 0f);
        }

        writer.Flush();
        writer.Close();
    }

    /// <summary>
    ///     This method adds the RIFF WAVE header to an empty file.
    /// </summary>
    /// <param name="writer">An open BinaryWriter</param>
    /// <param name="dataLength">Length of the audio data.</param>
    /// <param name="channels">The number of audio channels.</param>
    /// <param name="sampleRate">The sample rate.</param>
    private static void AddWavHeader<T>(BinaryWriter writer, int dataLength, uint channels, uint sampleRate)
        where T : struct
    {
        ReadOnlySpan<char> riff_header = ['R', 'I', 'F', 'F'];
        ReadOnlySpan<char> wave_header = ['W', 'A', 'V', 'E'];
        ReadOnlySpan<char> fmt_header = ['f', 'm', 't', ' '];
        ReadOnlySpan<char> data_header = ['d', 'a', 't', 'a'];

        var is_float = typeof(T) == typeof(float) || typeof(T) == typeof(double);
        var byte_size = Marshal.SizeOf<T>();
        var length = dataLength * (int)channels;
        writer.Write(riff_header); // RIFF Chunk Descriptor
        writer.Write(4 + 8 + 16 + 8 + length * 2); // Sub Chunk 1 Size
        //Chunk Size 4 bytes.
        writer.Write(wave_header);
        // fmt sub-chunk
        writer.Write(fmt_header);
        writer.Write(16); // Sub Chunk 1 Size
        writer.Write((short)(is_float ? 3 : 1)); // Audio Format 1 = PCM / 3 = Float
        writer.Write((short)channels); // Audio Channels
        writer.Write((int)sampleRate); // Sample Rate
        writer.Write((int)(sampleRate * channels * byte_size /* Bytes */)); // Byte Rate
        writer.Write((short)(channels * byte_size)); // Block Align
        writer.Write((short)(byte_size * 8)); // Bits per Sample
        // data sub-chunk
        writer.Write(data_header);
        writer.Write(length * byte_size); // Sub Chunk 2 Size.
    }
}
