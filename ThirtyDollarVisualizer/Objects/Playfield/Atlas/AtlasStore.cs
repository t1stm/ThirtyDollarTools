namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class AtlasStore
{
    public required StaticSoundAtlas StaticAtlas { get; set; }
    public required Dictionary<string, FramedAtlas> AnimatedAtlases { get; set; }
}