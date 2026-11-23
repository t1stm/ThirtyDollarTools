using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics;

namespace ThirtyDollarVisualizer.WASM;

internal static partial class BrowserContext
{
    [DynamicDependency("Invoke", typeof(ClearColorSignature))]
    [DynamicDependency("Invoke", typeof(ReadPixelsSignature))]
    [DynamicDependency("Invoke", typeof(CopyTexSubImageSignature))]
    [DynamicDependency("Invoke", typeof(TexImageSignature))]
    [DynamicDependency("Invoke", typeof(BlitFramebufferSignature))]
    public static void Open()
    {
        var result = CreateContext();
        if (result != 0)
            throw new InvalidOperationException($"Could not make a WebGL 2 context current (Emscripten result {result}).");
        GLLoader.LoadBindings(new BrowserBindings());
    }

    [LibraryImport("*", EntryPoint = "sundex_create_webgl2_context")]
    private static partial int CreateContext();

    [LibraryImport("*", EntryPoint = "emscripten_GetProcAddress", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetProcAddress(string name);

    // Mono's WASM build discovers these interpreter trampolines from delegate metadata.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ClearColorSignature(float red, float green, float blue, float alpha);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReadPixelsSignature(int x, int y, int width, int height, uint format, uint type, nint pixels);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CopyTexSubImageSignature(uint target, int level, int offsetX, int offsetY, int x, int y, int width, int height);

    // TexImage2D and TexSubImage2D both lower to nine WASM i32 arguments.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TexImageSignature(uint target, int level, int internalFormat, int width, int height, int border,
        uint format, uint type, nint pixels);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void BlitFramebufferSignature(int sourceX0, int sourceY0, int sourceX1, int sourceY1,
        int destX0, int destY0, int destX1, int destY1, uint mask, uint filter);

    private sealed class BrowserBindings : IBindingsContext
    {
        public nint GetProcAddress(string procName) => BrowserContext.GetProcAddress(procName);
    }
}
