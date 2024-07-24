using System.Runtime.InteropServices;
using ThirtyDollarEncoder.PCM;
using Encoding = System.Text.Encoding;

namespace ThirtyDollarEncoder.Wave;

public class WaveDecoder
{
    private readonly PcmDataHolder Holder = new();
    private long dataChunkLength;

    // Shamefully copied from NAudio.
    // Here goes copyright infringement.
    private long riffFileSize;

    private static int HeaderToInt(string header)
    {
        return BitConverter.ToInt32(Encoding.UTF8.GetBytes(header)[..4], 0);
    }

    public PcmDataHolder Read(Stream inputStream)
    {
        var reader = new BinaryReader(inputStream);
        var header = ReadRiffHeader(reader);
        riffFileSize = reader.ReadUInt32();

        if (reader.ReadInt32() != HeaderToInt("WAVE"))
            throw new FileLoadException("Supplied data doesn't have \"WAVE\" header.");
        switch (header)
        {
            case 2:
                ReadDs64StandardChunk(reader);
                break;
            case 0:
                throw new FileLoadException("Supplied data doesn't have \"RIFF\" header.");
        }

        var dataChunkId = HeaderToInt("data");
        var formatChunkId = HeaderToInt("fmt ");
        var stopPosition = Math.Min(riffFileSize + 8, inputStream.Length);
        //long dataChunkPosition = -1;
        while (inputStream.Position <= stopPosition - 8)
        {
            var chunkID = reader.ReadInt32();

            Span<int> testing_span = stackalloc int[] { chunkID };
            var chunk_bytes = MemoryMarshal.AsBytes(testing_span);
            var chunk_name = Encoding.ASCII.GetString(chunk_bytes);

            var chunkLength = reader.ReadUInt32();
            if (chunkID == formatChunkId)
            {
                if (chunkLength > int.MaxValue)
                    throw new InvalidDataException($"Format chunk length must be between 0 and {int.MaxValue}.");
                ReadWaveFormat(reader, (int)chunkLength);
                continue;
            }

            if (chunkID != dataChunkId)
            {
                inputStream.Position += chunkLength;
                continue;
            }

            if (header != 2) dataChunkLength = chunkLength;
            break;
        }

        var bytes = new byte[dataChunkLength];
        var read = reader.Read(bytes);
        reader.Close();
        Holder.AudioData = bytes;
        return Holder;
    }

    private void ReadWaveFormat(BinaryReader reader, int chunkLength)
    {
        if (chunkLength < 16)
            throw new InvalidDataException("Invalid WaveFormat Structure");
        var waveFormatTag = reader.ReadUInt16();
        if (waveFormatTag != 0x0001) Console.WriteLine("File is probably not int PCM.");
        Holder.Channels = (uint)reader.ReadInt16();
        Holder.SampleRate = (uint)reader.ReadInt32();
        var averageBytesPerSecond = reader.ReadInt32();
        var blockAlign = reader.ReadInt16();
        Holder.Encoding = (PCM.Encoding)reader.ReadInt16();
        if (chunkLength <= 16) return;
        var extraSize = reader.ReadInt16();
        if (extraSize == chunkLength - 18) return;
        extraSize = (short)(chunkLength - 18);
        var extraData = new byte[extraSize];
        var read = reader.Read(extraData, 0, extraSize);
    }

    private void ReadDs64StandardChunk(BinaryReader reader)
    {
        if (reader.ReadInt32() != HeaderToInt("ds64"))
            throw new FileLoadException("Supplied data doesn't have \"ds64\" chunk.");
        var chunkSize = reader.ReadInt32();
        riffFileSize = reader.ReadInt64();
        dataChunkLength = reader.ReadInt64();
        var sampleCount = reader.ReadInt64(); // I don't know why this isn't used in NAudio.
        var excess = reader.ReadBytes(chunkSize - 24);
    }

    public int ReadRiffHeader(BinaryReader reader)
    {
        var header = reader.ReadInt32();
        return header == HeaderToInt("RF64") ? 2 : header == HeaderToInt("RIFF") ? 1 : 0;
    }
}