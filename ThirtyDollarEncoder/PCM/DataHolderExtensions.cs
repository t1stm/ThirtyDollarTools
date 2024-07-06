namespace ThirtyDollarEncoder.PCM;

public static class DataHolderExtensions
{
    public static byte[]? ReadAsInt8Array(this PcmDataHolder holder)
    {
        throw new Exception("Why would anyone want to use 8 bit integers for music. I won't implement this for now.");
    }

    public static AudioData<short>? ReadAsInt16Array(this PcmDataHolder holder, bool monoToStereo)
    {
        lock (holder.LockObject)
        {
            if (holder.ShortData != null) return holder.ShortData;
            if (holder.AudioData == null) return null;
            
            var channel_count = holder.Channels;
            var export_channels = monoToStereo ? channel_count < 2 ? 2 : channel_count : channel_count;
            
            using var reader = new BinaryReader(new MemoryStream(holder.AudioData));
            
            var data_list = new List<short>[export_channels]; // Jesus Christ...
            for (var i = 0; i < data_list.Length; i++) data_list[i] = new List<short>();
            uint channel = 0;
            
            switch (holder.Encoding)
            {
                case Encoding.Float32:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        var val = (short)(reader.ReadSingle() * 32768);
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel + 1].Add(val);
                        channel += 1;
                    }

                    break;
                case Encoding.Int8:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        var val = (short)(reader.ReadByte() * 256);
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel + 1].Add(val);
                        channel += 1;
                    }

                    break;
                case Encoding.Int16:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        var val = reader.ReadInt16();
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel + 1].Add(val);
                        channel += 1;
                    }

                    break;
                case Encoding.Int24:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        var b1 = reader.ReadByte();
                        var b2 = reader.ReadByte();
                        var b3 = reader.ReadByte();
                        var i24 = new Int24
                        {
                            b1 = b1,
                            b2 = b2,
                            b3 = b3
                        };
                        var val = (short)(i24.ToFloat() * 32768);
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel + 1].Add(val);
                        channel += 1;
                    }

                    break;
            }

            var audioData = new AudioData<short>(export_channels);
            for (var i = 0; i < export_channels; i++) audioData.Samples[i] = data_list[i].ToArray();
            holder.ShortData = audioData;
            return audioData;
        }
    }

    public static Int24[]? ReadAsInt24Array(this PcmDataHolder holder)
    {
        throw new Exception("Working with Int24 isn't supported (yet.)");
    }

    public static AudioData<float>? ReadAsFloat32Array(this PcmDataHolder holder, bool monoToStereo)
    {
        lock (holder.LockObject)
        {
            if (holder.FloatData != null) return holder.FloatData;
            if (holder.AudioData == null) return null;
            var channel_count = holder.Channels;
            var export_channels = monoToStereo ? channel_count < 2 ? 2 : channel_count : channel_count;
            using var reader = new BinaryReader(new MemoryStream(holder.AudioData));
            var data_list = new List<float>[export_channels]; // Jesus Christ...
            for (var i = 0; i < data_list.Length; i++) data_list[i] = new List<float>();
            uint channel = 0;
            switch (holder.Encoding)
            {
                case Encoding.Float32:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        
                        var val = reader.ReadSingle();
                        val = val > 1 ? 1 : val < -1 ? -1 : val;
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel].Add(val);
                        channel += 1;
                    }

                    break;
                case Encoding.Int8:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        
                        var val = reader.ReadByte() / 256f;
                        val = val > 1 ? 1 : val < -1 ? -1 : val;
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel + 1].Add(val);
                        channel += 1;
                    }

                    break;
                case Encoding.Int16:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        
                        var val = reader.ReadInt16() / 32768f;
                        val = val > 1 ? 1 : val < -1 ? -1 : val;
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel + 1].Add(val);
                        channel += 1;
                    }

                    break;
                case Encoding.Int24:
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        channel %= channel_count;
                        
                        var b1 = reader.ReadByte();
                        var b2 = reader.ReadByte();
                        var b3 = reader.ReadByte();
                        var i24 = new Int24
                        {
                            b1 = b1,
                            b2 = b2,
                            b3 = b3
                        };
                        var val = i24.ToFloat();
                        data_list[channel].Add(val);
                        if (channel_count == 1 && monoToStereo) data_list[channel + 1].Add(val);
                        channel += 1;
                    }

                    break;
            }

            var audioData = new AudioData<float>(export_channels);
            for (var i = 0; i < export_channels; i++) audioData.Samples[i] = data_list[i].ToArray();
            holder.FloatData = audioData;
            return audioData;
        }
    }
}