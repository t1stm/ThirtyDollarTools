using System;
using System.Linq;

namespace ThirtyDollarConverter
{
    public static class ObjectExtensions
    {
        public static void NormalizeVolume(this float[] arr)
        {
            var max = arr.Max(Math.Abs);
            for (var index = 0UL; index < (ulong) arr.LongLength; index++)
            {
                arr[index] *= 1 / (float) (max + 0.02);
            }
        }

        public static float[] TrimEnd(this float[] arr)
        {
            var length = arr.LongLength - 1;
            for (var i = arr.LongLength - 1; i > 0; i--)
            {
                if (arr[i] != 0f) break;
                length--;
            }
            var newArr = new float[length];
            for (long i = 0; i < length; i++)
            {
                newArr[i] = arr[i];
            }
            return newArr;
        }
    }
}