using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Encoder.Wave;
using Text = System.Text.Encoding;

namespace ThirtyDollarConverter.Tests;

/// <summary>
///     Reading real-world WAVE files. The shape that matters here is WAVE_FORMAT_EXTENSIBLE
///     (tag 0xFFFE): what a DAW or a stem splitter writes for anything past 16-bit stereo, and
///     what the editor's wave reference tracks are pointed at. Its "fmt " chunk carries 22
///     extra bytes after the standard 16, and a reader that stops early lands in the middle of
///     them and never finds the data chunk.
/// </summary>
public class WaveDecoderTests
{
    private const int SampleRate = 44100;
    private const int Frames = 500;

    private static void Chunk(BinaryWriter writer, string id, byte[] body)
    {
        writer.Write(Text.ASCII.GetBytes(id));
        writer.Write(body.Length);
        writer.Write(body);
        if (body.Length % 2 == 1) writer.Write((byte)0);
    }

    /// <summary>
    ///     A 24-bit stereo extensible file, with a LIST chunk between "fmt " and "data" the way
    ///     a tagged export has. Every sample is the frame's index, so the decode can be checked
    ///     value by value rather than only by length.
    /// </summary>
    private static byte[] Extensible24Bit()
    {
        var fmt = new MemoryStream();
        var f = new BinaryWriter(fmt);
        f.Write((ushort)0xFFFE); // WAVE_FORMAT_EXTENSIBLE
        f.Write((ushort)2); // channels
        f.Write(SampleRate);
        f.Write(SampleRate * 6); // average bytes a second
        f.Write((ushort)6); // block align
        f.Write((ushort)24); // bits a sample
        f.Write((ushort)22); // cbSize - the extension below
        f.Write((ushort)24); // valid bits a sample
        f.Write(3); // channel mask: front left + front right
        f.Write(new byte[16]); // SubFormat GUID (KSDATAFORMAT_SUBTYPE_PCM in a real file)

        var data = new MemoryStream();
        var d = new BinaryWriter(data);
        for (var frame = 0; frame < Frames; frame++)
        for (var channel = 0; channel < 2; channel++)
        {
            var sample = frame * (channel == 0 ? 1 : -1);
            d.Write((byte)(sample & 0xFF));
            d.Write((byte)((sample >> 8) & 0xFF));
            d.Write((byte)((sample >> 16) & 0xFF));
        }

        var file = new MemoryStream();
        var w = new BinaryWriter(file);
        w.Write(Text.ASCII.GetBytes("RIFF"));
        var sizePosition = file.Position;
        w.Write(0); // patched below
        w.Write(Text.ASCII.GetBytes("WAVE"));
        Chunk(w, "fmt ", fmt.ToArray());
        Chunk(w, "LIST", Text.ASCII.GetBytes("INFOISFT\0\0\0\0"));
        Chunk(w, "data", data.ToArray());

        var bytes = file.ToArray();
        BitConverter.GetBytes((int)(bytes.Length - 8)).CopyTo(bytes, (int)sizePosition);
        return bytes;
    }

    [Fact]
    public void Extensible24BitFile_DecodesToItsFullLength()
    {
        var holder = new WaveDecoder().Read(new MemoryStream(Extensible24Bit()));

        Assert.Equal(2u, holder.Channels);
        Assert.Equal((uint)SampleRate, holder.SampleRate);
        Assert.Equal(ThirtyDollarConverter.Encoder.PCM.Encoding.Int24, holder.Encoding);

        var audio = holder.ReadAsFloat32Array(true);
        Assert.NotNull(audio);
        Assert.Equal(Frames, audio.GetLength());
        Assert.Equal(2u, audio.ChannelCount);

        // Sample 400 of a 24-bit file is 400 / 2^23 as a float; the second channel is its
        // negative, so a decoder reading the wrong stride or the wrong channel shows up here.
        Assert.Equal(400f / 8388608f, audio.GetChannel(0)[400], 6);
        Assert.Equal(-400f / 8388608f, audio.GetChannel(1)[400], 6);
    }
}
