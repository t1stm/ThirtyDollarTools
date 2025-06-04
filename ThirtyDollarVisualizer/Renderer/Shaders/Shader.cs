using System.Buffers;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Assets;

namespace ThirtyDollarVisualizer.Renderer.Shaders;

/// <summary>
/// Represents an OpenGL shader program.
/// </summary>
public class Shader : IDisposable
{
    private Shader(int handle)
    {
        Handle = handle;
    }

    /// <summary> 
    /// Controls whether the shader throws errors on missing uniforms.
    /// </summary>
    public bool IsPedantic { get; init; } = false;

    public static Shader Dummy { get; } = new(0);
    protected ShaderDefinition[] Definitions { get; set; } = [];
    protected int Handle { get; set; }

    public void Dispose()
    {
        GL.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }

    public static Shader NewVertexFragment(string vertexPath, string fragmentPath)
    {
        return NewDefined(ShaderDefinition.Vertex(vertexPath), ShaderDefinition.Fragment(fragmentPath));
    }

    public static Shader NewDefined(params ShaderDefinition[] shaders)
    {
        var shader = new Shader(GL.CreateProgram());
        shader.AddShaderDefinitions(shaders);
        shader.Definitions = shaders;
        return shader;
    }

    private void AddShaderDefinitions(ShaderDefinition[] shaders)
    {
        var shaderHandleArray = ArrayPool<int>.Shared.Rent(shaders.Length);

        try
        {
            for (var index = 0; index < shaders.Length; index++)
            {
                var definition = shaders[index];
                var tempShader = shaderHandleArray[index] = LoadShaderAtPath(definition.ShaderType, definition.Path);
                GL.AttachShader(Handle, tempShader);
            }

            LinkAndThrowOnError();

            foreach (var handle in shaderHandleArray)
            {
                GL.DetachShader(Handle, handle);
                GL.DeleteShader(handle);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(shaderHandleArray);
        }
    }

    protected void LinkAndThrowOnError()
    {
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out var link_status);

        if (link_status == 0)
            throw new Exception($"Program failed to link with error: {GL.GetProgramInfoLog(Handle)}");
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    public void ReloadFiles()
    {
        GL.DeleteProgram(Handle);
        Handle = GL.CreateProgram();
        AddShaderDefinitions(Definitions);
    }
    
    public bool SetUniform(string name, int value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }

        GL.Uniform1(location, value);
        return true;
    }

    public bool SetUniform(string name, Vector2 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }

        GL.Uniform2(location, value);
        return true;
    }

    public bool SetUniform(string name, Vector3 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }

        GL.Uniform3(location, value);
        return true;
    }

    public bool SetUniform(string name, Vector4 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }

        GL.Uniform4(location, value);
        return true;
    }

    public unsafe bool SetUniform(string name, Matrix4 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }

        GL.UniformMatrix4(location, 1, false, (float*)&value);
        return true;
    }

    public bool SetUniform(string name, float value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }

        GL.Uniform1(location, value);
        return true;
    }

    private static int LoadShaderAtPath(ShaderType type, string path)
    {
        var asset = AssetManager.GetAsset(path);
        using var stream = asset.Stream;
        
        var streamReader = new StreamReader(stream);
        var source = streamReader.ReadToEnd();

        var handle = GL.CreateShader(type);
        GL.ShaderSource(handle, source);
        GL.CompileShader(handle);
        
        var infoLog = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
            throw new Exception($"Error compiling shader \'{path}\' of type {type}, failed with error {infoLog}");

        return handle;
    }
}