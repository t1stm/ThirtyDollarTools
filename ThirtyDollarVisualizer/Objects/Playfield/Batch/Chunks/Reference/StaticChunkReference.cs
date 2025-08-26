using ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks.Reference;

public class StaticChunkReference(int index, IList<StaticSound> sounds, BufferObject<StaticSound> bufferObject) : IChunkReference
{
    public SoundData Sound
    {
        get => sounds[index].Data;
        set => bufferObject[index] = sounds[index] = sounds[index] with { Data = value };
    }
}