using Sundex.Components.Abstractions;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

namespace Sundex.Components.Tests.Layouts.NestedFlex;

public class NestedFlexLayout
{
    public static SundexComponent Create(UIContext uiContext)
    {
        var markupContext = new SundexContext(uiContext);
        var asset = uiContext.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Layouts/NestedFlex/NestedFlex.xml",
                Storage = StorageLocation.Assembly
            }
        });
        return markupContext.NewComponent(asset.Value);
    }
}