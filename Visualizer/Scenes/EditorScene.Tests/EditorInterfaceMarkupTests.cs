using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

// UIContext stores its providers in static fields (see Sundex.Components.Tests),
// so this suite must also run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EditorScene.Tests;

/// <summary>
///     Headless smoke test: the editor's snx layout + stylesheet parse and the ids the
///     code-behind looks up exist. A stylesheet typo otherwise only surfaces when the
///     scene is opened.
/// </summary>
public class EditorInterfaceMarkupTests
{
    [Fact]
    public void EditorInterfaceMarkup_ParsesAndRegistersAllWiredIds()
    {
        var context = new EditorTestContext();
        var markup = new SundexContext(context);
        var asset = context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/EditorInterface.snx.xml",
                Storage = StorageLocation.Assembly
            }
        });

        var component = markup.NewComponent(asset.Value);

        string[] wired =
        [
            "project-name", "project-bpm", "track-column", "grid-area", "inspector-column",
            "load-button", "save-button", "export-button"
        ];
        foreach (var id in wired)
            Assert.True(component.RegisteredIDs.ContainsKey(id), $"markup lost id \"{id}\"");
    }
}
