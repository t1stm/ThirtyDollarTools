using System.Buffers;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Text;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using VisualizerScene.Objects.Playfield.Batch.Chunks;

namespace VisualizerScene.Objects.Sound_Values;

public class IndividualCutValue : ISoundValue
{
    private const float RenderableSize = 24;
    private readonly SoundRenderable[] _renderables;

    public IndividualCutValue(IndividualCutEvent ice, RenderableFactory factory)
    {
        _renderables = ice.CutSounds
            .Select(s => factory.CookUp(new NormalEvent { SoundEvent = s }))
            .ToArray();

        ScaleMultiplier = 1f;
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
    public float ScaleMultiplier { get; set; }

    public void UpdatePosition()
    {
        var realPosition = Position + Translation;
        var fontSize = RenderableSize * ScaleMultiplier;
        var positioningProvider = new FlexLinePositioningProvider<SoundRenderable>
        {
            BasePosition = realPosition,
            FontSize = fontSize,
            LineHeight = 1f,
            RelativeSize = 1f
        };

        var layouts = ArrayPool<FlexLineItemPlacementLayout>.Shared.Rent(_renderables.Length);
        for (var i = 0; i < _renderables.Length; i++)
        {
            var renderable = _renderables[i];
            layouts[i] = new FlexLineItemPlacementLayout
            {
                Advance = 1.0,
                Scale = new Vector2(1, renderable.Scale.X / renderable.Scale.Y),
                Translate = Vector2.Zero,
                NewLines = i % 4 == 3 ? 1 : 0
            };
        }

        var arrayIndexable = (ArrayIndexable<SoundRenderable>)_renderables;
        var size = positioningProvider.UpdatePositions(ref arrayIndexable, layouts, 0, _renderables.Length);
        Scale = new Vector3(size.X, size.Y, 1);

        var startPos = realPosition;
        var scale = Scale;
        if (PositionAlign.HasFlag(PositionAlign.CenterX)) startPos.X -= scale.X / 2;
        if (PositionAlign.HasFlag(PositionAlign.CenterY)) startPos.Y -= scale.Y / 2;
        if (PositionAlign.HasFlag(PositionAlign.Bottom)) startPos.Y -= scale.Y;
        if (PositionAlign.HasFlag(PositionAlign.Right)) startPos.X -= scale.X;

        if (startPos == realPosition)
        {
            foreach (var renderable in _renderables) renderable.UpdateModel(false);
            ArrayPool<FlexLineItemPlacementLayout>.Shared.Return(layouts);
            return;
        }

        positioningProvider.BasePosition = startPos;
        positioningProvider.UpdatePositions(ref arrayIndexable, layouts, 0, _renderables.Length);
        foreach (var renderable in _renderables) renderable.UpdateModel(false);

        ArrayPool<FlexLineItemPlacementLayout>.Shared.Return(layouts);
    }

    public void Reset()
    {
        Translation = Vector3.Zero;
        ScaleMultiplier = 1f;
        UpdatePosition();
    }
}