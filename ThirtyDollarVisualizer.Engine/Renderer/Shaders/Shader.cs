using System.Buffers;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Shader;
using ThirtyDollarVisualizer.Engine.Renderer.Debug;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;

namespace ThirtyDollarVisualizer.Engine.Renderer.Shaders;

/// <summary>
///     Represents an OpenGL shader program.
/// </summary>
public class Shader(AssetProvider assetProvider, params ShaderSource[] shaderSource) : IDisposable
{
    /// <summary>
    ///     An allocated shader object that points to OpenGL program handle 0
    /// </summary>
    public static Shader Dummy { get; } = new(null!)
    {
        Handle = 0
    };

    public ShaderSource[] Sources { get; } = shaderSource;
    public BufferState BufferState { get; set; } = BufferState.PendingCreation;

    /// <summary>
    ///     Controls whether the shader throws errors on missing uniforms.
    /// </summary>
    public bool IsPedantic { get; set; } = false;

    /// <summary>
    ///     The OpenGL program handle.
    /// </summary>
    protected int Handle { get; set; }

    /// <summary>
    ///     Disposes the shader program and releases all resources.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        GL.DeleteProgram(Handle);
    }

    private void Load()
    {
        if (BufferState == BufferState.Created)
        {
            RenderMarker.Debug($"Deleting already created shader: {Handle}");
            GL.DeleteProgram(Handle);
        }

        Handle = GL.CreateProgram();
        RenderMarker.Debug($"Created Shader: ({Handle})");

        var handlesArray = ArrayPool<int>.Shared.Rent(Sources.Length);
        var handles = handlesArray.AsSpan()[..Sources.Length];

        try
        {
            for (var index = 0; index < Sources.Length; index++)
            {
                var source = Sources[index];
                var sourceCode = source.SourceCode;

                var shader = handles[index] = GL.CreateShader(source.Type);
                GL.ShaderSource(shader, sourceCode);
                GL.CompileShader(shader);

                GL.AttachShader(Handle, shader);
                RenderMarker.Debug($"Attached: ({shader}) {source.Type}");
            }

            LinkAndThrowOnError();

            foreach (var shaderHandle in handles)
            {
                GL.DetachShader(Handle, shaderHandle);
                GL.DeleteShader(shaderHandle);
                RenderMarker.Debug($"Detached and Deleted: ({shaderHandle})");
            }

            BufferState = BufferState.Created;
            RenderMarker.Debug($"Shader: ({Handle}) Successfully Loaded.");
        }
        catch
        {
            BufferState = BufferState.Failed;
            RenderMarker.Debug($"Failed to Create Shader: {Handle}");
            throw;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(handlesArray);
        }
    }

    /// <summary>
    ///     Links an OpenGL shader program and checks for any errors during the linking process.
    ///     Throws an exception if the program fails to link.
    /// </summary>
    /// <exception cref="Exception">
    ///     Thrown when the shader program fails to link. The exception message contains the error details retrieved from
    ///     OpenGL.
    /// </exception>
    private void LinkAndThrowOnError()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Handle, 1);
        GL.LinkProgram(Handle);
        GL.GetProgrami(Handle, ProgramProperty.LinkStatus, out var link_status);

        if (link_status != 0) return;
        GL.GetProgramInfoLog(Handle, out var error);
        throw new Exception($"Program failed to link with error: {error}");
    }

    /// <summary>
    ///     Sets the current OpenGL context to use this shader program.
    ///     This ensures that any later rendering operations are executed using the specified shader program.
    /// </summary>
    public void Use()
    {
        if (BufferState == BufferState.PendingCreation)
            Load();

        if (BufferState.HasFlag(BufferState.Failed))
            throw new Exception("Tried to use a failed shader.");

        ArgumentOutOfRangeException.ThrowIfLessThan(Handle, 1);
        GL.UseProgram(Handle);
    }

    /// <summary>
    ///     Reloads and recreates the shader program using the previously defined shader configurations.
    ///     This involves deleting the current shader program, creating a new program, and re-attaching the existing shader
    ///     definitions.
    /// </summary>
    public void ReloadShader()
    {
        var assetInfoHolder = ArrayPool<ShaderInfo>.Shared.Rent(Sources.Length);
        var shaderInfos = assetInfoHolder.AsSpan();

        try
        {
            var span = Sources.AsSpan();
            for (var index = 0; index < span.Length; index++) shaderInfos[index] = span[index].Info;

            assetProvider.Load<ShaderSource, ShaderInfo>(Sources, shaderInfos);
            Load();
        }
        finally
        {
            ArrayPool<ShaderInfo>.Shared.Return(assetInfoHolder);
        }
    }

    #region Uniform Methods

    /// <summary>
    ///     Sets the value of an integer uniform in the shader program by its name.
    /// </summary>
    /// <param name="name">The name of the uniform variable to set.</param>
    /// <param name="value">The integer value to assign to the uniform.</param>
    /// <returns>
    ///     Returns <c>true</c> if the uniform was successfully set; otherwise, <c>false</c>.
    ///     If the uniform does not exist and <c>IsPedantic</c> is <c>true</c>, an <see cref="Exception" /> is thrown.
    /// </returns>
    /// <exception cref="Exception">
    ///     Thrown if <c>IsPedantic</c> is <c>true</c> and the uniform is not found in the shader
    ///     program.
    /// </exception>
    public bool SetUniform(string name, int value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1) return IsPedantic ? throw new Exception($"Uniform \'{name}\' not found in shader.") : false;

        GL.Uniform1i(location, value);
        return true;
    }

    /// <summary>
    ///     Sets the value of a uniform variable in the shader program to the specified 2D vector value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Vector2" /> value to set for the uniform variable.</param>
    /// <returns>
    ///     True if the uniform variable was successfully updated; false if the uniform was not found and
    ///     <c>IsPedantic</c> is not enabled.
    /// </returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
    public bool SetUniform(string name, Vector2 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1) return IsPedantic ? throw new Exception($"Uniform \'{name}\' not found in shader.") : false;

        GL.Uniform2f(location, value.X, value.Y);
        return true;
    }

    /// <summary>
    ///     Sets the value of a uniform variable in the shader program to the specified 3D vector value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Vector3" /> value to set for the uniform variable.</param>
    /// <returns>
    ///     True if the uniform variable was successfully updated; false if the uniform was not found and
    ///     <c>IsPedantic</c> is not enabled.
    /// </returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
    public bool SetUniform(string name, Vector3 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1) return IsPedantic ? throw new Exception($"Uniform \'{name}\' not found in shader.") : false;

        GL.Uniform3f(location, value.X, value.Y, value.Z);
        return true;
    }

    /// <summary>
    ///     Sets the value of a uniform variable in the shader program to the specified 4D vector value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Vector4" /> value to set for the uniform variable.</param>
    /// <returns>
    ///     True if the uniform variable was successfully updated; false if the uniform was not found and
    ///     <c>IsPedantic</c> is not enabled.
    /// </returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
    public bool SetUniform(string name, Vector4 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1) return IsPedantic ? throw new Exception($"Uniform \'{name}\' not found in shader.") : false;

        GL.Uniform4f(location, value.X, value.Y, value.Z, value.W);
        return true;
    }

    /// <summary>
    ///     Sets the value of a uniform variable in the shader program to the specified Matrix4 value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="Matrix4" /> value to set for the uniform variable.</param>
    /// <returns>
    ///     True if the uniform variable was successfully updated; false if the uniform was not found and
    ///     <c>IsPedantic</c> is not enabled.
    /// </returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
    public bool SetUniform(string name, Matrix4 value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1) return IsPedantic ? throw new Exception($"Uniform \'{name}\' not found in shader.") : false;

        GL.UniformMatrix4f(location, 1, false, ref value);
        return true;
    }

    /// <summary>
    ///     Sets the value of a uniform variable in the shader program to the specified float value.
    /// </summary>
    /// <param name="name">The name of the uniform variable in the shader program.</param>
    /// <param name="value">The <see cref="float" /> value to set for the uniform variable.</param>
    /// <returns>
    ///     True if the uniform variable was successfully updated; false if the uniform was not found and
    ///     <c>IsPedantic</c> is not enabled.
    /// </returns>
    /// <exception cref="Exception">Thrown if the uniform variable is not found and <c>IsPedantic</c> is enabled.</exception>
    public bool SetUniform(string name, float value)
    {
        var location = GL.GetUniformLocation(Handle, name);
        if (location == -1) return IsPedantic ? throw new Exception($"Uniform \'{name}\' not found in shader.") : false;

        GL.Uniform1f(location, value);
        return true;
    }

    #endregion
}