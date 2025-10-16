using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Attributes;
using ThirtyDollarVisualizer.Renderer.Buffers;

namespace ThirtyDollarVisualizer.Base_Objects;

[PreloadGL]
[UsedImplicitly]
public class GLQuad : IGLPreloadable
{
    public static GLBuffer<float> VBOWithoutUV { get; } = new(BufferTarget.ArrayBuffer);
    public static GLBuffer<float> VBOWithUV { get; } = new(BufferTarget.ArrayBuffer);
    public static GLBuffer<int> EBO { get; } = new(BufferTarget.ElementArrayBuffer);
    
    public static void Preload()
    {
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
        VBOWithoutUV.DangerousGLThread_SetBufferData(verticesWithoutUV);
        
        VBOWithUV.Bind();
        VBOWithUV.DangerousGLThread_SetBufferData(verticesWithUV);
        
        ReadOnlySpan<int> indices = [0, 1, 3, 1, 2, 3];
        
        EBO.Bind();
        EBO.DangerousGLThread_SetBufferData(indices);
    }
}