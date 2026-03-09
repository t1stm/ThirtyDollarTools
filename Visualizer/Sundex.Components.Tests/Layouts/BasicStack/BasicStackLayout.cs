using Sunder.Markup;
using Sundex.Components.Abstractions;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;

namespace Sundex.Components.Tests.Layouts.BasicStack;

public class BasicStackLayout
{
    public static SundexComponent Create(UIContext uiContext)
    {
        var markupContext = new SundexContext<object>(new object(), uiContext);
        var asset = uiContext.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Layouts/BasicStack/BasicStack.xml",
                Storage = StorageLocation.Assembly
            }
        });
        return markupContext.NewComponent(asset.Value);
    }
}