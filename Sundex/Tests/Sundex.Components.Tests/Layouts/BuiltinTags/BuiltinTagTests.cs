using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Bars;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

namespace Sundex.Components.Tests.Layouts.BuiltinTags;

/// <summary>
///     Every tag a component declares must also be buildable from markup. Six of the
///     twelve only had a <see cref="UIElement.Tag" /> - enough for a stylesheet to target
///     them, not enough for the builder, which threw "Unknown tag" instead.
/// </summary>
public class BuiltinTagTests
{
    private readonly TestUIContext _context = new();

    private SundexComponent Build(string document = "AllTags.xml")
    {
        var sundex = new SundexContext(_context);
        return sundex.NewComponent(_context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = $"Layouts/BuiltinTags/{document}",
                Storage = StorageLocation.Assembly
            }
        }).Value);
    }

    private static Vector4 FillOf(Panel panel)
    {
        return Assert.IsType<ColoredPlane>(panel.Background).Color;
    }

    [Fact]
    public void EveryTag_BuildsItsElementType()
    {
        var component = Build();

        Assert.IsType<ScrollView>(component.GetID<UIElement>("scroller"));
        Assert.IsType<ModalLayer>(component.GetID<UIElement>("dialog"));
        Assert.IsType<TextInput>(component.GetID<UIElement>("text"));
        Assert.IsType<NumericInput>(component.GetID<UIElement>("number"));
        Assert.IsType<Checkbox>(component.GetID<UIElement>("check"));
        Assert.IsType<Slider>(component.GetID<UIElement>("volume"));
    }

    [Fact]
    public void ContainerTags_BuildTheirMarkupChildren()
    {
        var component = Build();

        var scroller = component.GetID<ScrollView>("scroller");
        // The scroll bar is an off-tree sub-element, so only the markup child is here.
        Assert.Same(component.GetID<Label>("scrolled"), Assert.Single(scroller.Children));

        var modal = component.GetID<ModalLayer>("dialog");
        Assert.Same(component.GetID<Label>("modal-label"), Assert.Single(modal.Children));
    }

    [Fact]
    public void InputTags_HonourTheirAttributes()
    {
        var component = Build();

        Assert.Equal("hello", component.GetID<TextInput>("text").Value);
        Assert.Equal(42.5, component.GetID<NumericInput>("number").Value);
        Assert.Equal(0.25, component.GetID<Slider>("volume").Value);

        var checkbox = component.GetID<Checkbox>("check");
        Assert.True(checkbox.Checked);
        // TextSlice keeps a fixed-size char[], so shortening the text leaves trailing NULs.
        Assert.Equal("Enabled", checkbox.Label.Value.TrimEnd('\0').ToString());
    }

    /// <summary>
    ///     A ProgressBar's planes are constructor arguments, and its background/foreground
    ///     [NamedSetting]s are Panel-typed, which no ApplyStyleValue case handles - so the
    ///     builder's resolution is the only route the sheet has to them. It was tag-only,
    ///     leaving class/id rules silently dead.
    /// </summary>
    [Fact]
    public void BarBackgrounds_ResolveByIdThenClassThenTag()
    {
        var component = Build("StyledBars.xml");

        var plain = component.GetID<ProgressBar>("plain-bar");
        Assert.Equal(new Vector4(0x11 / 255f, 0, 0, 1), FillOf(plain.BackgroundPanel));
        Assert.Equal(new Vector4(0x22 / 255f, 0, 0, 1), FillOf(plain.ForegroundPanel));

        var hero = component.GetID<ProgressBar>("hero-bar");
        Assert.Equal(new Vector4(0x44 / 255f, 0, 0, 1), FillOf(hero.BackgroundPanel)); // id beats tag
        Assert.Equal(new Vector4(0x33 / 255f, 0, 0, 1), FillOf(hero.ForegroundPanel)); // class beats tag
    }
}
