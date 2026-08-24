using EditorScene.Scenes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using StringInfo = Sundex.Engine.Asset_Management.Types.String.StringInfo;

namespace EditorScene.Tests;

/// <summary>
///     Style hot reload against the editor, which is the screen that composes: seven
///     imported documents, each with a stylesheet of its own, and the interface's sheet
///     applied over the lot afterwards. A reload has to re-run that whole cascade rather
///     than any one sheet, or the imports come out unstyled - which showed up as the header
///     labels rendering at the default size and colour.
/// </summary>
public class StyleReloadTests : IDisposable
{
    private readonly EditorTestContext _context = new();
    private readonly bool _tracked = UIElement.TrackPristineStyles;

    public StyleReloadTests()
    {
        UIElement.TrackPristineStyles = true;
    }

    public void Dispose()
    {
        UIElement.TrackPristineStyles = _tracked;
    }

    /// <summary>The real editor header, built and styled by its own document as the editor builds it.</summary>
    private SundexComponent Header()
    {
        var markup = _context.AssetProvider.Load<StringAsset, StringInfo>(
            StringInfo.CreateFromUnknownStorage("Scenes/Layout/Editor Header/EditorHeader.snx.xml")).Value;
        return new SundexContext(_context).NewComponent(markup);
    }

    [Fact]
    public void TheInterfaceSheetDoesNotStripTheHeadersFontSizes()
    {
        // Exactly what EditorInterface does after building its imports. The interface sheet
        // has no rules for these ids, so applying it must leave the header's own alone.
        var header = Header();
        var title = header.GetID<Label>("editor-title");
        var name = header.GetID<Label>("project-name");

        Assert.Equal(16f, title.FontSizePx.Value);
        Assert.Equal(14f, name.FontSizePx.Value);

        header.Element.ApplyStyleSheet(EditorTestContext.Styles);

        Assert.Equal(16f, title.FontSizePx.Value);
        Assert.Equal(14f, name.FontSizePx.Value);
    }

    [Fact]
    public void ReloadingKeepsTheHeadersFontSizesAndColours()
    {
        var header = Header();
        var title = header.GetID<Label>("editor-title");
        var colourBefore = title.Color;

        header.Element.ApplyStyleSheet(EditorTestContext.Styles);
        header.ReloadStyleSheet();

        Assert.Equal(16f, title.FontSizePx.Value);
        Assert.Equal(colourBefore, title.Color);
    }

    [Fact]
    public void ReloadingIsRepeatable()
    {
        var header = Header();
        var title = header.GetID<Label>("editor-title");

        for (var i = 0; i < 3; i++) header.ReloadStyleSheet();

        Assert.Equal(16f, title.FontSizePx.Value);
    }
}
