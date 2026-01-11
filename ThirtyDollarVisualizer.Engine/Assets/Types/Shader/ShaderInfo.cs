using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Assets.Abstract;
using ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Shader;

public class ShaderInfo : ILoaderInfo
{
    public AssetInfo AssetInfo { get; set; } = new();
    public ShaderType Type { get; set; }

    public static ShaderInfo CreateFromUnknownStorage(ShaderType type, string location) =>
        new() { Type = type, AssetInfo = new AssetInfo { Location = location, Storage = StorageLocation.Unknown } };
}