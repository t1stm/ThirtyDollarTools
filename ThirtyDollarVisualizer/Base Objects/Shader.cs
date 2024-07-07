using System.Reflection;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class Shader : IDisposable
{
    private static readonly Dictionary<(string, string), Shader> CachedShaders = new();
    public int Handle { get; }

    /// <summary>
    ///     Controls whether the shader throws errors on missing uniforms.
    /// </summary>
    private readonly bool IsPedantic = false;

    public Shader(string vertexPath, string fragmentPath)
    {
        CachedShaders.TryGetValue((vertexPath, fragmentPath), out var shader);
        if (shader != null)
        {
            Handle = shader.Handle;
            return;
        }

        var vertex = LoadShader(ShaderType.VertexShader, vertexPath);
        var fragment = LoadShader(ShaderType.FragmentShader, fragmentPath);
        Handle = GL.CreateProgram();

        GL.AttachShader(Handle, vertex);
        GL.AttachShader(Handle, fragment);

        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out var link_status);

        if (link_status == 0)
            throw new Exception($"Program failed to link with error: {GL.GetProgramInfoLog(Handle)}");

        GL.DetachShader(Handle, vertex);
        GL.DetachShader(Handle, fragment);

        GL.DeleteShader(vertex);
        GL.DeleteShader(fragment);

        CachedShaders.Add((vertexPath, fragmentPath), this);
    }

    public void Dispose()
    {
        GL.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }

    public void Use()
    {
        GL.UseProgram(Handle);
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

            if (stream == null)
                throw new FileNotFoundException($"Unable to find shader \'{path}\' in assembly or real path.");

            using var stream_reader = new StreamReader(stream);
            source = stream_reader.ReadToEnd();
        }

        var handle = GL.CreateShader(type);
        GL.ShaderSource(handle, source);
        GL.CompileShader(handle);
        var infoLog = GL.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
            throw new Exception($"Error compiling shader \'{path}\' of type {type}, failed with error {infoLog}");

        return handle;
    }
}