using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace DrumMasterScene.UI;

public class SoundList(UIContext context, AtlasStore store) : FlexPanel(context), IDisposable
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";
    private const float SoundElementSize = 40f;

    public override LiteralOrComputable Width { get; set; } = LiteralOrComputable.AutoSize;
    public override LiteralOrComputable Height { get; set; } = LiteralOrComputable.AutoSize;
    public override LayoutDirection Direction { get; set; } = LayoutDirection.Vertical;
    public override float Spacing { get; set; } = 2;
    public override float Padding { get; set; } = 5;
    public override bool Wrap { get; set; } = true;
    public StackCollection StackCollection { get; private set; } = new();

    public void Clear()
    {
        Children.Clear();
        StackCollection.Dispose();
        StackCollection = new StackCollection();
    }
    
    public void AddSound(string soundName)
    {
        SoundListElement? element = null;

        if (store.AnimatedSounds.TryGetValue(soundName, out var framedAtlas))
        {
            if (!StackCollection.AnimatedStacks.TryGetValue(framedAtlas, out var stack))
            {
                var shader = Context.AssetProvider.ShaderPool.GetOrLoad(AnimatedShaderLocation);
                stack = new RenderStack<SoundData>(Context.DeleteQueue) { Shader = shader };
                StackCollection.AnimatedStacks.Add(framedAtlas, stack);
            }

            stack.List.Add(new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One });
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);

            var aspectRatio = framedAtlas.CurrentRectangle.Width / (float)framedAtlas.CurrentRectangle.Height;

            element = new SoundListElement(Context, soundName)
            {
                AnimatedReference = reference,
                Width = aspectRatio > 1 ? SoundElementSize : SoundElementSize * aspectRatio,
                Height = aspectRatio > 1 ? SoundElementSize / aspectRatio : SoundElementSize
            };
        }
        else if (store.StaticSounds.TryGetValue(soundName, out var staticAtlas))
        {
            if (!staticAtlas.TryGetSound(soundName, out var rect))
                return;

            if (!StackCollection.StaticStacks.TryGetValue(staticAtlas, out var stack))
            {
                var shader = Context.AssetProvider.ShaderPool.GetOrLoad(StaticShaderLocation);
                stack = new RenderStack<StaticSound>(Context.DeleteQueue) { Shader = shader };
                StackCollection.StaticStacks.Add(staticAtlas, stack);
            }

            var staticSound = new StaticSound
            {
                Data = new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One },
                TextureUV = QuadUV.FromRectangle(rect, staticAtlas.Width, staticAtlas.Height)
            };

            stack.List.Add(staticSound);
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);

            var aspectRatio = rect.Width / (float)rect.Height;

            element = new SoundListElement(Context, soundName)
            {
                StaticReference = reference,
                Width = aspectRatio > 1 ? SoundElementSize : SoundElementSize * aspectRatio,
                Height = aspectRatio > 1 ? SoundElementSize / aspectRatio : SoundElementSize
            };
        }

        if (element != null)
        {
            AddChild(element);
        }
    }

    public override void Test(MouseState mouse, Vector2 scale)
    {
        base.Test(mouse, scale);
        if (!mouse.IsButtonReleased(MouseButton.Left) || DragManager.DraggedElement == null) return;

        if (!IsHovered) return;
        
        var element = DragManager.DraggedElement;
        
        // If it was already in the sound list, it will just snap back due to AddChild's logic and InvalidateLayout.
        // If it was in a lane, we need to remove it from that lane's configuration list.
        if (element.Parent?.Parent is TaikoLaneConfiguration config)
        {
            config.SelectedSounds.Remove(element.SoundName);
        }

        AddChild(element);
        DragManager.DraggedElement = null;
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(StackCollection, Index);
    }

    public void Dispose()
    {
        StackCollection.Dispose();
        GC.SuppressFinalize(this);
    }
}
