using ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class AtlasStore
{
    public required List<StaticSoundAtlas> StaticAtlases { get; set; }
    public required Dictionary<string, FramedAtlas> AnimatedAtlases { get; set; }

    public Dictionary<string, ImageAtlas> SoundLocations { get; set; } = [];
}