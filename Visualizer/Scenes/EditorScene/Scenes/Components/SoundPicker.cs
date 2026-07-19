using OpenTK.Mathematics;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Data_Buffers;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The note editor's sound picker: a wrapping grid of TDW sound icons, ported from
///     DrumMaster's SoundList. Same atlas/render-stack drawing, but click-to-select
///     through the UI input routing instead of DrumMaster's drag-and-drop.
/// </summary>
public class SoundPicker : FlexPanel
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";
    private const float SoundElementSize = 40f;

    private readonly AtlasStore _store;
    private StackCollection _stacks = new();

    public SoundPicker(UIContext context, AtlasStore store) : base(context)
    {
        _store = store;
        Height = LiteralOrComputable.AutoSize;
        Direction = LayoutDirection.Horizontal;
        Wrap = true;
        Spacing = 4;
        Padding = 8;
    }

    /// <summary>Fired with the sound's name when an icon is clicked (single-select mode).</summary>
    public Action<string>? OnPick { get; set; }

    /// <summary>
    ///     When true, clicking an icon toggles it in <see cref="Selected" /> and tints it
    ///     instead of firing <see cref="OnPick" />. Used by the track-automation sound
    ///     filter; the default single-select "active sound" picker is unaffected.
    /// </summary>
    public bool MultiSelect { get; set; }

    public HashSet<string> Selected { get; } = [];

    public bool HasSounds => Children.Count > 0;

    /// <summary>
    ///     Fills the grid from the atlas store. Call lazily — the atlases may still be
    ///     loading while the scene is constructed; sounds without an image are skipped.
    /// </summary>
    public void Fill(IEnumerable<string> soundNames)
    {
        foreach (var name in soundNames) AddSound(name);
        InvalidateLayout();
    }

    /// <summary>Reseeds <see cref="Selected" /> and re-tints icons to match — call each
    /// time a multi-select picker is reopened, since it may edit a different filter.</summary>
    public void SetSelected(IEnumerable<string> sounds)
    {
        Selected.Clear();
        foreach (var name in sounds) Selected.Add(name);
        foreach (var icon in Children.OfType<SoundIcon>())
            icon.ApplySelection(Selected.Contains(icon.SoundName));
    }

    private void AddSound(string soundName)
    {
        if (_store.AnimatedSounds.TryGetValue(soundName, out var framedAtlas))
        {
            if (!_stacks.AnimatedStacks.TryGetValue(framedAtlas, out var stack))
            {
                var shader = Context.AssetProvider.ShaderPool.GetOrLoad(AnimatedShaderLocation);
                stack = new RenderStack<SoundData>(Context.DeleteQueue) { Shader = shader };
                _stacks.AnimatedStacks.Add(framedAtlas, stack);
            }

            stack.List.Add(new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One });
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);
            var aspect = framedAtlas.CurrentRectangle.Width / (float)framedAtlas.CurrentRectangle.Height;

            AddChild(new SoundIcon(Context, this, soundName)
            {
                AnimatedReference = reference,
                Width = aspect > 1 ? SoundElementSize : SoundElementSize * aspect,
                Height = aspect > 1 ? SoundElementSize / aspect : SoundElementSize
            });
        }
        else if (_store.StaticSounds.TryGetValue(soundName, out var staticAtlas))
        {
            if (!staticAtlas.TryGetSound(soundName, out var rect)) return;

            if (!_stacks.StaticStacks.TryGetValue(staticAtlas, out var stack))
            {
                var shader = Context.AssetProvider.ShaderPool.GetOrLoad(StaticShaderLocation);
                stack = new RenderStack<StaticSound>(Context.DeleteQueue) { Shader = shader };
                _stacks.StaticStacks.Add(staticAtlas, stack);
            }

            stack.List.Add(new StaticSound
            {
                Data = new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One },
                TextureUV = QuadUV.FromRectangle(rect, staticAtlas.Width, staticAtlas.Height)
            });
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);
            var aspect = rect.Width / (float)rect.Height;

            AddChild(new SoundIcon(Context, this, soundName)
            {
                StaticReference = reference,
                Width = aspect > 1 ? SoundElementSize : SoundElementSize * aspect,
                Height = aspect > 1 ? SoundElementSize / aspect : SoundElementSize
            });
        }
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(_stacks, Index);
    }

    public override void StopRendering()
    {
        Context.DequeueRender(_stacks, Index);
        base.StopRendering();
    }

    public override void ApplyClip(Vector4i? clip)
    {
        _stacks.ClipRect = clip;
        base.ApplyClip(clip);
    }

    /// <summary>One icon; its screen rectangle is pushed into the instanced render stack.</summary>
    private sealed class SoundIcon : Panel
    {
        private static readonly Vector4 SelectedTint = new(0.478f, 0.635f, 0.969f, 1f); // #7aa2f7

        public SoundIcon(UIContext context, SoundPicker picker, string soundName) : base(context)
        {
            SoundName = soundName;
            UpdateCursorOnHover = true;
            Computed = new ComputedRectangle { OnUpdate = UpdateMatrix };
            OnClick = _ =>
            {
                if (!picker.MultiSelect)
                {
                    picker.OnPick?.Invoke(soundName);
                    return;
                }

                if (!picker.Selected.Remove(soundName)) picker.Selected.Add(soundName);
                ApplySelection(picker.Selected.Contains(soundName));
            };
        }

        public string SoundName { get; }
        public TrackedBufferReference<StaticSound>? StaticReference { get; init; }
        public TrackedBufferReference<SoundData>? AnimatedReference { get; init; }

        public sealed override ComputedRectangle Computed { get; protected set; }

        public void ApplySelection(bool selected)
        {
            var rgba = selected ? SelectedTint : Vector4.One;

            if (StaticReference != null)
            {
                var value = StaticReference.Value;
                value.Data.RGBA = rgba;
                StaticReference.Value = value;
            }

            if (AnimatedReference != null)
            {
                var value = AnimatedReference.Value;
                value.RGBA = rgba;
                AnimatedReference.Value = value;
            }
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

            if (AnimatedReference != null)
            {
                var value = AnimatedReference.Value;
                value.Model = matrix;
                AnimatedReference.Value = value;
            }
        }
    }
}
