namespace ThirtyDollarConverter.Encoder.Resamplers;

public abstract class KaiserSincResampler : IResampler
{
    private readonly double[] _delta;
    private readonly double[] _halfWindow;
    private readonly int _precision;

    protected KaiserSincResampler(double beta, double rolloff, int numZeros, int precision)
    {
        _precision = precision;

        var n = precision * numZeros;
        _halfWindow = new double[n + 1];
        _delta = new double[n + 1];

        var i0Beta = BesselI0(beta);

        for (var i = 0; i <= n; i++)
        {
            var t = (double)i / precision;
            var sincVal = rolloff * Sinc(rolloff * t);
            var m = (double)i / n;
            var kaiserVal = BesselI0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - m * m))) / i0Beta;
            _halfWindow[i] = sincVal * kaiserVal;
        }

        for (var i = 0; i < n; i++)
            _delta[i] = _halfWindow[i + 1] - _halfWindow[i];
        _delta[n] = 0.0;
    }

    public string Name => "Bandlimited Sinc with Kaiser window";

    public float[] Resample(Memory<float> samples, uint sampleRate, uint targetSampleRate)
    {
        var sampleRatio = (double)targetSampleRate / sampleRate;
        var outputLength = (int)(samples.Length * sampleRatio);
        var output = new float[outputLength];

        var scale = Math.Min(1.0, sampleRatio);
        var indexStep = Math.Max(1, (int)(scale * _precision));

        var span = samples.Span;
        var samplesLen = samples.Length;

        for (var t = 0; t < outputLength; t++)
        {
            var timeRegister = t / sampleRatio;
            var nSample = (int)Math.Floor(timeRegister);

            var fracLeft = scale * (timeRegister - nSample);
            var offsetLeft = (int)(fracLeft * _precision);
            var etaLeft = fracLeft * _precision - offsetLeft;

            var y = 0.0;

            var iMax = Math.Min(nSample + 1, (_halfWindow.Length - offsetLeft) / indexStep);
            for (var i = 0; i < iMax; i++)
            {
                var idx = offsetLeft + i * indexStep;
                var weight = (_halfWindow[idx] + etaLeft * _delta[idx]) * scale;
                y += weight * span[nSample - i];
            }

            var fracRight = scale - fracLeft;
            var offsetRight = (int)(fracRight * _precision);
            var etaRight = fracRight * _precision - offsetRight;

            var kMax = Math.Min(samplesLen - nSample - 1, (_halfWindow.Length - offsetRight) / indexStep);
            for (var k = 0; k < kMax; k++)
            {
                var idx = offsetRight + k * indexStep;
                var weight = (_halfWindow[idx] + etaRight * _delta[idx]) * scale;
                y += weight * span[nSample + k + 1];
            }

            output[t] = (float)y;
        }

        return output;
    }

    public double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate)
    {
        var sampleRatio = (double)targetSampleRate / sampleRate;
        var outputLength = (int)(samples.Length * sampleRatio);
        var output = new double[outputLength];

        var scale = Math.Min(1.0, sampleRatio);
        var indexStep = Math.Max(1, (int)(scale * _precision));

        var span = samples.Span;
        var samplesLen = samples.Length;

        for (var t = 0; t < outputLength; t++)
        {
            var timeRegister = t / sampleRatio;
            var nSample = (int)Math.Floor(timeRegister);

            var fracLeft = scale * (timeRegister - nSample);
            var offsetLeft = (int)(fracLeft * _precision);
            var etaLeft = fracLeft * _precision - offsetLeft;

            var y = 0.0;

            var iMax = Math.Min(nSample + 1, (_halfWindow.Length - offsetLeft) / indexStep);
            for (var i = 0; i < iMax; i++)
            {
                var idx = offsetLeft + i * indexStep;
                var weight = (_halfWindow[idx] + etaLeft * _delta[idx]) * scale;
                y += weight * span[nSample - i];
            }

            var fracRight = scale - fracLeft;
            var offsetRight = (int)(fracRight * _precision);
            var etaRight = fracRight * _precision - offsetRight;

            var kMax = Math.Min(samplesLen - nSample - 1, (_halfWindow.Length - offsetRight) / indexStep);
            for (var k = 0; k < kMax; k++)
            {
                var idx = offsetRight + k * indexStep;
                var weight = (_halfWindow[idx] + etaRight * _delta[idx]) * scale;
                y += weight * span[nSample + k + 1];
            }

            output[t] = y;
        }

        return output;
    }

    private static double Sinc(double x)
    {
        if (x == 0.0) return 1.0;
        x *= Math.PI;
        return Math.Sin(x) / x;
    }

    private static double BesselI0(double x)
    {
        var sum = 1.0;
        var term = 1.0;
        var halfX = x / 2.0;
        for (var k = 1; k <= 50; k++)
        {
            var ratio = halfX / k;
            term *= ratio * ratio;
            sum += term;
            if (term < 1e-15 * sum) break;
        }

        return sum;
    }
}