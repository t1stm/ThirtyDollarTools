using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;

namespace Sundex.Engine.Asset_Management.Extensions;

public static class StringExtensions
{
    /// <summary>Loads a text asset (markup, script, stylesheet) by its location and returns its contents.</summary>
    public static string LoadString(this IAssetProvider assetProvider, string location,
        StorageLocation storage = StorageLocation.Unknown)
    {
        return assetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = location, Storage = storage }
        }).Value;
    }
}
