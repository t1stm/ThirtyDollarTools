#nullable enable

using System.Numerics;
using Silk.NET.OpenGL;

namespace ThirtyDollarVisualizer;

public struct ShaderData
{
    public string? FilePath;
    public string Code;
    public ShaderType Type;
}

public class Shader
{
    private ShaderData[] _sources = Array.Empty<ShaderData>();
    private readonly Dictionary<string, int> _uniformLocations = new();
    private uint Program;

    private readonly GL Gl;

    public Shader(GL gl)
    {
        Gl = gl;
    }

    private uint CompileShader(ShaderType type, string code)
    {
        const int FAIL = 0;
        var shader = Gl.CreateShader(type);

        Gl.ShaderSource(shader, code);
        Gl.CompileShader(shader);

        Gl.GetShader(shader, ShaderParameterName.CompileStatus, out var result);
        if (result != FAIL) return shader;

        Gl.GetShaderInfoLog(shader, out var message);
        Gl.DeleteShader(shader);
        throw new Exception($"\'{type}\' compilation failed with message: \"{message}\"");
    }

    public uint CreateShaderProgram(params ShaderData[] shaderFiles)
    {
        var program = Gl.CreateProgram();

        foreach (var shaderFile in shaderFiles)
        {
            var compiledShader = CompileShader(shaderFile.Type, shaderFile.Code);

            Gl.AttachShader(program, compiledShader);

            Gl.LinkProgram(program);
            Gl.ValidateProgram(program);

            Gl.DeleteShader(compiledShader);
        }

        return program;
    }

    public static Shader FromFiles(GL gl, string vertexShaderPath, string fragmentShaderPath)
    {
        var vertexShader = File.ReadAllText(vertexShaderPath);
        var fragmentShader = File.ReadAllText(fragmentShaderPath);
        var sources = new ShaderData[]
        {
            new()
            {
                FilePath = vertexShaderPath,
                Code = vertexShader,
                Type = ShaderType.VertexShader
            },
            new()
            {
                FilePath = fragmentShaderPath,
                Code = fragmentShader,
                Type = ShaderType.FragmentShader
            }
        };

        var shader = new Shader(gl)
        {
            _sources = sources
        };

        shader.Program = shader.CreateShaderProgram(sources);

        return shader;
    }

    public void SetUniform1(string name, int value)
    {
        var location = GetUniformLocation(name);
        Gl.Uniform1(location, value);
    }
    
    public void SetUniform4(string name, Vector4 color)
    {
        var location = GetUniformLocation(name);
        Gl.Uniform4(location, color);
    }

    public void SetUniformMatrix4(string name, ReadOnlySpan<float> matrix)
    {
        var location = GetUniformLocation(name);
        Gl.UniformMatrix4(location, true, matrix);
    }

    public int GetUniformLocation(string name)
    {
        bool found;
        int location;
        lock (_uniformLocations)
        {
            found = _uniformLocations.TryGetValue(name, out location);
        }

        if (found) return location;
        location = Gl.GetUniformLocation(Program, name);
        if (location == -1)
            throw new Exception(
                $"Uniform \'{name}\' wasn't found in files \"{string.Join(' ', _sources.Select(r => $"{r.FilePath} "))}\".");
        lock (_uniformLocations)
        {
            _uniformLocations.Add(name, location);
        }

        return location;
    }

    public void SetUniform4(string name, float r, float g, float b, float a)
    {
        SetUniform4(name, new Vector4(r, g, b, a));
    }

    public void Bind()
    {
        Gl.UseProgram(Program);
    }

    public void Unbind()
    {
        Gl.UseProgram(0);
    }
}