using System;
using System.Linq;

namespace ThirtyDollarConverter.Objects;

public static class ObjectExtensions
{
    /// <summary>
    /// Normalizes the volume in an audio data array.
    /// </summary>
    /// <param name="arr">The target array.</param>
    public static void NormalizeVolume(this float[] arr)
    {
        var max = arr.Length < 1 ? 0 : arr.Max(Math.Abs);
        for (var index = 0UL; index < (ulong)arr.LongLength; index++) arr[index] *= 1 / (float)(max + 0.02);
    }

    /// <summary>
    /// Trims empty samples in the end of an audio data array.
    /// </summary>
    /// <param name="arr">The target array.</param>
    /// <returns>The trimmed array.</returns>
    public static float[] TrimEnd(this float[] arr)
    {
        var length = arr.LongLength - 1;
        for (var i = arr.LongLength - 1; i > 0; i--)
        {
            if (arr[i] != 0f) break;
            length--;
        }

        var destination = new float[length];
        Array.Copy(arr, destination, length);
        return destination;
    }
}