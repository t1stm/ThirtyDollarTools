using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Buffers;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace LoadingScene.Background;

internal record AnimatedTexture(TrackedBufferReference<SoundData> Reference)
{
    public Vector2 Velocity { get; set; }
}