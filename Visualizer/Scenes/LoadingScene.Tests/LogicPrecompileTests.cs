using LoadingScene.Scenes;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using Sundex.Markup;
using StringInfo = Sundex.Engine.Asset_Management.Types.String.StringInfo;

namespace LoadingScene.Tests;

/// <summary>
///     The logic precompile the loading screen runs on a worker while the sounds come down.
///     Compiling a markup document's logic block is Roslyn, which is two orders of magnitude
///     above every other step in building a component and needs no graphics context - so it
///     does not have to happen on the frame a scene is opened.
/// </summary>
public class LogicPrecompileTests
{
    private readonly LoaderTestContext _context = new();

    private string LoadMarkup()
    {
        return _context.AssetProvider.Load<StringAsset, StringInfo>(new StringInfo
        {
            AssetInfo = new AssetInfo
            {
                Location = "Scenes/Layout/LoaderInterface.snx.xml",
                Storage = StorageLocation.Assembly
            }
        }).Value;
    }

    /// <summary>
    ///     Discovery finds the documents rather than being handed a list, so a screen added
    ///     later is covered by existing. If the resource suffix or the embedding ever changes
    ///     this returns zero and the warm-up silently stops being worth anything.
    /// </summary>
    [Fact]
    public void PrecompileLogic_FindsAndCompilesTheEmbeddedDocuments()
    {
        var compiled = new SundexContext(_context).PrecompileLogic();
        Assert.True(compiled > 0, "no markup documents were discovered in the asset assemblies");
    }

    /// <summary>
    ///     The whole point: the script the precompile cached has to be the one the real build
    ///     then finds. A cache key that does not match means the compile was paid twice and
    ///     the warm-up bought nothing - which no other test would notice, since building
    ///     works either way.
    /// </summary>
    [Fact]
    public void APrecompiledDocument_StillBuildsAndWiresCorrectly()
    {
        var markup = LoadMarkup();
        Assert.True(new SundexContext(_context).PrecompileLogic(markup), "the loader's markup declared no logic");

        // The constructor runs RunLogicAndVerify, which throws if any [SetFromLogic]
        // member is still null - so a mis-keyed or wrongly bound script fails right here.
        var loader = new LoaderInterface(_context);
        Assert.NotNull(loader.RootPanel);
        Assert.NotNull(loader.StatusMessage);
    }

    /// <summary>
    ///     The precompile runs on a worker while the render thread may reach the same source
    ///     first. Both arriving at once must produce one compile and one working component,
    ///     not a torn cache.
    /// </summary>
    [Fact]
    public async Task Precompiling_WhileTheSameSourceIsBuilt_IsSafe()
    {
        var markup = LoadMarkup();

        var precompile = Task.Run(() =>
        {
            for (var i = 0; i < 4; i++) new SundexContext(_context).PrecompileLogic(markup);
        });

        var built = new LoaderInterface(_context);
        await precompile;

        Assert.NotNull(built.RootPanel);
        Assert.NotNull(new LoaderInterface(_context).RootPanel);
    }

    /// <summary>Markup with no logic block is not an error, it is just nothing to do.</summary>
    [Fact]
    public void PrecompileLogic_IgnoresMarkupItCannotUse()
    {
        var context = new SundexContext(_context);
        Assert.False(context.PrecompileLogic("<sundex version=\"1.0\"><layout><panel id=\"a\"/></layout></sundex>"));
        Assert.False(context.PrecompileLogic("not xml at all"));
    }
}
