using OpenTK.Graphics.OpenGL;
using Sundex.Engine.Asset_Management.Abstract;
using Sundex.Engine.Asset_Management.Abstract.Loading;

namespace Sundex.Engine.Asset_Management.Types.Shader;

public class ShaderSource : ILoadableAsset<ShaderSource, ShaderInfo>
{
    public ShaderInfo Info { get; set; } = new();
    public ShaderType Type { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public static IAssetLoader<ShaderSource, ShaderInfo> AssetLoader { get; } = new ShaderLoader();
}