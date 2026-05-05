namespace ThirtyDollarEncoder.Resamplers;

public class HannSincResampler : IResampler
{
    private readonly int _precision;
    private readonly int _filterSize;
    private readonly double[] _table;
    private readonly double[] _delta;

    public HannSincResampler(int filterSize = 64, int precision = 512)
    {
        _filterSize = filterSize;
        _precision = precision;
        _table = BuildTable(filterSize, precision);
        _delta = BuildDelta(_table);
    }

    private static double[] BuildTable(int filterSize, int precision)
    {
        int n = (filterSize + 1) * precision;
        var table = new double[n + 1];
        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / precision;
            table[i] = Sinc(t) * HannWindow(t / filterSize);
        }
        return table;
    }

    private static double[] BuildDelta(double[] table)
    {
        var delta = new double[table.Length];
        for (int i = 0; i < table.Length - 1; i++)
            delta[i] = table[i + 1] - table[i];
        delta[^1] = 0.0;
        return delta;
    }

    public string Name => "Bandlimited Sinc with Hann window";

    public float[] Resample(Memory<float> samples, uint sampleRate, uint targetSampleRate)
    {
        var resample_ratio = (double)targetSampleRate / sampleRate;
        var samples_length = (int)(samples.Length * resample_ratio);
        var output = new float[samples_length];

        for (var i = 0; i < samples_length; i++)
        {
            var sample_position = i / resample_ratio;
            var sample_index = (int)Math.Floor(sample_position);

            var result = 0.0f;

            for (var j = sample_index - _filterSize; j <= sample_index + _filterSize; j++)
            {
                if (j < 0 || j >= samples.Length) continue;

                var t = Math.Abs(sample_position - j);
                var idx = (int)(t * _precision);
                var eta = t * _precision - idx;
                var window = samples.Span[j] * (_table[idx] + eta * _delta[idx]);
                result += (float)window;
            }

            output[i] = result;
        }

        return output;
    }

    public double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate)
    {
        var resample_ratio = (double)targetSampleRate / sampleRate;

        var samples_length = (int)(samples.Length * resample_ratio);
        var output = new double[samples_length];

        for (var i = 0; i < samples_length; i++)
        {
            var sample_position = i / resample_ratio;
            var sample_index = (int)Math.Floor(sample_position);

            var result = 0.0d;

            for (var j = sample_index - _filterSize; j <= sample_index + _filterSize; j++)
            {
                if (j < 0 || j >= samples.Length) continue;

                var t = Math.Abs(sample_position - j);
                var idx = (int)(t * _precision);
                var eta = t * _precision - idx;
                result += samples.Span[j] * (_table[idx] + eta * _delta[idx]);
            }

            output[i] = result;
        }

        return output;
    }

    private static double Sinc(double x)
    {
        if (x == 0.0)
            return 1.0;

        x *= Math.PI;
        return Math.Sin(x) / x;
    }

    private static double HannWindow(double x)
    {
        return 0.5 * (1.0 + Math.Cos(2.0 * Math.PI * x));
    }
}
