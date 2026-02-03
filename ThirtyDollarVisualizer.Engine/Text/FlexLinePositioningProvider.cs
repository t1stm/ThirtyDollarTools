using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Text.Fonts;

namespace ThirtyDollarVisualizer.Engine.Text;

public ref struct FlexLinePositioningProvider<TValue>() where TValue : IPositionable
{
    public float FontSize { get; set; } = 16f;
    public double LineHeight { get; set; } = 1f;
    public double EmSize { get; set; } = 1f;
    public Vector3 BasePosition { get; set; } = Vector3.Zero;
    public float RelativeSize { get; set; } = GlyphProvider.GlyphSize;

    public Vector2 UpdatePositions<TCollection>(ref TCollection items,
        ReadOnlySpan<FlexLineItemPlacementLayout> layouts, int offset, int bufferIndex)
        where TCollection : IIndexableCollection<int, TValue>, allows ref struct
    {
        var cursorX = BasePosition.X;
        var cursorY = BasePosition.Y;

        var minX = cursorX;
        var minY = cursorY;
        var maxX = cursorX;
        var maxY = cursorY;

        for (var index = 0; index < bufferIndex; index++)
        {
            var alignmentData = layouts[index];
            var newLines = alignmentData.NewLines;
            while (newLines > 0)
            {
                cursorX = BasePosition.X;
                cursorY += FontSize * (float)(LineHeight / EmSize);
                newLines--;
            }

            if (offset + index >= items.Count) 
                throw new Exception("TCollection capacity exceeded.");
            
            var item = items[offset + index];
            var fontSize = FontSize;

            var (advanceUnitSpace, 
                (translateX, translateY), 
                (scaleX, scaleY)) = alignmentData;

            var positionX = cursorX - translateX * fontSize;
            var positionY = cursorY + fontSize - (RelativeSize / scaleY - translateY) * fontSize;
            var scaleW = RelativeSize / scaleX * fontSize;
            var scaleH = RelativeSize / scaleY * fontSize;

            item.Position = new Vector3(positionX, positionY, BasePosition.Z);
            item.Scale = new Vector3(scaleW, scaleH, 1);
            
            cursorX += (float)advanceUnitSpace * fontSize;

            maxX = Math.Max(maxX, cursorX);
            maxY = Math.Max(maxY, cursorY + FontSize * (float)(LineHeight / EmSize));
            items[offset + index] = item;
        }

        return new Vector2(maxX - minX, maxY - minY);
    }
}

public struct FlexLineItemPlacementLayout()
{
    public double Advance = 0;
    public Vector2 Translate = Vector2.Zero;
    public Vector2 Scale = Vector2.Zero;
    public int NewLines = 0;

    public void Deconstruct(out double advance, out Vector2 translate, out Vector2 scale)
    {
        advance = Advance;
        translate = Translate;
        scale = Scale;
    }
}