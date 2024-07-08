using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class LayoutHandler
{
    private readonly float[] calculated_positions;
    public readonly float Size;

    public readonly float VerticalMargin;
    public readonly float HorizontalMargin;
    
    private float Y;
    private GapBox? Padding;

    public readonly float Width;
    public float Height;
    public int CurrentSoundIndex;
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

    public LayoutHandler(float size, int sounds_on_single_line, GapBox? margin = null, GapBox? padding = null)
    {
        calculated_positions = GeneratePositions(sounds_on_single_line, size, margin, padding);
        Size = size;
        VerticalMargin = margin?.Sum_Y() ?? 0;
        HorizontalMargin = margin?.Sum_X() ?? 0;
        Padding = padding;
        
        Width = calculated_positions.LastOrDefault(0f) + size + padding?.X2 ?? 0;
        Y = padding?.Y1 ?? 0;
    }

    public void Reset()
    {
        CurrentSoundIndex = 0;
    }

    public void NewLine(int times = 1)
    {
        CurrentSoundIndex = 0;
        Y += (Size + VerticalMargin) * times;
        Height = Y + Padding?.Y2 ?? 0;
    }

    public Vector2 GetNewPosition(Vector2 object_size)
    {
        var x = calculated_positions[CurrentSoundIndex];
        var y = Y;
        Vector2 position = (x, y);

        CurrentSoundIndex++;
        if (CurrentSoundIndex >= calculated_positions.Length) NewLine();
        
        return position;
    }

    public void Finish()
    {
        Height += Size;
    }
}

public readonly struct GapBox(float x1, float y1, float x2, float y2)
{
    public readonly float X1 = x1;
    public readonly float Y1 = y1;
    public readonly float X2 = x2;
    public readonly float Y2 = y2;

    public float Sum_X() => X1 + X2;
    public float Sum_Y() => Y1 + Y2;

    public static explicit operator GapBox(float size) => new(size);

    public GapBox(float size) : this(size, size, size, size)
    {
    }

    public GapBox(float x, float y) : this(x, y, x, y)
    {
    }
}