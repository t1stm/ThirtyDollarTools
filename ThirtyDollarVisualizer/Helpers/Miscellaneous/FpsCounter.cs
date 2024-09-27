namespace ThirtyDollarVisualizer.Helpers.Miscellaneous;

public class FpsCounter(int average_pool = 8)
{
    private readonly double[] updates = new double[average_pool];
    private int current_update;
    private double last_calculated_fps;

    public double GetAverageFPS(double last_fps_tick)
    {
        updates[current_update] = last_fps_tick;
        if (++current_update < average_pool) return last_calculated_fps;

        current_update = 0;
        last_calculated_fps = updates.Average();
        return last_calculated_fps;
    }
}