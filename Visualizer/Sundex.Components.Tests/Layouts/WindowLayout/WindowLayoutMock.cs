using Sundex.Components.Abstractions;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

namespace Sundex.Components.Tests.Layouts.WindowLayout;

public class WindowLayoutMock
{
    public static SundexComponent Create(UIContext uiContext)
    {
        var markupContext = new SundexContext(uiContext);
        var asset = uiContext.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Layouts/WindowLayout/WindowLayout.xml",
                Storage = StorageLocation.Assembly
            }
        });
        return markupContext.NewComponent(asset.Value);
    }
}