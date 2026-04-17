using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Markup;
using Sundex.Markup.Abstract;

namespace DrumMasterScene.UI;

public class TaikoLaneConfiguration : FlexPanel
{
    public TaikoLaneConfiguration(UIContext context, ISundexContext? sundexContext = null) : base(context)
    {
        SundexContext = sundexContext;
        LaneLabel = new Label(context, "Taiko Lane")
        {
            FontSizePx = 24
        };
        DropZone = new FlexPanel(context)
        {
            VerticalAlign = Align.Center,
            BorderRadius = 8,
            Padding = 5,
            Spacing = 5,
            Height = 60,
            Width = LiteralOrComputable.Percent(100),
            Background = new ColoredPlane
            {
                Color = new Vector4(0.5f, 0.5f, 0.5f, 1f)
            }
        };

        var buttonRow = new FlexPanel(context)
            { Direction = LayoutDirection.Horizontal, Spacing = 5, Width = LiteralOrComputable.AutoSize };
        buttonRow.AddChild(new Button(context, new Label(context, "Clear"),
            new ColoredPlane { Color = new Vector4(0.3f, 0.3f, 0.3f, 1f) })
        {
            Width = 60,
            OnClick = _ => { ReturnChildren(); }
        });
        buttonRow.AddChild(new Button(context, new Label(context, "Delete"),
            new ColoredPlane { Color = new Vector4(0.3f, 0.3f, 0.3f, 1f) })
        {
            Width = 60,
            OnClick = _ =>
            {
                ReturnChildren();
                OnDeleteRequested?.Invoke(this);
            }
        });

        var topRow = new FlexPanel(context)
        {
            Direction = LayoutDirection.Horizontal,
            Width = LiteralOrComputable.Percent(100),
            Spacing = 10,
            VerticalAlign = Align.Center
        };
        topRow.AddChild(LaneLabel);
        topRow.AddChild(buttonRow);

        Children = [topRow, DropZone];
        VerticalAlign = Align.Start;
    }

    public Label LaneLabel { get; }
    public FlexPanel DropZone { get; }
    public List<string> SelectedSounds { get; } = [];
    public ISundexContext? SundexContext { get; }
    public Action<UIElement>? OnDeleteRequested { get; set; }

    public override LiteralOrComputable Width { get; set; } = LiteralOrComputable.Percent(100);
    public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.AutoSize;

    public override LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
    public override float Spacing { get; set; } = 5;
    public override float Padding { get; set; } = 10;

    public void ReturnChildren()
    {
        var children = DropZone.Children.ToArray();
        if (SundexContext is not SundexContext sundexContext) return;

        foreach (var child in children)
        {
            if (child is not SoundListElement element) continue;
            if (sundexContext.LoadedComponents.TryGetValue("available-sounds", out var component) &&
                component.Element is SoundList soundList)
            {
                soundList.AddChild(element);
                continue;
            }

            DropZone.RemoveChild(child);
        }

        SelectedSounds.Clear();
    }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        base.Test(mouse, scale);
        if (!mouse.IsButtonReleased(MouseButton.Left) || DragManager.DraggedElement == null) return;

        if (!IsHovered) return;

        var element = DragManager.DraggedElement;
        if (element.Parent?.Parent is TaikoLaneConfiguration oldConfig)
            oldConfig.SelectedSounds.Remove(element.SoundName);

        AddSound(element.SoundName, element);
        DragManager.DraggedElement = null;
    }

    public void AddSound(string soundName, UIElement element)
    {
        if (SelectedSounds.Contains(soundName)) return;
        SelectedSounds.Add(soundName);
        DropZone.AddChild(element);
    }
}