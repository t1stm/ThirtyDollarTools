using System.Runtime.InteropServices;
using ThirtyDollarVisualizer.Base_Objects.Textures.Atlas;

namespace ThirtyDollarVisualizer.Objects.Playfield.Atlas;

public class AtlasStore
{
    public required List<StaticSoundAtlas> StaticAtlases { get; set; }
    public required Dictionary<string, FramedAtlas> AnimatedAtlases { get; set; }

    public void Update()
    {
        foreach (var (_, atlas) in AnimatedAtlases)
        {
            atlas.Update();
        }

        foreach (var atlas in CollectionsMarshal.AsSpan(StaticAtlases))
        {
            atlas.Update();
        }
    }
}