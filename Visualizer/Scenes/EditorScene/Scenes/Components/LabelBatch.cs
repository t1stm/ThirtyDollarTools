using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;

namespace EditorScene.Scenes.Components;

/// <summary>
///     A pool of short strings (beat numbers, gutter values, clip names) sharing one
///     <see cref="TextBuffer" />, so the whole pool is one instanced draw call instead of
///     one buffer - and one draw - per <see cref="Sundex.Components.Labels.Label" />.
///     <see cref="LineBatch" />'s counterpart for text, and like it the count is a
///     reservation rather than a cap: writing past the end appends slots.
///     <see cref="Hide" /> blanks a slot instead of parking it off-screen; blanked slots
///     stay in the buffer as degenerate quads and cost no vertex work.
///     Slots are written in absolute UI coordinates - this is not an element and never
///     goes through layout.
/// </summary>
internal sealed class LabelBatch : IRenderable, IClippable
{
    /// <summary>Both grids' captions are 11px - the same constant their row math centers on.</summary>
    public const float FontSize = 11f;

    private readonly TextBuffer _buffer;
    private readonly float _fontSize;
    private readonly int _sliceCapacity;
    private readonly List<Slot> _slots = [];

    /// <param name="context">Supplies the text provider and delete queue.</param>
    /// <param name="count">Slots to reserve up front; more are appended on demand.</param>
    /// <param name="fontSize">Size for every slot - one batch, one size.</param>
    /// <param name="sliceCapacity">Characters a slot holds before it needs a bigger slice.</param>
    public LabelBatch(UIContext context, int count, float fontSize = FontSize, int sliceCapacity = 8)
    {
        _buffer = new TextBuffer(context.TextProvider, context.DeleteQueue);
        _fontSize = fontSize;
        _sliceCapacity = sliceCapacity;
        EnsureCount(count);
    }

    public IReadOnlyList<Slot> Slots => _slots;

    public Vector4i? ClipRect { get; set; }

    public void Render(Camera camera)
    {
        _buffer.RenderBuffer(camera);
    }

    /// <summary>Appends slots until the pool holds at least <paramref name="count" /> of them.</summary>
    public void EnsureCount(int count)
    {
        while (_slots.Count < count) _slots.Add(NewSlot(_sliceCapacity));
    }

    /// <summary>A view over part of the pool - one caller's own sub-range of slots.</summary>
    public IReadOnlyList<Slot> Range(int start, int count)
    {
        return _slots.GetRange(start, count);
    }

    /// <summary>
    ///     Assigns a slot's text, absolute position and color, re-laying its glyphs only
    ///     when one of them actually changed - the views re-assign every slot on every
    ///     layout pass.
    /// </summary>
    /// <param name="clip">
    ///     Box this caption's pixels are confined to, in the same absolute UI units as
    ///     <paramref name="x" />/<paramref name="y" />: (left, top, right, bottom).
    ///     Default (all-zero) leaves it unclipped. This is per slot, unlike
    ///     <see cref="ClipRect" />, which scissors the whole batch at once and so cannot serve
    ///     a pool whose captions each belong to a different box (arrangement clip names). See
    ///     <see cref="Sundex.Engine.Text.TextCharacter.ClipRect" />.
    /// </param>
    public void Set(int index, string text, float x, float y, Vector4 color, Vector4 clip = default)
    {
        EnsureCount(index + 1);
        var slot = _slots[index];
        if (slot.Text == text && slot.X.Equals(x) && slot.Y.Equals(y) && slot.Color == color &&
            slot.Clip == clip) return;

        if (text.Length > slot.Capacity)
        {
            // Outgrew the slice it was born with: take a bigger one and hand the old range
            // back to the buffer's free list, rather than clipping the text to fit.
            slot.Slice.Dispose();
            slot = NewSlot(text.Length);
            _slots[index] = slot;
        }

        slot.Text = text;
        slot.X = x;
        slot.Y = y;
        slot.Color = color;
        slot.Clip = clip;

        var slice = slot.Slice;
        slice.UpdateManually = true;
        slice.Value = text;
        slice.Color = color;
        slice.ClipRect = clip;
        slice.Position = (x, y, 0);
        slice.UpdateManually = false;
        slice.UpdateCharacters();
    }

    /// <summary>
    ///     Releases a slot: empty text blanks its characters, so it draws nothing. A slot
    ///     past the pool's end holds nothing to release, so it is left alone rather than
    ///     appended into existence.
    /// </summary>
    public void Hide(int index)
    {
        if (index >= _slots.Count) return;
        Set(index, "", 0, 0, default);
    }

    private Slot NewSlot(int capacity)
    {
        var slice = _buffer.GetTextSlice("", capacity);
        slice.FontSize = _fontSize;
        return new Slot(slice, capacity);
    }

    /// <summary>One caption. <see cref="Visible" /> is false while the slot is released.</summary>
    internal sealed class Slot(TextSlice slice, int capacity)
    {
        public TextSlice Slice { get; } = slice;

        /// <summary>Characters this slot's slice can hold before it has to be replaced.</summary>
        public int Capacity { get; } = capacity;

        public string Text { get; internal set; } = "";
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public Vector4 Color { get; internal set; }

        /// <inheritdoc cref="Sundex.Engine.Text.TextCharacter.ClipRect" />
        public Vector4 Clip { get; internal set; }

        public bool Visible => Text.Length > 0;
    }
}
