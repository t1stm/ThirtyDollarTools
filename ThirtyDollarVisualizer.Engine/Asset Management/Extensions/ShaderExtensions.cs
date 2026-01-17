using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Shader;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;

public static class ShaderExtensions
{
    public static ShaderSource[] LoadShaders(this AssetProvider assetProvider, params ReadOnlySpan<ShaderInfo> infos)
    {
        var array = new ShaderSource[infos.Length];
        assetProvider.Load<ShaderSource, ShaderInfo>(array, infos);
        return array;
    }
}