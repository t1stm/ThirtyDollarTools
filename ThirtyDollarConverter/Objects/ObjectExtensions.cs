using System;
using System.Linq;
using System.Numerics;

namespace ThirtyDollarConverter.Objects;

public static class ObjectExtensions
{
    /// <summary>
    ///     Normalizes the volume in an audio data array.
    /// </summary>
    /// <param name="arr">The target array.</param>
    public static void NormalizeVolume(this float[] arr)
    {
        var max = arr.Length < 1 ? 0 : arr.Max(Math.Abs);
        var chunk_size = Vector<float>.Count;
        var span = arr.AsSpan();

        for (var i = 0; i < arr.Length - arr.Length % chunk_size; i += chunk_size)
        {
            var chunk = span[i..];
            var vector = new Vector<float>(chunk);

            var final = Vector.Multiply(vector, 1f / (max + 0.02f));
            final.CopyTo(chunk);
        }

        for (var i = arr.Length - arr.Length % chunk_size; i < arr.Length; i++) span[i] *= 1f / (max + 0.02f);
    }

    /// <summary>
    ///     Trims empty samples in the end of an audio data array.
    /// </summary>
    /// <param name="arr">The target array.</param>
    /// <returns>The trimmed array.</returns>
    public static float[] TrimEnd(this float[] arr)
    {
        var length = arr.Length - 1;
        for (var i = arr.Length - 1; i > 0; i--)
        {
            if (arr[i] != 0f) break;
            length--;
        }

        Array.Resize(ref arr, length);
        return arr;
    }
}