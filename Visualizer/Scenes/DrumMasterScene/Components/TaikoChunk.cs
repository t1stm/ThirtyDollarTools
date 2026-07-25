using Sundex.Engine.Text;
using VisualizerScene.Objects;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace DrumMasterScene.Components;

public class TaikoChunk(TextBuffer textBuffer, StackCollection stacks) : IDisposable
{
    public TextBuffer TextBuffer { get; } = textBuffer;
    public StackCollection Stacks { get; } = stacks;
    public List<SoundRenderable> Renderables { get; } = [];
    public List<TaikoSound> Sounds { get; } = [];
    public RenderStack<BackgroundBlip>? BackgroundBlips { get; set; }

    public void Dispose()
    {
        TextBuffer.Dispose();
        Stacks.Dispose();
        BackgroundBlips?.Dispose();
        GC.SuppressFinalize(this);
    }
}