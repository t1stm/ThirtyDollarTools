using System.Diagnostics;
using System.Reflection;
using System.Text;
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer.Engine.Renderer.Debug;

public static class RenderMarker
{
    public static bool Enabled { get; set; } = true;
    
    [Conditional("DEBUG")]
    public static unsafe void Debug(ReadOnlySpan<char> message, MarkerType markerType = MarkerType.Visible,
        DebugSource source = DebugSource.DebugSourceApplication,
        DebugType type = DebugType.DebugTypeMarker)
    {
        if (!Enabled) return;
        var bytesLength = message.Length * sizeof(char);
        Span<byte> bytes = stackalloc byte[bytesLength];

        Encoding.UTF8.GetBytes(message, bytes);
        var id = (uint)markerType;

        fixed (byte* messagePtr = bytes)
        {
            GL.DebugMessageInsert(source, type, id, DebugSeverity.DebugSeverityNotification, -1, messagePtr);
        }
    }
    
    [Conditional("DEBUG")]
    public static void Debug(ReadOnlySpan<char> message1, ReadOnlySpan<char> message2, MarkerType markerType = MarkerType.Visible,
        DebugSource source = DebugSource.DebugSourceApplication,
        DebugType type = DebugType.DebugTypeMarker)
    {
        if (!Enabled) return;
        Span<char> message = stackalloc char[message1.Length + message2.Length];
        
        message1.CopyTo(message);
        message2.CopyTo(message[message1.Length..]);
        Debug(message, markerType, source, type);
    }
}