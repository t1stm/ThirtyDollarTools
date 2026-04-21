using System.Text;
using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Types.Asset;

namespace Sundex.Engine.Asset_Management.Types.String;

public class StringAssetLoader : IAssetLoader<StringAsset, StringInfo>
{
    public bool Query(StringInfo createInfo, AssetProvider assetProvider)
    {
        return assetProvider.Query<AssetStream, AssetInfo>(createInfo.AssetInfo);
    }

    public StringAsset Load(StringInfo createInfo, AssetProvider assetProvider)
    {
        return Load(createInfo, assetProvider, Create);
    }

    public StringAsset Load(StringInfo createInfo, AssetProvider assetProvider,
        Func<StringInfo, AssetProvider, StringAsset> create)
    {
        return create(createInfo, assetProvider);
    }

    public static StringAsset Create(StringInfo createInfo, AssetProvider assetProvider)
    {
        var assetStream = assetProvider.Load<AssetStream, AssetInfo>(createInfo.AssetInfo);

        var encoding = createInfo.Encoding ?? Encoding.UTF8;
        using var stream = assetStream.Stream;
        using var reader = new StreamReader(stream, encoding, true);
        var content = reader.ReadToEnd();

        return new StringAsset
        {
            Value = content
        };
    }
}