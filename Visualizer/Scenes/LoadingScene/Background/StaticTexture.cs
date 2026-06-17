using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Data_Buffers;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace LoadingScene.Background;

internal record StaticTexture(TrackedBufferReference<StaticSound> Reference)
{
    public Vector2 Velocity { get; set; }
}