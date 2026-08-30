using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;

namespace Sundex.Components.Tests.Layouts.ComponentReuse;

/// <summary>
///     A registered component used as a tag elsewhere yields an independent tree per usage
///     site, rather than handing every usage the registered component's own Element - which
///     would make two usages one instance, the second reparenting the first out of the tree.
/// </summary>
public class ComponentReuseTests
{
    private readonly TestUIContext _context = new();

    private static string Load(UIContext uiContext, string name)
    {
        return uiContext.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = $"Layouts/ComponentReuse/{name}",
                Storage = StorageLocation.Assembly
            }
        }).Value;
    }

    /// <summary>Registers the header component, then builds the host that uses it twice.</summary>
    private (SundexComponent Header, SundexComponent Host) Build()
    {
        var sundex = new SundexContext(_context);
        var header = sundex.NewComponent(Load(_context, "Header.xml"));
        var host = sundex.NewComponent(Load(_context, "Host.xml"));
        return (header, host);
    }

    [Fact]
    public void NamedComponent_RegistersUnderItsName()
    {
        var sundex = new SundexContext(_context);
        var header = sundex.NewComponent(Load(_context, "Header.xml"));

        // The builder has to assign Name; RegisterComponent throws without one.
        Assert.Equal("header", header.Name);
        Assert.Same(header, sundex.LoadedComponents["header"]);
    }

    [Fact]
    public void TwoUsages_ProduceTwoIndependentTrees()
    {
        var (header, host) = Build();
        var stack = (StackPanel)host.Element;

        Assert.Equal(2, stack.Children.Count);
        var first = stack.Children[0];
        var second = stack.Children[1];

        Assert.NotSame(first, second);
        Assert.Same(stack, first.Parent);
        Assert.Same(stack, second.Parent);

        // The registered template stays out of the tree entirely.
        Assert.NotSame(header.Element, first);
        Assert.NotSame(header.Element, second);
        Assert.Null(header.Element.Parent);
    }

    [Fact]
    public void UsageSiteAttributes_ApplyOnTopOfEachInstance()
    {
        var (_, host) = Build();
        var stack = (StackPanel)host.Element;

        Assert.Same(stack.Children[0], host.GetID<UIElement>("first"));
        Assert.Same(stack.Children[1], host.GetID<UIElement>("second"));
    }

    [Fact]
    public void Dependencies_AreRecorded()
    {
        var (header, host) = Build();

        Assert.Same(header, Assert.Single(host.Dependencies));
        Assert.Equal(2, host.Children.Count);
    }

    [Fact]
    public void Logic_BindsToEachInstance_NotTheTemplate()
    {
        var (header, host) = Build();

        // Each usage site compiled against its own id map. A shared delegate would write
        // the template's label twice and leave both in-tree labels untouched.
        host.RunLogic?.Invoke(null);

        // TextSlice keeps a fixed-size char[], so shortening the text leaves trailing NULs.
        foreach (var child in host.Children.Cast<SundexComponent>())
            Assert.Equal("wired", child.GetID<Label>("header-title").Value.TrimEnd('\0').ToString());

        Assert.Equal("untouched", header.GetID<Label>("header-title").Value.TrimEnd('\0').ToString());
    }
}
