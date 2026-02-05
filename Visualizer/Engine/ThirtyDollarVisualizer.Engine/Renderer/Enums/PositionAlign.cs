namespace ThirtyDollarVisualizer.Engine.Renderer.Enums;

[Flags]
public enum PositionAlign
{
    Top = 1 << 1,
    CenterX = 1 << 2,
    Bottom = 1 << 3,
    Left = 1 << 4,
    CenterY = 1 << 5,
    Right = 1 << 6,

    Center = CenterX | CenterY
}