using System.Reflection;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class Shader : IDisposable
{
    private readonly int _handle;
    /// <summary>
    /// Controls whether the shader throws errors on missing uniforms.
    /// </summary>
    private bool IsPedantic = false;
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
        GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out var link_status);
        
        if (link_status == 0)
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

    public bool SetUniform(string name, int value)
    {
        var location = GL.GetUniformLocation(_handle, name);
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
        var location = GL.GetUniformLocation(_handle, name);
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
        var location = GL.GetUniformLocation(_handle, name);
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
        var location = GL.GetUniformLocation(_handle, name);
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
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }
        GL.UniformMatrix4(location, 1, false, (float*) &value);
        return true;
    }

    public bool SetUniform(string name, float value)
    {
        var location = GL.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            if (IsPedantic) throw new Exception($"Uniform \'{name}\' not found in shader.");
            return false;
        }
        GL.Uniform1(location, value);
        return true;
    }

    public void Dispose()
    {
        GL.DeleteProgram(_handle);
    }   

    private static int LoadShader(ShaderType type, string path)
    {
        string source;

        if (File.Exists(path))
        {
            source = File.ReadAllText(path);
        }
        else
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(path);

            if (stream == null) throw new FileNotFoundException($"Unable to find shader \'{path}\' in assembly or real path.");
            
            using var stream_reader = new StreamReader(stream);
            source = stream_reader.ReadToEnd();
        }
        
        var handle = GL.CreateShader(type);
        GL.ShaderSource(handle, source);
        GL.CompileShader(handle);
        var infoLog = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new Exception($"Error compiling shader \'{path}\' of type {type}, failed with error {infoLog}");
        }

        return handle;
    }
}