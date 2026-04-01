using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Buffers;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace DrumMasterScene.UI;

public class SoundListElement : FlexPanel
{
    public string SoundName { get; }
    public override LiteralOrComputable Width { get; set; } = LiteralOrComputable.AutoSize;
    public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.AutoSize;

    public TrackedBufferReference<StaticSound>? StaticReference { get; set; }
    public TrackedBufferReference<SoundData>? AnimatedReference { get; set; }

    public sealed override ComputedRectangle Computed { get; protected set; }

    public SoundListElement(UIContext context, string soundName) : base(context)
    {
        SoundName = soundName;
        UpdateCursorOnHover = true;
        Computed = new ComputedRectangle(this)
        {
            OnUpdate = UpdateMatrix
        };
    }

    private void UpdateMatrix()
    {
        var matrix = Matrix4.CreateScale(Computed.Width, Computed.Height, 1) *
                     Matrix4.CreateTranslation(Computed.AbsoluteX, Computed.AbsoluteY, 0);

        if (StaticReference != null)
        {
            var value = StaticReference.Value;
            value.Data.Model = matrix;
            StaticReference.Value = value;
        }

        if (AnimatedReference == null) return;
        {
            var value = AnimatedReference.Value;
            value.Model = matrix;
            AnimatedReference.Value = value;
        }
    }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        base.Test(mouse, scale);

        if (DragManager.DraggedElement == this)
        {
            if (mouse.IsButtonReleased(MouseButton.Left))
            {
                InvalidateLayout();
                DoLayout();
                UpdateMatrix();
                return;
            }

            Computed.OverrideAbsolutePositions(mouse.X / scale.X - Computed.Width / 2, mouse.Y / scale.Y - Computed.Height / 2);
            UpdateMatrix();
        }
        else if (CurrentState == UIState.Pressed && DragManager.DraggedElement == null)
        {
            DragManager.DraggedElement = this;
        }
    }
}
