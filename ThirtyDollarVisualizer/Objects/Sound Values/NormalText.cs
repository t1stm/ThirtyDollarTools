using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;
using ThirtyDollarVisualizer.Engine.Text;

namespace ThirtyDollarVisualizer.Objects.Sound_Values;

public class NormalText : ISoundValue
{
    public NormalText(TextSlice text)
    {
        Text = text;
        Text.UpdateManually = true;
        OriginalTextSize = Text.FontSize;
        Position = Text.Position;
        Scale = Text.Scale;
        UpdatePosition();
    }

    public Vector3 Position
    {
        get;
        set
        {
            field = value;
            UpdatePosition();
        }
    }

    public Vector3 Scale { get; set; }
    
    public PositionAlign PositionAlign { get; set; } = PositionAlign.Top | PositionAlign.Left;
    public Vector3 Translation { get; set; }
    public float ScaleMultiplier { get; set; } = 1;
    public TextSlice Text { get; }
    
    private float OriginalTextSize { get; }
    
    public void UpdatePosition()
    {
        var realPosition = Position + Translation;
        
        Text.FontSize = OriginalTextSize * ScaleMultiplier;
        Text.SetPosition(realPosition, PositionAlign);
        Text.UpdateCharacters();
    }

    public void Reset()
    {
        Translation = Vector3.Zero;
        ScaleMultiplier = 1;
        
        Text.FontSize = OriginalTextSize;
        Text.SetPosition(Position, PositionAlign);
        Text.UpdateCharacters();
    }
}