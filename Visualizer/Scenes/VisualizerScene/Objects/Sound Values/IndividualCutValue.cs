using System.Buffers;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Text;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using VisualizerScene.Objects.Playfield.Batch.Chunks;

namespace VisualizerScene.Objects.Sound_Values;

public class IndividualCutValue : ISoundValue
{
    /// <summary>A cut icon's side, as a fraction of the event box it hangs off.</summary>
    private const float SizeRatio = 0.3f;

    /// <summary>Icons per line, as on the site.</summary>
    private const int PerLine = 4;

    /// <summary>Gap between two icons, as a fraction of one - the same air the boxes get.</summary>
    private const float Advance = 1.2f;

    private readonly float _baseSize;
    private readonly SoundRenderable[] _renderables;

    /// <param name="boxSize">The event box these hang off; the icons are sized from it.</param>
    public IndividualCutValue(IndividualCutEvent ice, RenderableFactory factory, float renderScale, float boxSize)
    {
        // render scale is baked in, since ScaleMultiplier belongs to the expand animation and gets reset to 1
        _baseSize = boxSize * SizeRatio * CountScale(ice.CutSounds.Count) * renderScale;
        _renderables =
        [
            .. ice.CutSounds
                .Select(s => factory.CookUp(new NormalEvent { SoundEvent = s }))
        ];

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
        var fontSize = _baseSize * ScaleMultiplier;
        var positioningProvider = new FlexLinePositioningProvider<SoundRenderable>
        {
            BasePosition = realPosition,
            FontSize = fontSize,
            LineHeight = Advance,
            RelativeSize = 1f
        };

        var layouts = ArrayPool<FlexLineItemPlacementLayout>.Shared.Rent(_renderables.Length);
        for (var i = 0; i < _renderables.Length; i++)
        {
            var renderable = _renderables[i];
            layouts[i] = new FlexLineItemPlacementLayout
            {
                Advance = Advance,
                Scale = new Vector2(1, renderable.Scale.X / renderable.Scale.Y),
                Translate = Vector2.Zero,
                // Before this icon, not after it: the provider consumes NewLines as it places
                // the item, so breaking on the last of a line would only fit PerLine - 1.
                NewLines = i > 0 && i % PerLine == 0 ? 1 : 0
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

    /// <summary>
    ///     A long cut list is drawn smaller so it still fits under its box: four icons lose a
    ///     quarter of the size, seven lose half.
    /// </summary>
    private static float CountScale(int count)
    {
        return count switch
        {
            >= 7 => 0.5f,
            >= 4 => 0.75f,
            _ => 1f
        };
    }

    public void Reset()
    {
        Translation = Vector3.Zero;
        ScaleMultiplier = 1f;
        UpdatePosition();
    }
}