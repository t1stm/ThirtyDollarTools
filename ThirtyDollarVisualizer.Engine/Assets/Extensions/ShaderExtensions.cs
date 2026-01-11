using ThirtyDollarVisualizer.Engine.Assets.Types.Shader;

namespace ThirtyDollarVisualizer.Engine.Assets.Extensions;

public static class ShaderExtensions
{
    public static ShaderSource[] LoadShaders(this AssetProvider assetProvider, params ReadOnlySpan<ShaderInfo> infos)
    {
        var array = new ShaderSource[infos.Length];
        assetProvider.Load<ShaderSource, ShaderInfo>(array, infos);
        return array;
    }
}