using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Renderer.Textures;
using Sundex.Style.DSL;

namespace Sundex.Components.Tests;

/// <summary>
///     The <c>&lt;image&gt;</c> element: where its texture comes from, and what happens when
///     any of its settings changes after the first load - from code or from a stylesheet.
/// </summary>
public class ImageTests
{
    private const string Wide = "Assets/wide-4x2.png"; // 4x2, aspect 2:1
    private const string Tall = "Assets/tall-2x6.png"; // 2x6, aspect 1:3

    /// <summary>
    ///     Solid colour, so it is tiny on disk but has four million pixels to decode - which
    ///     makes it reliably the slower of two concurrent loads.
    /// </summary>
    private const string Slow = "Assets/slow-2000x2000.png";

    private static TexturedPlane PlaneOf(Image image)
    {
        return (TexturedPlane)image.Background!;
    }

    /// <summary>Runs a pending fetch to completion and hands the result to the element.</summary>
    private static async Task Settle(Image image, UIContext context)
    {
        if (image.LoadTask is { } task) await task;
        // Rethrows anything the worker threw, so a broken load fails the test loudly.
        context.AssetProvider.ThreadRunner.Update();
        image.Update(context);
    }

    private static StyleSheet Sheet(string dsl)
    {
        return new StyleSheet(StyleParser.Parse(dsl));
    }

    [Fact]
    public async Task Src_FromCode_LoadsTheTexture()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Wide };

        await Settle(image, context);

        Assert.True(image.IsLoaded);
        Assert.Equal(4, image.Texture!.Width);
        Assert.Equal(2, image.Texture.Height);
        Assert.True(PlaneOf(image).IsVisible);
    }

    [Fact]
    public void Src_BeforeItLoads_LeavesThePlaneUndrawn()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Wide };

        // TexturedPlane.Render binds Texture only if it has one; drawing before the fetch
        // lands would paint with whatever texture was bound last.
        Assert.False(PlaneOf(image).IsVisible);
        Assert.Null(PlaneOf(image).Texture);
    }

    [Fact]
    public async Task Src_FromAStyleSheet_LoadsTheTexture()
    {
        var context = new TestUIContext();
        var image = new Image(context);

        image.ApplyStyleSheet(Sheet($$"""
                                     component image {
                                         src = "{{Wide}}";
                                         storage = "assembly";
                                         texture-fit = "fit";
                                     }
                                     """));

        await Settle(image, context);

        Assert.Equal(StorageLocation.Assembly, image.Storage);
        Assert.Equal(TextureFit.Fit, image.TextureFit);
        Assert.Equal(4, image.Texture!.Width);
    }

    [Fact]
    public async Task Src_FromASecondStyleSheet_ReloadsTheTexture()
    {
        var context = new TestUIContext();
        var image = new Image(context);

        image.ApplyStyleSheet(Sheet($"component image {{ src = \"{Wide}\"; }}"));
        await Settle(image, context);
        Assert.Equal(4, image.Texture!.Width);

        image.ApplyStyleSheet(Sheet($"component image {{ src = \"{Tall}\"; }}"));
        await Settle(image, context);

        Assert.Equal(2, image.Texture!.Width);
        Assert.Equal(6, image.Texture.Height);
    }

    [Fact]
    public async Task Src_FromAStyleClass_LoadsTheTexture()
    {
        var context = new TestUIContext();
        var image = new Image(context);
        image.Classes.Add("thumb");

        image.ApplyStyleSheet(Sheet($"class thumb {{ src = \"{Tall}\"; }}"));
        await Settle(image, context);

        Assert.Equal(6, image.Texture!.Height);
    }

    [Fact]
    public async Task Src_WrittenTwiceByOneStylePass_CostsOneFetch()
    {
        var context = new TestUIContext();
        var image = new Image(context) { ID = "hero" };
        image.Classes.Add("thumb");

        // The class rule is applied first and the id rule overrides it; each is a separate
        // write to Src, and reflection order is not declaration order either - so the fetch
        // has to wait for the pass to finish rather than fire per write.
        image.ApplyStyleSheet(Sheet($$"""
                                     class thumb { src = "{{Wide}}"; }
                                     id hero { src = "{{Tall}}"; storage = "assembly"; }
                                     """));
        await Settle(image, context);

        Assert.Equal(1, image.LoadCount);
        Assert.Equal(2, image.Texture!.Width);
    }

    [Fact]
    public async Task Src_ChangedAtRuntime_ReloadsTheTexture()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Wide };
        await Settle(image, context);

        var first = image.Texture;
        image.Src = Tall;
        await Settle(image, context);

        Assert.NotSame(first, image.Texture);
        Assert.Equal(2, image.Texture!.Width);
        Assert.Equal(6, image.Texture.Height);
    }

    [Fact]
    public async Task Src_SetToTheSameValue_DoesNotReload()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Wide };
        await Settle(image, context);

        var first = image.Texture;
        image.Src = Wide;
        await Settle(image, context);

        Assert.Same(first, image.Texture);
    }

    [Fact]
    public async Task Src_ClearedAtRuntime_DropsTheTexture()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Wide };
        await Settle(image, context);

        image.Src = string.Empty;

        Assert.False(image.IsLoaded);
        Assert.Null(PlaneOf(image).Texture);
        Assert.False(PlaneOf(image).IsVisible);
    }

    [Fact]
    public async Task Storage_ChangedAtRuntime_ReloadsTheTexture()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Wide };
        await Settle(image, context);
        var first = image.Texture;

        // Same src, different resolution path: the fetch has to run again.
        image.Storage = StorageLocation.Assembly;
        await Settle(image, context);

        Assert.NotSame(first, image.Texture);
        Assert.Equal(4, image.Texture!.Width);
    }

    [Fact]
    public async Task Src_ChangedTwiceBeforeSettling_KeepsTheLastOne()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Wide };

        // The first fetch is still in flight; its result must be dropped rather than
        // landing on top of the newer one.
        image.Src = Tall;
        await Settle(image, context);

        Assert.Equal(2, image.Texture!.Width);
        Assert.Equal(6, image.Texture.Height);
    }

    [Fact]
    public async Task Src_ChangedWhileASlowLoadIsStillRunning_DropsTheStaleResult()
    {
        var context = new TestUIContext();

        var image = new Image(context) { Src = Slow };
        var slow = image.LoadTask!;
        image.Src = Tall;
        var fast = image.LoadTask!;

        // Both workers have finished by now and the slow one finished last, so without the
        // generation check its four-megapixel result is what would be sitting there.
        await Task.WhenAll(slow, fast);
        context.AssetProvider.ThreadRunner.Update();
        image.Update(context);

        Assert.Equal(2, image.Texture!.Width);
        Assert.Equal(6, image.Texture.Height);
    }

    [Fact]
    public async Task TextureFit_Stretch_FillsTheBox()
    {
        var context = new TestUIContext();
        var image = new Image(context)
        {
            Width = 100,
            Height = 100,
            Src = Wide
        };

        await Settle(image, context);
        image.Layout();

        Assert.Equal(new Vector3(100, 100, 1), PlaneOf(image).Scale);
    }

    [Fact]
    public async Task TextureFit_Fit_LetterboxesAndCentresInsideTheBox()
    {
        var context = new TestUIContext();
        var image = new Image(context)
        {
            Width = 100,
            Height = 100,
            TextureFit = TextureFit.Fit,
            Src = Wide
        };

        await Settle(image, context);
        image.Layout();

        // 4x2 into 100x100: width-bound, so 100x50 centred vertically.
        Assert.Equal(new Vector3(100, 50, 1), PlaneOf(image).Scale);
        Assert.Equal(new Vector3(0, 25, 0), PlaneOf(image).Position);
    }

    [Fact]
    public async Task TextureFit_Fit_HeightBound_LetterboxesHorizontally()
    {
        var context = new TestUIContext();
        var image = new Image(context)
        {
            Width = 120,
            Height = 60,
            TextureFit = TextureFit.Fit,
            Src = Tall
        };

        await Settle(image, context);
        image.Layout();

        // 2x6 into 120x60: height-bound, so 20x60 centred horizontally.
        Assert.Equal(new Vector3(20, 60, 1), PlaneOf(image).Scale);
        Assert.Equal(new Vector3(50, 0, 0), PlaneOf(image).Position);
    }

    [Fact]
    public async Task TextureFit_ChangedAtRuntime_RelaysOutWithoutReloading()
    {
        var context = new TestUIContext();
        var image = new Image(context)
        {
            Width = 100,
            Height = 100,
            Src = Wide
        };

        await Settle(image, context);
        image.Layout();
        var texture = image.Texture;

        image.TextureFit = TextureFit.Fit;
        image.Layout();

        Assert.Same(texture, image.Texture);
        Assert.Equal(new Vector3(100, 50, 1), PlaneOf(image).Scale);

        image.TextureFit = TextureFit.Stretch;
        image.Layout();

        Assert.Equal(new Vector3(100, 100, 1), PlaneOf(image).Scale);
    }

    [Fact]
    public async Task TextureFit_FromAStyleSheet_AppliesWithoutReloading()
    {
        var context = new TestUIContext();
        var image = new Image(context)
        {
            Width = 100,
            Height = 100,
            Src = Wide
        };

        await Settle(image, context);
        var texture = image.Texture;

        image.ApplyStyleSheet(Sheet("component image { texture-fit = \"fit\"; }"));
        image.Layout();

        Assert.Same(texture, image.Texture);
        Assert.Equal(new Vector3(100, 50, 1), PlaneOf(image).Scale);
    }

    [Fact]
    public async Task AutoSize_TakesTheTexturesPixelSize()
    {
        var context = new TestUIContext();
        var image = new Image(context)
        {
            Width = LiteralOrComputable.AutoSize,
            Height = LiteralOrComputable.AutoSize
        };

        image.Layout();
        Assert.Equal(0, image.Computed.Width);

        image.Src = Tall;
        await Settle(image, context);

        // The load lands off the layout pass; the element has to re-run it itself.
        Assert.Equal(2, image.Computed.Width);
        Assert.Equal(6, image.Computed.Height);
    }

    [Fact]
    public async Task LoadedTexture_ResizesItsParentsLayout()
    {
        var context = new TestUIContext();
        var stack = new StackPanel(context) { Direction = LayoutDirection.Vertical };
        var image = new Image(context)
        {
            Width = LiteralOrComputable.AutoSize,
            Height = LiteralOrComputable.AutoSize
        };
        var below = new Panel(context) { Width = 10, Height = 10 };

        stack.AddChild(image);
        stack.AddChild(below);
        stack.DrawTo(context);

        image.Src = Tall;
        await Settle(image, context);

        // The relayout has to start at the root, or the stack keeps the sibling at the position
        // the image's pre-load zero height gave it.
        Assert.Equal(6, below.Computed.Y);
    }

    [Fact]
    public async Task BadSrc_SurfacesOnTheThreadRunner()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = "Assets/does-not-exist.png" };

        await image.LoadTask!;

        Assert.Throws<FileNotFoundException>(() => context.AssetProvider.ThreadRunner.Update());
    }

    [Fact]
    public void SetTexture_SwapsThePlanesTextureAndQueuesTheOldHandleForDeletion()
    {
        var context = new TestUIContext();
        var image = new Image(context);

        var first = new GPUTexture { Width = 8, Height = 8 };
        image.SetTexture(first);
        Assert.Same(first, PlaneOf(image).Texture);

        var second = new GPUTexture { Width = 4, Height = 4 };
        image.SetTexture(second);

        Assert.Same(second, PlaneOf(image).Texture);
        Assert.Same(second, image.Texture);
    }

    [Theory]
    [InlineData("https://example.com/a.png", true)]
    [InlineData("HTTP://example.com/a.png", true)]
    [InlineData("Assets/wide-4x2.png", false)]
    [InlineData("C:/http/a.png", false)]
    public void UnknownStorage_MatchesWebLocations(string location, bool isWeb)
    {
        Assert.Equal(isWeb, AssetLoader.IsWebLocation(location));
    }

    [Fact]
    public void UnknownStorage_QueryAcceptsAUrlThatIsNeitherOnDiskNorInAnAssembly()
    {
        var context = new TestUIContext();
        var provider = (AssetProvider)context.AssetProvider;

        Assert.True(provider.Query<AssetStream, AssetInfo>(new AssetInfo
        {
            Location = "https://example.com/definitely-not-here.png"
        }));
    }
}
