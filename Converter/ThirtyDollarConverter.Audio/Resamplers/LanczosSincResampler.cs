namespace ThirtyDollarEncoder.Resamplers;

public class LanczosSincResampler : IResampler
{
    /// <summary>
    /// This is the radius of the Lanczos kernel. Directly related to the quality of the resampling.
    /// </summary>
    public int KernelRadius { get; init; } = 8;
    public int KernelResolution { get; init; } = 0x400;
    public int FixedPointFractionalSize { get; init; } = 1 << 16;
    private const double Pi = Math.PI;

    private int[]? _lanczosKernelTable;
    private readonly Lock _lock = new();

    private double LanczosKernel(double x)
    {
        if (x == 0.0) return 1.0;
        if (Math.Abs(x) > KernelRadius) return 0.0;

        var xPi = x * Pi;
        var xPiDividedByRadius = xPi / KernelRadius;

        return Math.Sin(xPi) * Math.Sin(xPiDividedByRadius) / (xPi * xPiDividedByRadius);
    }

    private void Precompute(int[] table)
    {
        for (var i = 0; i < table.Length; i++)
        {
            var x = ((double)i / table.Length * 2.0 - 1.0) * KernelRadius;
            table[i] = (int)(LanczosKernel(x) * FixedPointFractionalSize);
        }
    }

    private struct Configuration
    {
        public long StretchedKernelRadius; // 16.16
        public int IntegerStretchedKernelRadius;
        public long StretchedKernelRadiusDelta; // 16.16
        public long KernelStepSize; // 16.16
        public int[] KernelTable;
    }

    private uint CalculateRatio(uint a, uint b)
    {
        if (a == 0 || b == 0) return 0xFFFFFFFF;
        
        long upper = a / FixedPointFractionalSize;
        long middle = a % FixedPointFractionalSize;
        
        middle |= (upper % b) * FixedPointFractionalSize;
        upper /= b;
        
        long lower = (middle % b) * FixedPointFractionalSize;
        middle /= b;
        
        lower /= b;
        
        if (upper != 0 || middle >= FixedPointFractionalSize) return 0xFFFFFFFF;
        
        uint result = (uint)(middle * FixedPointFractionalSize + lower);
        return result == 0 ? 1 : result;
    }

    private bool Configure(out Configuration config, uint inputSampleRate, uint outputSampleRate, uint lowPassFilterSampleRate)
    {
        config = default;
        var actualLowPassSampleRate = Math.Min(inputSampleRate, Math.Min(outputSampleRate, lowPassFilterSampleRate));
        var kernelScale = CalculateRatio(inputSampleRate, actualLowPassSampleRate);
        var inverseKernelScale = CalculateRatio(actualLowPassSampleRate, inputSampleRate);

        if (kernelScale >= 0x1000 * (long)FixedPointFractionalSize) return false;

        lock (_lock)
        {
            if (_lanczosKernelTable == null)
            {
                var table = new int[KernelRadius * 2 * KernelResolution];
                Precompute(table);
                _lanczosKernelTable = table;
            }
        }

        config.KernelTable = _lanczosKernelTable;
        config.StretchedKernelRadius = KernelRadius * (long)kernelScale;
        config.IntegerStretchedKernelRadius = (int)((config.StretchedKernelRadius + FixedPointFractionalSize - 1) / FixedPointFractionalSize);
        config.StretchedKernelRadiusDelta = (long)config.IntegerStretchedKernelRadius * FixedPointFractionalSize - config.StretchedKernelRadius;
        config.KernelStepSize = (long)KernelResolution * inverseKernelScale / FixedPointFractionalSize;

        return true;
    }

    public float[] Resample(Memory<float> samples, uint sampleRate, uint targetSampleRate)
    {
        if (!Configure(out var config, sampleRate, targetSampleRate, 44100))
        {
            throw new InvalidOperationException("Failed to configure resampler");
        }

        var input = samples.Span;
        var increment = CalculateRatio(sampleRate, targetSampleRate);
        var durationSecs = (float)samples.Length / sampleRate;
        var outputSize = (int)(durationSecs * targetSampleRate);
        var output = new float[outputSize];

        long positionFractional = 0;
        int positionInteger = 0;

        for (var i = 0; i < outputSize; i++)
        {
            output[i] = ResampleSample(input, positionInteger, positionFractional, config);

            positionFractional += increment;
            positionInteger += (int)(positionFractional / FixedPointFractionalSize);
            positionFractional %= FixedPointFractionalSize;
        }

        return output;
    }

    private float ResampleSample(ReadOnlySpan<float> input, int posInt, long posFrac, Configuration config)
    {
        long minRelative = (posFrac + config.StretchedKernelRadiusDelta + FixedPointFractionalSize - 1) / FixedPointFractionalSize;
        long maxRelative = (posFrac + config.StretchedKernelRadius) / FixedPointFractionalSize;

        int min = posInt + (int)minRelative;
        int max = posInt + config.IntegerStretchedKernelRadius + (int)maxRelative;

        long kernelStart = config.KernelStepSize * (minRelative * FixedPointFractionalSize - posFrac) / FixedPointFractionalSize;

        double sampleAccumulator = 0;
        long normaliser = 0;

        for (int sampleIdx = min, kernelIdx = (int)kernelStart; sampleIdx < max; sampleIdx++, kernelIdx += (int)config.KernelStepSize)
        {
            if (sampleIdx < 0 || sampleIdx >= input.Length) continue;
            if (kernelIdx < 0 || kernelIdx >= config.KernelTable.Length) continue;

            int kernelValue = config.KernelTable[kernelIdx];
            normaliser += kernelValue;
            sampleAccumulator += (double)input[sampleIdx] * kernelValue / FixedPointFractionalSize;
        }

        if (normaliser == 0) return 0;
        return (float)(sampleAccumulator / ((double)normaliser / FixedPointFractionalSize));
    }

    public double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate)
    {
        if (!Configure(out var config, sampleRate, targetSampleRate, 44100))
        {
            throw new InvalidOperationException("Failed to configure resampler");
        }

        var input = samples.Span;
        var increment = CalculateRatio(sampleRate, targetSampleRate);
        var durationSecs = (double)samples.Length / sampleRate;
        var outputSize = (int)(durationSecs * targetSampleRate);
        var output = new double[outputSize];

        long positionFractional = 0;
        int positionInteger = 0;

        for (var i = 0; i < outputSize; i++)
        {
            output[i] = ResampleSample(input, positionInteger, positionFractional, config);

            positionFractional += increment;
            positionInteger += (int)(positionFractional / FixedPointFractionalSize);
            positionFractional %= FixedPointFractionalSize;
        }

        return output;
    }

    private double ResampleSample(ReadOnlySpan<double> input, int posInt, long posFrac, Configuration config)
    {
        long minRelative = (posFrac + config.StretchedKernelRadiusDelta + FixedPointFractionalSize - 1) / FixedPointFractionalSize;
        long maxRelative = (posFrac + config.StretchedKernelRadius) / FixedPointFractionalSize;

        int min = posInt + (int)minRelative;
        int max = posInt + config.IntegerStretchedKernelRadius + (int)maxRelative;

        long kernelStart = config.KernelStepSize * (minRelative * FixedPointFractionalSize - posFrac) / FixedPointFractionalSize;

        double sampleAccumulator = 0;
        long normaliser = 0;

        for (int sampleIdx = min, kernelIdx = (int)kernelStart; sampleIdx < max; sampleIdx++, kernelIdx += (int)config.KernelStepSize)
        {
            if (sampleIdx < 0 || sampleIdx >= input.Length) continue;
            if (kernelIdx < 0 || kernelIdx >= config.KernelTable.Length) continue;

            int kernelValue = config.KernelTable[kernelIdx];
            normaliser += kernelValue;
            sampleAccumulator += input[sampleIdx] * kernelValue / FixedPointFractionalSize;
        }

        if (normaliser == 0) return 0;
        return sampleAccumulator / ((double)normaliser / FixedPointFractionalSize);
    }
}