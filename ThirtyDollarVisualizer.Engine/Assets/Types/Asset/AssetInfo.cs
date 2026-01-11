using System.Text.Json;
using System.Text.Json.Serialization;
using ThirtyDollarVisualizer.Engine.Assets.Abstract;
using ThirtyDollarVisualizer.Engine.Common;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

public class AssetInfo : ILoaderInfo
{
    public string Location { get; set; } = string.Empty;
    public StorageLocation Storage { get; set; } = StorageLocation.Unknown;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, SerializerOptions.Json);
    }
}

public enum StorageLocation
{
    Unknown = 0,
    Disk = 1,
    Assembly = 2,
    Network = 4
}