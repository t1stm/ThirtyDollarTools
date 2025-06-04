using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects.Playfield;

/// <summary>
///     Position calculator for each event of a Thirty Dollar Sequence.
/// </summary>
public class LayoutHandler
{
    /// <summary>
    ///     Calculated positions.
    /// </summary>
    private readonly float[] _calculatedPositions;

    /// <summary>
    ///     Contains the inner gap for each side of the playfield.
    /// </summary>
    private readonly GapBox? _padding;

    /// <summary>
    ///     The horizontal gap between two boxes.
    /// </summary>
    public readonly float HorizontalMargin;

    /// <summary>
    ///     The wanted box size that the layout is calculated in mind with.
    /// </summary>
    public readonly float Size;

    /// <summary>
    ///     The vertical gap between two boxes.
    /// </summary>
    public readonly float VerticalMargin;

    /// <summary>
    ///     The width of the playfield.
    /// </summary>
    public readonly float Width;

    /// <summary>
    ///     Contains the gap for each side of a box.
    /// </summary>
    private GapBox? _margin;

    /// <summary>
    ///     Current line Y
    /// </summary>
    private float _y;

    /// <summary>
    ///     The current object for this line.
    /// </summary>
    public int CurrentSoundIndex;

    /// <summary>
    ///     The height of the playfield.
    /// </summary>
    public float Height;

    /// <summary>
    ///     Creates a LayoutHandler with the given parameters.
    /// </summary>
    /// <param name="size">The size of a single sound box.</param>
    /// <param name="soundsOnSingleLine">The number of sounds on a single line.</param>
    /// <param name="margin">The gap for each side of a sound box.</param>
    /// <param name="padding">The inner gap for each side of the playfield.</param>
    public LayoutHandler(float size, int soundsOnSingleLine, GapBox? margin = null, GapBox? padding = null)
    {
        _calculatedPositions = GeneratePositions(soundsOnSingleLine, size, margin, padding);
        Size = size;
        VerticalMargin = margin?.Sum_Y() ?? 0;
        HorizontalMargin = margin?.Sum_X() ?? 0;
        _padding = padding;
        _margin = margin;

        Width = _calculatedPositions.LastOrDefault(0f) + size + padding?.X2 ?? 0;
        _y = padding?.Y1 ?? 0;
    }

    /// <summary>
    ///     How many objects are contained in a single line.
    /// </summary>
    public int SoundsCount => _calculatedPositions.Length;

    private static float[] GeneratePositions(int soundCount, float size, GapBox? margin, GapBox? padding)
    {
        var array = new float[soundCount];

        var padding_left = padding?.X1 ?? 0f;

        var margin_left = margin?.X1 ?? 0f;
        var margin_right = margin?.X2 ?? 0f;

        var margin_sum = margin_left + margin_right;

        var x = padding_left;

        for (var i = 0; i < soundCount; i++)
        {
            array[i] = x;
            x += size + margin_sum;
        }

        return array;
    }

    /// <summary>
    ///     Resets the layout handler to the start.
    /// </summary>
    public void Reset()
    {
        CurrentSoundIndex = 0;
        _y = _padding?.Y1 ?? 0;
    }

    /// <summary>
    ///     Breaks the current line and starts a new one.
    /// </summary>
    /// <param name="times">How many new lines should be created.</param>
    public void NewLine(int times = 1)
    {
        CurrentSoundIndex = 0;
        _y += (Size + VerticalMargin) * times;
        Height = _y;
    }

    /// <summary>
    ///     Gives a position for a sound and calculates the next one.
    /// </summary>
    /// <returns>The current position.</returns>
    public Vector2 GetNewPosition()
    {
        var x = _calculatedPositions[CurrentSoundIndex];
        var y = _y;
        Vector2 position = (x, y);

        CurrentSoundIndex++;
        if (CurrentSoundIndex >= _calculatedPositions.Length) NewLine();

        return position;
    }

    /// <summary>
    ///     Adds the bottom padding to the playfield.
    /// </summary>
    public void Finish()
    {
        Height = _y + Size + (_padding?.Y2 ?? 0);
    }
}

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

    public static explicit operator GapBox(float size)
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