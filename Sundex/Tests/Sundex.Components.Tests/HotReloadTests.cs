using System.Reflection;
using Serilog;
using Sundex.Engine;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Asset_Management.Types.String;
using StringInfo = Sundex.Engine.Asset_Management.Types.String.StringInfo;

namespace Sundex.Components.Tests;

/// <summary>
///     The two halves of hot reload that live outside the UI tree: resolving an asset to the
///     file being edited rather than the copy compiled into the assembly, and deciding what a
///     saved file is worth reloading.
/// </summary>
public class HotReloadTests
{
    private static AssetProvider Provider()
    {
        return new AssetProvider(new LoggerConfiguration().CreateLogger(),
            [Assembly.GetExecutingAssembly()], new GLInfo());
    }

    [Fact]
    public void DebugBuildsCarryTheirProjectDirectory()
    {
        // Stamped by Directory.Build.props. Without it every asset resolves to the embedded
        // copy and nothing below can work; this is the check that fails if that file is
        // dropped or its Debug condition stops matching.
        var roots = Provider().SourceRoots;

        Assert.NotEmpty(roots);
        Assert.All(roots, root => Assert.True(Directory.Exists(root)));
        Assert.Contains(roots, root => File.Exists(Path.Combine(root, "HotReloadTests.cs")));
    }

    [Fact]
    public void AProjectRelativeLocationResolvesUnderASourceRoot()
    {
        var provider = Provider();
        var found = AssetLoader.FindInSourceRoots("Layouts/HotReloadProbe.txt", provider.SourceRoots);

        Assert.NotNull(found);
        Assert.True(File.Exists(found));
    }

    [Fact]
    public void LoadingReadsTheFileOnDiskRatherThanTheEmbeddedCopy()
    {
        // The embedded copy is whatever this file said at build time; the disk copy is
        // written here and now. Reading back what was just written is the whole mechanism.
        var provider = Provider();
        var root = provider.SourceRoots.First(r => Directory.Exists(Path.Combine(r, "Layouts")));
        var path = Path.Combine(root, "Layouts", "HotReloadProbe.txt");

        var original = File.ReadAllText(path);
        var edited = $"edited {Guid.NewGuid()}";
        try
        {
            File.WriteAllText(path, edited);
            var loaded = provider.Load<StringAsset, StringInfo>(
                StringInfo.CreateFromUnknownStorage("Layouts/HotReloadProbe.txt"));

            Assert.Equal(edited, loaded.Value);
        }
        finally
        {
            File.WriteAllText(path, original);
        }
    }

    [Fact]
    public void AnAbsoluteOrMissingLocationIsNotResolvedFromSource()
    {
        var roots = Provider().SourceRoots;

        Assert.Null(AssetLoader.FindInSourceRoots("Layouts/NotHere.txt", roots));
        Assert.Null(AssetLoader.FindInSourceRoots(Path.GetFullPath("Layouts/HotReloadProbe.txt"), roots));
        Assert.Null(AssetLoader.FindInSourceRoots("Layouts/HotReloadProbe.txt", []));
    }

    [Fact]
    public async Task SavingAStylesheetAsksForAStyleReload()
    {
        Assert.Equal(ReloadScope.Styles, await ScopeAfterSaving("Panels.snx.ss"));
    }

    [Fact]
    public async Task SavingMarkupAsksForAFullReload()
    {
        Assert.Equal(ReloadScope.Full, await ScopeAfterSaving("Home.snx.xml"));
    }

    [Fact]
    public async Task ASaveTouchingBothIsOneFullReload()
    {
        // Save-all writes several files in a burst. It has to come out as a single reload,
        // and as the wider of the two scopes - a rebuild covers the sheet, not the reverse.
        var fired = await ScopesAfterSaving("Panels.snx.ss", "Home.snx.xml", "Theme.snx.ss");

        Assert.Equal([ReloadScope.Full], fired);
    }

    private static async Task<ReloadScope> ScopeAfterSaving(params string[] files)
    {
        return (await ScopesAfterSaving(files)).Single();
    }

    private static async Task<List<ReloadScope>> ScopesAfterSaving(params string[] files)
    {
        var directory = Directory.CreateTempSubdirectory("sundex-hot-reload");
        var fired = new List<ReloadScope>();
        var previous = HotReload.Requested;

        try
        {
            HotReload.Requested = scope =>
            {
                lock (fired) fired.Add(scope);
            };

            using var watcher = new SourceWatcher(new LoggerConfiguration().CreateLogger(),
                [directory.FullName]);

            foreach (var file in files)
                await File.WriteAllTextAsync(Path.Combine(directory.FullName, file), "changed");

            // Comfortably past the watcher's debounce, so a batch has coalesced and fired.
            await Task.Delay(TimeSpan.FromSeconds(2));

            lock (fired) return [..fired];
        }
        finally
        {
            HotReload.Requested = previous;
            directory.Delete(true);
        }
    }
}
