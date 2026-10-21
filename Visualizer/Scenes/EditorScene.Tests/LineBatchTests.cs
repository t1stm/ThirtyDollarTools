using EditorScene.Scenes.Components;
using OpenTK.Mathematics;

namespace EditorScene.Tests;

/// <summary>
///     The batch's shape channel: a rect is flat unless its writer asks for a shape, so every
///     call site that predates the shaded instance keeps drawing exactly what it drew.
/// </summary>
public class LineBatchTests
{
    private static LineBatch NewBatch()
    {
        _ = new EditorTestContext(); // preloads the batch's shader and delete queue
        return new LineBatch { Count = 4 };
    }

    [Fact]
    public void EverySlotWrittenThroughTheFlatApi_HasAZeroShape()
    {
        var batch = NewBatch();
        var shape = ShadedRect.Of(3, strength: 1);
        for (var i = 0; i < 3; i++) batch.Set(i, 0, 0, 10, 10, Vector4.One, shape);

        // Same geometry and color as before - only the shape differs, so it must still land.
        batch.Set(0, 0, 0, 10, 10, Vector4.One);
        batch.SetLine(1, 0, 0, 10, 10, 1, Vector4.One);
        batch.Hide(2);

        for (var i = 0; i < 3; i++) Assert.Equal(Vector4.Zero, batch.ShapeOf(i));
    }

    [Fact]
    public void AShapedSlot_KeepsItsShape_AndItsRect()
    {
        var batch = NewBatch();
        var shape = ShadedRect.Of(2, ShadedRect.Top, 1, 0.5f, ShadedRect.Light.ClipStrip);
        batch.Set(5, 12, 34, 56, 78, Vector4.One, shape); // past the reservation: grows

        Assert.Equal(shape, batch.ShapeOf(5));
        Assert.Equal(new Vector4(12, 34, 56, 78), batch.RectOf(5));
        // The light's kind rides above the corner mask, which the shader reads back as is.
        Assert.Equal(ShadedRect.Top | 3 << 4, (int)shape.Y);
    }
}
