using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class Shader : IDisposable
{
    private readonly int _handle;
    private static readonly Dictionary<(string, string), Shader> CachedShaders = new();

    public Shader(string vertexPath, string fragmentPath)
    {
        CachedShaders.TryGetValue((vertexPath, fragmentPath), out var shader);
        if (shader != null)
        {
            _handle = shader._handle;
            return;
        }
        
        var vertex = LoadShader(ShaderType.VertexShader, vertexPath);
        var fragment = LoadShader(ShaderType.FragmentShader, fragmentPath);
        _handle = GL.CreateProgram();
        
        GL.AttachShader(_handle, vertex);
        GL.AttachShader(_handle, fragment);
        
        GL.LinkProgram(_handle);
        GL.GetProgram(_handle, ProgramParameter.LinkStatus, out var status);
        
        if (status == 0)
        {
            throw new Exception($"Program failed to link with error: {GL.GetProgramInfoLog(_handle)}");
        }
        
        GL.DetachShader(_handle, vertex);
        GL.DetachShader(_handle, fragment);
        
        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);
        
        CachedShaders.Add((vertexPath, fragmentPath), this);
    }

    public void Use()
    {
        GL.UseProgram(_handle);
    }

    public void SetUniform(string name, int value)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        GL.Uniform1(location, value);
    }

    public void SetUniform(string name, Vector2 value)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        GL.Uniform2(location, value);
    }

    public void SetUniform(string name, Vector3 value)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        GL.Uniform3(location, value);
    }

    public void SetUniform(string name, Vector4 value)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        GL.Uniform4(location, value);
    }

    public unsafe void SetUniform(string name, Matrix4 value)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        GL.UniformMatrix4(location, 1, false, (float*) &value);
    }

    public void SetUniform(string name, float value)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        GL.Uniform1(location, value);
    }

    public void Dispose()
    {
        GL.DeleteProgram(_handle);
    }   

    private int LoadShader(ShaderType type, string path)
    {
        var src = File.ReadAllText(path);
        var handle = GL.CreateShader(type);
        GL.ShaderSource(handle, src);
        GL.CompileShader(handle);
        var infoLog = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new Exception($"Error compiling shader of type {type}, failed with error {infoLog}");
        }

        return handle;
    }
}