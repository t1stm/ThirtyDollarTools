using System;
using System.Collections.Generic;
using System.IO;

namespace ThirtyDollarEncoder.PCM
{
    public static class DataHolderExtensions
    {
        public static byte[]? ReadAsInt8Array(this PcmDataHolder holder)
        {
            throw new Exception("Why would anyone want to use 8 bit integers for music. I won't implement this for now.");
        }
        
        public static short[]? ReadAsInt16Array(this PcmDataHolder holder, bool monoToStereo)
        {
            lock (holder.LockObject)
            {
                if (holder.FloatData != null) return holder.ShortData;
                if (holder.AudioData == null) throw new NullReferenceException("Holder Audio Data is null.");
                using var reader = new BinaryReader(new MemoryStream(holder.AudioData));
                var shortList = new List<short>();
                switch (holder.Encoding)
                {
                    case Encoding.Float32:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            var val = (short) (reader.ReadSingle() * 32768);
                            shortList.Add(val);
                            if (holder.Channels == 1 && monoToStereo) shortList.Add(val);
                        }

                        holder.ShortData = shortList.ToArray();
                        return holder.ShortData;
                    case Encoding.Int8:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            var val = (short) (reader.ReadByte() * 256);
                            shortList.Add(val);
                            if (holder.Channels == 1 && monoToStereo) shortList.Add(val);
                        }

                        holder.ShortData = shortList.ToArray();
                    
                        return holder.ShortData;
                    case Encoding.Int16:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            var val = reader.ReadInt16();
                            shortList.Add(val);
                            if (holder.Channels == 1 && monoToStereo) shortList.Add(val);
                        }

                        holder.ShortData = shortList.ToArray();
                        return holder.ShortData;
                    case Encoding.Int24:
                        throw new Exception("Working with Int24 isn't supported (yet.)");
                }
                return Array.Empty<short>();
            }
        }
        
        public static int[]? ReadAsInt24Array(this PcmDataHolder holder)
        {
            throw new Exception("Working with Int24 isn't supported (yet.)");
        }
        
        public static float[]? ReadAsFloat32Array(this PcmDataHolder holder, bool monoToStereo)
        {
            lock (holder.LockObject)
            {
                if (holder.FloatData != null) return holder.FloatData;
                if (holder.AudioData == null) throw new NullReferenceException("Holder Audio Data is null.");
                using var reader = new BinaryReader(new MemoryStream(holder.AudioData));
                var floatList = new List<float>();
                switch (holder.Encoding)
                {
                    case Encoding.Float32:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            var val = reader.ReadSingle();
                            val = val > 1 ? 1 : val < -1 ? -1 : val;
                            floatList.Add(val);
                            if (holder.Channels == 1 && monoToStereo) floatList.Add(val);
                        }
                        holder.FloatData = floatList.ToArray();
                        return holder.FloatData;
                    case Encoding.Int8:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            var val = reader.ReadByte() / 256f;
                            val = val > 1 ? 1 : val < -1 ? -1 : val;
                            floatList.Add(val);
                            if (holder.Channels == 1 && monoToStereo) floatList.Add(val);
                        }
                        holder.FloatData = floatList.ToArray();
                        return holder.FloatData;
                    case Encoding.Int16:
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            var val = reader.ReadInt16() / 32768f;
                            val = val > 1 ? 1 : val < -1 ? -1 : val;
                            floatList.Add(val);
                            if (holder.Channels == 1 && monoToStereo) floatList.Add(val);
                        }
                        holder.FloatData = floatList.ToArray();
                        return holder.FloatData;
                    case Encoding.Int24:
                        throw new Exception("Working with Int24 isn't supported (yet.)");
                }
                return Array.Empty<float>();
            }
        }
    }
}