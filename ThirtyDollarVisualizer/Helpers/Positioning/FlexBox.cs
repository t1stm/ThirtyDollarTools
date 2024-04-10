using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Helpers.Positioning;

public class FlexBox
{
    private readonly float MarginPixels;
    private readonly Vector2i Position;
    private readonly Vector2i Size;

    private float CurrentX;
    private float CurrentY;
    private float MaxHeight;

    public FlexBox(Vector2i position, Vector2i size, float margin_pixels)
    {
        Position = position;
        Size = size;
        CurrentX = CurrentY = MarginPixels = margin_pixels;

        CurrentX += X;
        CurrentY += Y;
    }

    private int X => Position.X;
    private int Y => Position.Y;
    private int Width => Size.X;

    public Vector3 AddBox(Vector2i size)
    {
        if (CurrentX + size.X + MarginPixels > Width + X + 1)
        {
            CurrentX = X + MarginPixels;
            CurrentY += MaxHeight + MarginPixels;
        }

        if (size.Y > MaxHeight) MaxHeight = size.Y;

        var vector = new Vector3(CurrentX, CurrentY, 0);

        CurrentX += size.X + MarginPixels;
        return vector;
    }

    public void NewLine()
    {
        CurrentX = X + MarginPixels;
        CurrentY += MaxHeight + MarginPixels;
    }
}