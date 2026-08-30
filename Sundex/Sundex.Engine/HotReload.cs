using System.Reflection.Metadata;
using Sundex.Engine;

[assembly: MetadataUpdateHandler(typeof(HotReload))]

namespace Sundex.Engine;

/// <summary>What a reload request should do to the UI that is already on screen.</summary>
public enum ReloadScope
{
    /// <summary>
    ///     Re-read the stylesheets and apply them to the live tree. Keeps everything the
    ///     running UI is holding - scroll positions, selection, focus, playback.
    /// </summary>
    Styles,

    /// <summary>
    ///     Rebuild the element trees from their markup. Needed for anything a stylesheet
    ///     cannot express, and the only thing that picks up an edited layout or logic block.
    /// </summary>
    Full
}

/// <summary>
///     The one entry point a reload request goes through, wherever it comes from.
///     <para>
///         The IDE's hot-reload button reaches <see cref="UpdateApplication" /> below through
///         the MetadataUpdateHandler attribute on this assembly, but only for an edit that
///         produced an IL delta; <see cref="SourceWatcher" /> covers a .snx.ss or .snx.xml
///         edit, which produces none, by watching the markup on disk.
///     </para>
///     <para>
///         Both fire from threads that are not the render thread, so <see cref="Request" />
///         only hands the work to <see cref="Requested" />, which <see cref="Game" /> points
///         at its own frame queue.
///     </para>
/// </summary>
public static class HotReload
{
    /// <summary>
    ///     Set by <see cref="Game" /> in Debug builds. Null when nothing is listening, which
    ///     is every Release build and any test that never constructs a Game.
    /// </summary>
    public static Action<ReloadScope>? Requested { get; set; }

    public static void Request(ReloadScope scope)
    {
        Requested?.Invoke(scope);
    }

    /// <summary>Called by the runtime when the IDE applies a hot reload. Signature is fixed.</summary>
    public static void UpdateApplication(Type[]? updatedTypes)
    {
        _ = updatedTypes;
        // A code edit can change anything the markup builds against, so the whole UI is
        // rebuilt rather than restyled.
        Request(ReloadScope.Full);
    }
}
