using System.Text;
using Sundex.Engine.Asset_Management.Abstract;
using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Types.Asset;

namespace Sundex.Engine.Asset_Management.Types.String;

public class StringInfo : ILoaderInfo
{
    public AssetInfo AssetInfo { get; set; } = new();
    public Encoding? Encoding { get; set; }

    public static StringInfo CreateFromUnknownStorage(string location, Encoding? encoding = null)
    {
        return new StringInfo
        {
            AssetInfo = new AssetInfo { Location = location, Storage = StorageLocation.Unknown },
            Encoding = encoding
        };
    }
}