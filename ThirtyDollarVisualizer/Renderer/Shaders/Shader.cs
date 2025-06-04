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
    /// <summary> 
    /// Controls whether the shader throws errors on missing uniforms.
    /// </summary>
    public bool IsPedantic { get; init; } = false;

    /// <summary>
    /// An allocated shader object that points to OpenGL program handle 0
    /// </summary>
    public static Shader Dummy { get; } = new(0);
    
    /// <summary>
    /// Definitions of each shader.
    /// </summary>
    protected ShaderDefinition[] Definitions { get; set; } = [];
    
    /// <summary>
    /// The OpenGL program handle.
    /// </summary>
    protected int Handle { get; set; }
    
    private Shader(int handle)
    {
        Handle = handle;
    }

    /// <summary>
    /// Disposes the shader program and releases all resources.
    /// </summary>
    public void Dispose()
    {
        GL.DeleteProgram(Handle);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a new shader program from the given vertex and fragment shader paths. These paths support embedded assets.
    /// </summary>
    /// <param name="vertexPath">The path of the vertex shader to be loaded with <see cref="AssetManager.GetAsset(string)"/></param>
    /// <param name="fragmentPath">The path of the fragment shader to be loaded with <see cref="AssetManager.GetAsset(string)"/></param>
    /// <returns></returns>
    public static Shader NewVertexFragment(string vertexPath, string fragmentPath)
    {
        return NewDefined(ShaderDefinition.Vertex(vertexPath), ShaderDefinition.Fragment(fragmentPath));
    }

    /// <summary>
    /// Creates a new shader program from the given shader definitions. These shader definitions support embedded asset paths.
    /// </summary>
    /// <param name="shaders">Array of <see cref="ShaderDefinition"/></param>
    /// <returns></returns>
    public static Shader NewDefined(params ShaderDefinition[] shaders)
    {
        var shader = new Shader(GL.CreateProgram());
        shader.AddShaderDefinitions(shaders);
        shader.Definitions = shaders;
        return shader;
    }
    
    private void AddShaderDefinitions(ShaderDefinition[] shaders)
    {
        // rents an array for shader handles
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
            // releases it back to the pool
            ArrayPool<int>.Shared.Return(shaderHandleArray);
        }
    }

    /// <summary>
    /// Links an OpenGL shader program and checks for any errors during the linking process.
    /// Throws an exception if the program fails to link.
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown when the shader program fails to link. The exception message contains the error details retrieved from OpenGL.
    /// </exception>
    protected void LinkAndThrowOnError()
    {
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out var link_status);

        if (link_status == 0)
            throw new Exception($"Program failed to link with error: {GL.GetProgramInfoLog(Handle)}");
    }

    /// <summary>
    /// Sets the current OpenGL context to use this shader program.
    /// This ensures that any later rendering operations are executed using the specified shader program.
    /// </summary>
    public void Use()
    {
        GL.UseProgram(Handle);
    }

    /// <summary>
    /// Reloads and recreates the shader program using the previously defined shader configurations.
    /// This involves deleting the current shader program, creating a new program, and re-attaching the existing shader definitions.
    /// </summary>
    public void ReloadShader()
    {
        GL.DeleteProgram(Handle);
        Handle = GL.CreateProgram();
        AddShaderDefinitions(Definitions);
    }

    /// <summary>
    /// Sets the value of an integer uniform in the shader program by its name.
    /// </summary>
    /// <param name="name">The name of the uniform variable to set.</param>
    /// <param name="value">The integer value to assign to the uniform.</param>
    /// <returns>
    /// Returns <c>true</c> if the uniform was successfully set; otherwise, <c>false</c>.
    /// If the uniform does not exist and <c>IsPedantic</c> is <c>true</c>, an <see cref="Exception"/> is thrown.
    /// </returns>
    /// <exception cref="Exception">Thrown if <c>IsPedantic</c> is <c>true</c> and the uniform is not found in the shader program.</exception>
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

    /// <summary>
    /// Sets the value of a uniform variable in the shader program to the specified 2D vector value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Vector2"/> value to set for the uniform variable.</param>
    /// <returns>True if the uniform variable was successfully updated; false if the uniform was not found and <c>IsPedantic</c> is not enabled.</returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
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

    /// <summary>
    /// Sets the value of a uniform variable in the shader program to the specified 3D vector value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Vector3"/> value to set for the uniform variable.</param>
    /// <returns>True if the uniform variable was successfully updated; false if the uniform was not found and <c>IsPedantic</c> is not enabled.</returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
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

    /// <summary>
    /// Sets the value of a uniform variable in the shader program to the specified 4D vector value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Vector4"/> value to set for the uniform variable.</param>
    /// <returns>True if the uniform variable was successfully updated; false if the uniform was not found and <c>IsPedantic</c> is not enabled.</returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
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

    /// <summary>
    /// Sets the value of a uniform variable in the shader program to the specified Matrix4 value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Matrix4"/> value to set for the uniform variable.</param>
    /// <returns>True if the uniform variable was successfully updated; false if the uniform was not found and <c>IsPedantic</c> is not enabled.</returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
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

    /// <summary>
    /// Sets the value of a uniform variable in the shader program to the specified float value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="float"/> value to set for the uniform variable.</param>
    /// <returns>True if the uniform variable was successfully updated; false if the uniform was not found and <c>IsPedantic</c> is not enabled.</returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
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