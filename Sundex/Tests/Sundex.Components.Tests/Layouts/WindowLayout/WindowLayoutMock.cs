using Sundex.Components.Abstractions;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Markup;

namespace Sundex.Components.Tests.Layouts.WindowLayout;

public static class WindowLayoutMock
{
    public static SundexComponent Create(UIContext uiContext)
    {
        var markupContext = new SundexContext(uiContext);
        var asset = uiContext.AssetProvider.LoadString("Layouts/WindowLayout/WindowLayout.xml", StorageLocation.Assembly);
        return markupContext.NewComponent(asset);
    }
}