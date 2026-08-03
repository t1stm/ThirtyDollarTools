using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
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
    /// <param name="indexReport">Action object that receives write progress.</param>
    public static void WriteAsWavFloat32File(string location, AudioData<float> data, uint channels, uint sampleRate,
        bool normalize = false, Action<ulong, ulong>? indexReport = null)
    {
        using var stream = File.Open(location, FileMode.Create);
        WriteAsWavFloat32File(stream, data, channels, sampleRate, normalize, indexReport);
    }

    private static int TrimmedLength<T>(T[] arr) where T : INumberBase<T>
    {
        var n = arr.Length;
        while (n > 0 && T.IsZero(arr[n - 1])) n--;
        return n;
    }

    /// <summary>
    ///     Exports an AudioData object as a WAVE stream.
    /// </summary>
    /// <param name="stream">The stream you want to export to.</param>
    /// <param name="data">The AudioData object.</param>
    /// <param name="channels">The number of audio channels to write.</param>
    /// <param name="sampleRate">The sample rate to write into the header.</param>
    /// <param name="normalize">Whether to normalize the audio before writing.</param>
    /// <param name="indexReport">Action object that receives write progress.</param>
    public static void WriteAsWavFloat32File<T>(Stream stream, AudioData<T> data, uint channels, uint sampleRate,
        bool normalize = false, Action<ulong, ulong>? indexReport = null)
        where T : INumber<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfZero(channels);

        if (normalize)
            data.Normalize();

        var source = data.Samples;
        var ch = (int)channels;
        var lengths = ArrayPool<int>.Shared.Rent(source.Length);
        byte[]? buffer = null;

        try
        {
            var maxLength = 0;
            for (var i = 0; i < source.Length; i++) // source.Length, not lengths.Length
            {
                var n = TrimmedLength(source[i]);
                lengths[i] = n;
                if (n > maxLength) maxLength = n;
            }

            var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            AddWavHeader<float>(writer, maxLength, channels, sampleRate);
            writer.Flush();

            var frameBytes = ch * sizeof(float);
            var capacity = Math.Max(1, 64 * 1024 / frameBytes) * frameBytes;
            buffer = ArrayPool<byte>.Shared.Rent(capacity);

            var reportEvery = Math.Max(1, maxLength / 200);
            var nextReport = 0;
            var pos = 0;

            for (var i = 0; i < maxLength; i++)
            {
                if (i == nextReport)
                {
                    indexReport?.Invoke((ulong)i, (ulong)maxLength);
                    nextReport += reportEvery;
                }

                if (pos + frameBytes > capacity) // capacity, not buffer.Length
                {
                    stream.Write(buffer, 0, pos);
                    pos = 0;
                }

                var dest = buffer.AsSpan(pos, frameBytes);
                for (var j = 0; j < ch; j++)
                {
                    var v = 0f;
                    if (j < source.Length && (uint)i < (uint)lengths[j])
                        v = float.CreateSaturating(source[j][i]);
                    BinaryPrimitives.WriteSingleLittleEndian(dest[(j * sizeof(float))..], v);
                }

                pos += frameBytes;
            }

            if (pos > 0)
                stream.Write(buffer, 0, pos);
            stream.Flush();
            indexReport?.Invoke((ulong)maxLength, (ulong)maxLength); // guarantee a terminal 100%
        }
        finally
        {
            ArrayPool<int>.Shared.Return(lengths);
            if (buffer is not null)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// This method adds the RIFF WAVE header to an empty file.
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