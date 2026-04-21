namespace VisualizerScene.Objects.Playfield;

public readonly struct GapBox(float x1, float y1, float x2, float y2)
{
    public readonly float X1 = x1;
    public readonly float Y1 = y1;
    public readonly float X2 = x2;
    public readonly float Y2 = y2;

    /// <summary>
    ///     Calculates the sum of X1 and X2.
    /// </summary>
    /// <returns>The summed value.</returns>
    public float Sum_X()
    {
        return X1 + X2;
    }

    /// <summary>
    ///     Calculates the sum of Y1 and Y2.
    /// </summary>
    /// <returns>The summed value.</returns>
    public float Sum_Y()
    {
        return Y1 + Y2;
    }

    public static implicit operator GapBox(float size)
    {
        return new GapBox(size);
    }

    public GapBox(float size) : this(size, size, size, size)
    {
    }

    public GapBox(float x, float y) : this(x, y, x, y)
    {
    }
}