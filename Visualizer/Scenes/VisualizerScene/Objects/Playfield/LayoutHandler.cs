using OpenTK.Mathematics;

namespace VisualizerScene.Objects.Playfield;

/// <summary>
///     Position calculator for each event of a Thirty Dollar Sequence.
/// </summary>
public class LayoutHandler
{
    /// <summary>
    ///     The wanted box size that the layout is calculated in mind with.
    /// </summary>
    public readonly float Size;

    /// <summary>
    ///     The vertical gap between two boxes.
    /// </summary>
    public readonly float VerticalMargin;

    /// <summary>
    ///     The width of the playfield.
    /// </summary>
    public readonly float Width;

    /// <summary>
    ///     Calculated positions.
    /// </summary>
    private readonly float[] _calculatedPositions;

    /// <summary>
    ///     Contains the inner gap for each side of the playfield.
    /// </summary>
    private readonly GapBox? _padding;

    /// <summary>
    ///     The current object for this line.
    /// </summary>
    public int CurrentSoundIndex;

    /// <summary>
    ///     Contains the gap for each side of a box.
    /// </summary>
    private GapBox? _margin;

    /// <summary>
    ///     Creates a LayoutHandler with the given parameters.
    /// </summary>
    /// <param name="size">The size of a single sound box.</param>
    /// <param name="soundsOnSingleLine">The number of sounds on a single line.</param>
    /// <param name="margin">The gap for each side of a sound box.</param>
    /// <param name="padding">The inner gap for each side of the playfield.</param>
    public LayoutHandler(float size, int soundsOnSingleLine, GapBox? margin = null, GapBox? padding = null)
    {
        _calculatedPositions = GeneratePositions(soundsOnSingleLine, size, margin, padding);
        Size = size;
        VerticalMargin = margin?.Sum_Y() ?? 0;
        _padding = padding;
        _margin = margin;

        Width = _calculatedPositions.LastOrDefault(0f) + size + padding?.X2 ?? 0;
        Y = padding?.Y1 ?? 0;
    }

    /// <summary>
    ///     Current line Y
    /// </summary>
    public float Y { get; private set; }

    /// <summary>
    ///     The height of the playfield.
    /// </summary>
    public float Height { get; private set; }

    private static float[] GeneratePositions(int soundCount, float size, GapBox? margin, GapBox? padding)
    {
        var array = new float[soundCount];

        var padding_left = padding?.X1 ?? 0f;

        var margin_left = margin?.X1 ?? 0f;
        var margin_right = margin?.X2 ?? 0f;

        var margin_sum = margin_left + margin_right;

        var x = padding_left;

        for (var i = 0; i < soundCount; i++)
        {
            array[i] = x;
            x += size + margin_sum;
        }

        return array;
    }

    /// <summary>
    ///     Resets the layout handler to the start.
    /// </summary>
    public void Reset()
    {
        CurrentSoundIndex = 0;
        Y = _padding?.Y1 ?? 0;
        Height = 0;
    }

    /// <summary>
    ///     Jumps to a state a previous walk recorded, so a caller can lay out one slice of a
    ///     sequence without walking everything before it - see
    ///     <see cref="Batch.ChunkGenerator.PositionChunk" />. This is the whole of the
    ///     handler's state: positions along a line are a fixed table, and the only things that
    ///     carry between sounds are which column is next and how far down the lines have got.
    /// </summary>
    public void SeekTo(int soundIndex, float y, float height)
    {
        CurrentSoundIndex = soundIndex;
        Y = y;
        Height = height;
    }

    /// <summary>
    ///     Breaks the current line and starts a new one.
    /// </summary>
    /// <param name="times">How many new lines should be created.</param>
    /// <param name="isFromDivider">Whether to change the height if the object is a divider.</param>
    public void NewLine(int times = 1, bool isFromDivider = false)
    {
        CurrentSoundIndex = 0;
        var vertical_margin = isFromDivider ? 0 : VerticalMargin;
        Y += (Size + vertical_margin) * times;
        Height = Y;
    }

    /// <summary>
    ///     Gives a position for a sound and calculates the next one.
    /// </summary>
    /// <returns>The current position.</returns>
    public Vector2 GetNewPosition(bool isDivider)
    {
        var x = _calculatedPositions[CurrentSoundIndex];
        var y = Y;
        Vector2 position = (x, y);

        CurrentSoundIndex++;
        if (isDivider)
            NewLine(2, isDivider);
        else if (CurrentSoundIndex >= _calculatedPositions.Length) NewLine(1, isDivider);

        return position;
    }

    /// <summary>
    ///     Adds the bottom padding to the playfield.
    /// </summary>
    public void Finish()
    {
        Height = Y + Size + (_padding?.Y2 ?? 0);
    }
}