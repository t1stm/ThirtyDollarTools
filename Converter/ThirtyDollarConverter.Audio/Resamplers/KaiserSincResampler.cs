namespace ThirtyDollarEncoder.Resamplers;

public abstract class KaiserSincResampler : IResampler
{
    private readonly double[] _halfWindow;
    private readonly double[] _delta;
    private readonly int _precision;

    protected KaiserSincResampler(double beta, double rolloff, int numZeros, int precision)
    {
        _precision = precision;

        int n = precision * numZeros;
        _halfWindow = new double[n + 1];
        _delta = new double[n + 1];

        double i0Beta = BesselI0(beta);

        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / precision;
            double sincVal = rolloff * Sinc(rolloff * t);
            double m = (double)i / n;
            double kaiserVal = BesselI0(beta * Math.Sqrt(Math.Max(0.0, 1.0 - m * m))) / i0Beta;
            _halfWindow[i] = sincVal * kaiserVal;
        }

        for (int i = 0; i < n; i++)
            _delta[i] = _halfWindow[i + 1] - _halfWindow[i];
        _delta[n] = 0.0;
    }

    public string Name => "Bandlimited Sinc With Kaiser window";

    public float[] Resample(Memory<float> samples, uint sampleRate, uint targetSampleRate)
    {
        double sampleRatio = (double)targetSampleRate / sampleRate;
        int outputLength = (int)(samples.Length * sampleRatio);
        var output = new float[outputLength];

        double scale = Math.Min(1.0, sampleRatio);
        int indexStep = Math.Max(1, (int)(scale * _precision));

        var span = samples.Span;
        int samplesLen = samples.Length;

        for (int t = 0; t < outputLength; t++)
        {
            double timeRegister = t / sampleRatio;
            int nSample = (int)Math.Floor(timeRegister);

            double fracLeft = scale * (timeRegister - nSample);
            int offsetLeft = (int)(fracLeft * _precision);
            double etaLeft = fracLeft * _precision - offsetLeft;

            double y = 0.0;

            int iMax = Math.Min(nSample + 1, (_halfWindow.Length - offsetLeft) / indexStep);
            for (int i = 0; i < iMax; i++)
            {
                int idx = offsetLeft + i * indexStep;
                double weight = (_halfWindow[idx] + etaLeft * _delta[idx]) * scale;
                y += weight * span[nSample - i];
            }

            double fracRight = scale - fracLeft;
            int offsetRight = (int)(fracRight * _precision);
            double etaRight = fracRight * _precision - offsetRight;

            int kMax = Math.Min(samplesLen - nSample - 1, (_halfWindow.Length - offsetRight) / indexStep);
            for (int k = 0; k < kMax; k++)
            {
                int idx = offsetRight + k * indexStep;
                double weight = (_halfWindow[idx] + etaRight * _delta[idx]) * scale;
                y += weight * span[nSample + k + 1];
            }

            output[t] = (float)y;
        }

        return output;
    }

    public double[] Resample(Memory<double> samples, uint sampleRate, uint targetSampleRate)
    {
        double sampleRatio = (double)targetSampleRate / sampleRate;
        int outputLength = (int)(samples.Length * sampleRatio);
        var output = new double[outputLength];

        double scale = Math.Min(1.0, sampleRatio);
        int indexStep = Math.Max(1, (int)(scale * _precision));

        var span = samples.Span;
        int samplesLen = samples.Length;

        for (int t = 0; t < outputLength; t++)
        {
            double timeRegister = t / sampleRatio;
            int nSample = (int)Math.Floor(timeRegister);

            double fracLeft = scale * (timeRegister - nSample);
            int offsetLeft = (int)(fracLeft * _precision);
            double etaLeft = fracLeft * _precision - offsetLeft;

            double y = 0.0;

            int iMax = Math.Min(nSample + 1, (_halfWindow.Length - offsetLeft) / indexStep);
            for (int i = 0; i < iMax; i++)
            {
                int idx = offsetLeft + i * indexStep;
                double weight = (_halfWindow[idx] + etaLeft * _delta[idx]) * scale;
                y += weight * span[nSample - i];
            }

            double fracRight = scale - fracLeft;
            int offsetRight = (int)(fracRight * _precision);
            double etaRight = fracRight * _precision - offsetRight;

            int kMax = Math.Min(samplesLen - nSample - 1, (_halfWindow.Length - offsetRight) / indexStep);
            for (int k = 0; k < kMax; k++)
            {
                int idx = offsetRight + k * indexStep;
                double weight = (_halfWindow[idx] + etaRight * _delta[idx]) * scale;
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
        double sum = 1.0;
        double term = 1.0;
        double halfX = x / 2.0;
        for (int k = 1; k <= 50; k++)
        {
            double ratio = halfX / k;
            term *= ratio * ratio;
            sum += term;
            if (term < 1e-15 * sum) break;
        }
        return sum;
    }
}
