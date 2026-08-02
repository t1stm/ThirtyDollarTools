using OpenTK.Mathematics;
using Shared.Helpers.Decoders;
using Sundex.Engine.Renderer.Abstract.Extensions;
using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Enums;
using Sundex.Engine.Text;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace VisualizerScene.Objects.Sound_Values;

public class BackgroundEventValue : ISoundValue
{
    private const float Gap = 5.0f;
    private readonly float _baseFontSize;
    private readonly float _baseGap;
    private readonly TrackedBufferReference<BackgroundBlip> _blip;
    private readonly TextSlice _value;

    public BackgroundEventValue(double value, RenderableFactory factory, TextBuffer buffer, float baseFontSize,
        float renderScale)
    {
        // render scale is baked in, since ScaleMultiplier belongs to the expand animation and gets reset to 1
        _baseFontSize = baseFontSize * renderScale;
        _baseGap = Gap * renderScale;

        var (parsedColor, seconds) = BackgroundParser.ParseFromDouble(value);
        _blip = factory.NewBackgroundBlip(parsedColor);
        _value = buffer.GetTextSlice(seconds.ToString("0.##"));
        _value.UpdateManually = true;

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
        const float blipScaleMultiplier = 0.9f;
        var realPosition = Position + Translation;
        var fontSize = _baseFontSize * ScaleMultiplier;
        var gap = _baseGap * ScaleMultiplier;

        _value.FontSize = fontSize;
        _value.UpdateCharacters();

        var blipSize = fontSize * blipScaleMultiplier;
        var textWidth = _value.Scale.X;
        var textHeight = _value.Scale.Y;

        var totalWidth = blipSize + gap + textWidth;
        var totalHeight = MathF.Max(blipSize, textHeight);

        var startPos = realPosition;
        if (PositionAlign.HasFlag(PositionAlign.CenterX)) startPos.X -= totalWidth / 2f;
        if (PositionAlign.HasFlag(PositionAlign.CenterY)) startPos.Y -= totalHeight / 2f;
        if (PositionAlign.HasFlag(PositionAlign.Bottom)) startPos.Y -= totalHeight;
        if (PositionAlign.HasFlag(PositionAlign.Right)) startPos.X -= totalWidth;

        // Position Blip
        var blipYOffset = blipSize / 4f;
        var blipModel = Matrix4.CreateScale(blipSize, blipSize, 1f) *
                        Matrix4.CreateTranslation(startPos.X, startPos.Y + blipYOffset, startPos.Z);
        _blip.Value = _blip.Value with { Model = blipModel };

        // Position Text
        /*var textYOffset = (totalHeight - textHeight) / 2f;*/
        var textPos = new Vector3(startPos.X + blipSize + gap, startPos.Y /*+ textYOffset*/, startPos.Z);
        _value.SetPosition(textPos);

        Scale = new Vector3(totalWidth, totalHeight, 1);
        _value.UpdateCharacters();
    }

    public void Reset()
    {
        Translation = Vector3.Zero;
        ScaleMultiplier = 1f;
        UpdatePosition();
    }
}