using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;

namespace ThirtyDollarVisualizer.Engine.Renderer.Buffers;

[PreloadGraphicsContext]
[UsedImplicitly]
public class GLQuad : IGamePreloadable
{
    /// <summary>
    ///     Preloaded VBO that contains data for a quad without UV.
    /// </summary>
    public static GLBuffer<float> VBOWithoutUV { get; set; } = null!;

    /// <summary>
    ///     Preloaded VBO that contains data for a quad with UV.
    /// </summary>
    public static GLBuffer<float> VBOWithUV { get; set; } = null!;

    /// <summary>
    ///     Preloaded EBO that contains data for a quad.
    /// </summary>
    public static GLBuffer<int> EBO { get; set; } = null!;

    public static void Preload(AssetProvider assetProvider)
    {
        var deleteQueue = assetProvider.DeleteQueue;

        VBOWithoutUV = new GLBuffer<float>(deleteQueue, BufferTarget.ArrayBuffer);
        VBOWithUV = new GLBuffer<float>(deleteQueue, BufferTarget.ArrayBuffer);
        EBO = new GLBuffer<int>(deleteQueue, BufferTarget.ElementArrayBuffer);

        var (x, y, z) = (0f, 0f, 0);
        var (w, h) = (1f, 1f);

        ReadOnlySpan<float> verticesWithoutUV =
        [
            x, y + h, z,
            x + w, y + h, z,
            x + w, y, z,
            x, y, z
        ];

        ReadOnlySpan<float> verticesWithUV =
        [
            x, y + h, z, 0.0f, 1.0f,
            x + w, y + h, z, 1.0f, 1.0f,
            x + w, y, z, 1.0f, 0.0f,
            x, y, z, 0.0f, 0.0f
        ];

        VBOWithoutUV.Bind();
        VBOWithoutUV.Dangerous_SetBufferData(verticesWithoutUV);

        VBOWithUV.Bind();
        VBOWithUV.Dangerous_SetBufferData(verticesWithUV);

        ReadOnlySpan<int> indices = [0, 1, 3, 1, 2, 3];

        EBO.Bind();
        EBO.Dangerous_SetBufferData(indices);
    }
}