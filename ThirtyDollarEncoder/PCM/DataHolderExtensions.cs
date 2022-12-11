#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace ThirtyDollarEncoder.PCM
{
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
                var audioChannels = holder.Channels;
                var exportChannels = monoToStereo ? audioChannels < 2 ? 2 : audioChannels : audioChannels;
                using var reader = new BinaryReader(new MemoryStream(holder.AudioData));
                var typeList = new List<short>[exportChannels]; // Jesus Christ...
                for (var i = 0; i < typeList.Length; i++)
                {
                    typeList[i] = new List<short>();
                }
                var channel = 0;
                switch (holder.Encoding)
                {
                    case Encoding.Float32:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
                            var val =  (short) (reader.ReadSingle() * 32768);
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                    case Encoding.Int8:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
                            var val = (short) (reader.ReadByte() * 256);
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                    case Encoding.Int16:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
                            var val = reader.ReadInt16();
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                    case Encoding.Int24:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
                            var b1 = reader.ReadByte();
                            var b2 = reader.ReadByte();
                            var b3 = reader.ReadByte();
                            var i24 = new Int24
                            {
                                b1 = b1,
                                b2 = b2,
                                b3 = b3
                            };
                            var val = (short) (i24.ToFloat() * 32768);
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                }

                var audioData = new AudioData<short>(exportChannels);
                for (var i = 0; i < exportChannels; i++)
                {
                    audioData.Samples[i] = typeList[i].ToArray();
                }
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
                var audioChannels = holder.Channels;
                var exportChannels = monoToStereo ? audioChannels < 2 ? 2 : audioChannels : audioChannels;
                using var reader = new BinaryReader(new MemoryStream(holder.AudioData));
                var typeList = new List<float>[exportChannels]; // Jesus Christ...
                for (var i = 0; i < typeList.Length; i++)
                {
                    typeList[i] = new List<float>();
                }
                var channel = 0;
                switch (holder.Encoding)
                {
                    case Encoding.Float32:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
                            var val = reader.ReadSingle();
                            val = val > 1 ? 1 : val < -1 ? -1 : val;
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                    case Encoding.Int8:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
                            var val = reader.ReadByte() / 256f;
                            val = val > 1 ? 1 : val < -1 ? -1 : val;
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                    case Encoding.Int16:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
                            var val = reader.ReadInt16() / 32768f;
                            val = val > 1 ? 1 : val < -1 ? -1 : val;
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                    case Encoding.Int24:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            channel = channel + 1 < audioChannels ? channel + 1 : 0;
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
                            typeList[channel].Add(val);
                            if (audioChannels == 1 && monoToStereo) typeList[channel + 1].Add(val);
                        }
                        break;
                }

                var audioData = new AudioData<float>(exportChannels);
                for (var i = 0; i < exportChannels; i++)
                {
                    audioData.Samples[i] = typeList[i].ToArray();
                }
                holder.FloatData = audioData;
                return audioData;
            }
        }
    }
}