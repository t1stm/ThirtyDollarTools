using System.Text.Json;
using Sundex.Engine.Asset_Management.Abstract;
using Sundex.Engine.Common;

namespace Sundex.Engine.Asset_Management.Types.Asset;

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