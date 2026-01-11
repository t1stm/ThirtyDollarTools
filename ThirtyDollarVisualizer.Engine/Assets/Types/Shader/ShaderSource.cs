using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Assets.Abstract;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Shader;

public class ShaderSource : ILoadableAsset<ShaderSource, ShaderInfo>
{
    public static IAssetLoader<ShaderSource, ShaderInfo> AssetLoader { get; } = new ShaderLoader();
    
    public ShaderInfo Info { get; set; } = new();
    public ShaderType Type { get; set; }
    public string SourceCode { get; set; } = string.Empty;
}