namespace ThirtyDollarVisualizer.Helpers.Miscellaneous;

public class FpsCounter(int averagePool = 8)
{
    private readonly double[] _updates = new double[averagePool];
    private int _currentUpdate;
    private double _lastCalculatedFps;

    public double GetAverageFPS(double lastFPSTick)
    {
        _updates[_currentUpdate] = lastFPSTick;
        if (++_currentUpdate < averagePool) return _lastCalculatedFps;

        _currentUpdate = 0;
        _lastCalculatedFps = _updates.Average();
        return _lastCalculatedFps;
    }
}