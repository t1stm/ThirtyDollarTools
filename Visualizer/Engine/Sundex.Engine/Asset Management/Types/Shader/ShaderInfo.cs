using OpenTK.Graphics.OpenGL;
using Sundex.Engine.Asset_Management.Abstract;
using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Types.Asset;

namespace Sundex.Engine.Asset_Management.Types.Shader;

public class ShaderInfo : ILoaderInfo
{
    public AssetInfo AssetInfo { get; set; } = new();
    public ShaderType Type { get; set; }

    public static ShaderInfo CreateFromUnknownStorage(ShaderType type, string location)
    {
        return new ShaderInfo
            { Type = type, AssetInfo = new AssetInfo { Location = location, Storage = StorageLocation.Unknown } };
    }
}