using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Renderer.Shaders;

public struct ShaderDefinition()
{
    public ShaderType ShaderType;
    public string Path = "";

    public static ShaderDefinition Vertex(string path) => new()
    {
        Path = path,
        ShaderType = ShaderType.VertexShader,
    };

    public static ShaderDefinition Fragment(string path) => new()
    {
        Path = path,
        ShaderType = ShaderType.FragmentShader,
    };
}