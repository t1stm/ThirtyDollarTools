using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

/// <summary>
/// Position calculator for each event of a Thirty Dollar Sequence.
/// </summary>
public class LayoutHandler
{
    /// <summary>
    /// Calculated positions.
    /// </summary>
    private readonly float[] calculated_positions;
    
    /// <summary>
    /// The wanted box size that the layout is calculated in mind with.
    /// </summary>
    public readonly float Size;

    /// <summary>
    /// The vertical gap between two boxes.
    /// </summary>
    public readonly float VerticalMargin;
    
    /// <summary>
    /// The horizontal gap between two boxes.
    /// </summary>
    public readonly float HorizontalMargin;
    
    /// <summary>
    /// Current line Y
    /// </summary>
    private float Y;
    
    /// <summary>
    /// Contains the gap for each side of a box.
    /// </summary>
    private GapBox? Margin;
    
    /// <summary>
    /// Contains the inner gap for each side of the playfield.
    /// </summary>
    private GapBox? Padding;

    /// <summary>
    /// The width of the playfield.
    /// </summary>
    public readonly float Width;
    
    /// <summary>
    /// The height of the playfield.
    /// </summary>
    public float Height;
    
    /// <summary>
    /// The current object for this line.
    /// </summary>
    public int CurrentSoundIndex;
    
    /// <summary>
    /// How many objects are contained in a single line.
    /// </summary>
    public int SoundsCount => calculated_positions.Length;
    
    private static float[] GeneratePositions(int sound_count, float size, GapBox? margin, GapBox? padding)
    {
        var array = new float[sound_count];

        var padding_left = padding?.X1 ?? 0f;

        var margin_left = margin?.X1 ?? 0f;
        var margin_right = margin?.X2 ?? 0f;

        var margin_sum = margin_left + margin_right;
        
        var x = padding_left;
        
        for (var i = 0; i < sound_count; i++)
        {
            array[i] = x;
            x += size + margin_sum;
        }
        
        return array;
    }

    /// <summary>
    /// Creates a LayoutHandler with the given parameters.
    /// </summary>
    /// <param name="size">How big a single sound box is.</param>
    /// <param name="sounds_on_single_line">How many sounds are on a single line.</param>
    /// <param name="margin">The gap for each side of a sound box.</param>
    /// <param name="padding">The inner gap for each side of the playfield.</param>
    public LayoutHandler(float size, int sounds_on_single_line, GapBox? margin = null, GapBox? padding = null)
    {
        calculated_positions = GeneratePositions(sounds_on_single_line, size, margin, padding);
        Size = size;
        VerticalMargin = margin?.Sum_Y() ?? 0;
        HorizontalMargin = margin?.Sum_X() ?? 0;
        Padding = padding;
        Margin = margin;
        
        Width = calculated_positions.LastOrDefault(0f) + size + padding?.X2 ?? 0;
        Y = padding?.Y1 ?? 0;
    }

    /// <summary>
    /// Resets the layout handler to the start.
    /// </summary>
    public void Reset()
    {
        CurrentSoundIndex = 0;
        Y = Padding?.Y1 ?? 0;
    }

    /// <summary>
    /// Breaks the current line and starts a new one.
    /// </summary>
    /// <param name="times">How many new lines should be created.</param>
    public void NewLine(int times = 1)
    {
        CurrentSoundIndex = 0;
        Y += (Size + VerticalMargin) * times;
        Height = Y;
    }

    /// <summary>
    /// Gives a position for a sound and calculates the next one.
    /// </summary>
    /// <returns>The current position.</returns>
    public Vector2 GetNewPosition()
    {
        var x = calculated_positions[CurrentSoundIndex];
        var y = Y;
        Vector2 position = (x, y);

        CurrentSoundIndex++;
        if (CurrentSoundIndex >= calculated_positions.Length) NewLine();
        
        return position;
    }

    /// <summary>
    /// Adds the bottom padding to the playfield.
    /// </summary>
    public void Finish()
    {
        Height = Y + Size + (Padding?.Y2 ?? 0);
    }
}

public readonly struct GapBox(float x1, float y1, float x2, float y2)
{
    public readonly float X1 = x1;
    public readonly float Y1 = y1;
    public readonly float X2 = x2;
    public readonly float Y2 = y2;

    /// <summary>
    /// Calculates the sum of X1 and X2.
    /// </summary>
    /// <returns>The summed value.</returns>
    public float Sum_X() => X1 + X2;
    
    /// <summary>
    /// Calculates the sum of Y1 and Y2.
    /// </summary>
    /// <returns>The summed value.</returns>
    public float Sum_Y() => Y1 + Y2;

    public static explicit operator GapBox(float size) => new(size);

    public GapBox(float size) : this(size, size, size, size)
    {
    }

    public GapBox(float x, float y) : this(x, y, x, y)
    {
    }
}