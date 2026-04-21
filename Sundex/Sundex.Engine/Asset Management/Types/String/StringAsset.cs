using Sundex.Engine.Asset_Management.Abstract.Loading;

namespace Sundex.Engine.Asset_Management.Types.String;

public class StringAsset : ILoadableAsset<StringAsset, StringInfo>
{
    public string Value { get; set; } = string.Empty;

    public static IAssetLoader<StringAsset, StringInfo> AssetLoader { get; } = new StringAssetLoader();
}