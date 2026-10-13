using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Loading and building for markup documents that stand on their own - the dialogs,
///     which are built fresh per show rather than imported into the editor's root.
/// </summary>
public static class Markup
{
    /// <summary>Loads an embedded markup/script asset by its project-relative path.</summary>
    public static string Load(UIContext context, string location)
    {
        return context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo { Location = location }
        }).Value;
    }

    /// <summary>
    ///     Parses and builds a document with no imports and no logic, returning its
    ///     component so the caller can resolve handles off <c>RegisteredIDs</c>.
    ///     ponytail: a fresh SundexContext per call, so nothing is cached between shows and
    ///     every show pays one markup parse plus one style parse. Hoist the context onto the
    ///     scene if a document ever gets built in a loop.
    /// </summary>
    public static SundexComponent Build(UIContext context, string location)
    {
        var sundex = new SundexContext(context);
        // A label cut off at its parent's edge, for text that comes at runtime and may be long
        // (a project's name, its path). Its text is set from code, so it takes no value.
        sundex.RegisterElementFactory("clipped-label", ui => new ClippedLabel(ui, ""));
        return sundex.NewComponent(Load(context, location));
    }
}
