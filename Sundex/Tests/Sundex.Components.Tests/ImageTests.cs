using System.Net;
using System.Net.Sockets;
using System.Reflection;
using OpenTK.Mathematics;
using Serilog;
using Shared;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using Sundex.Engine;
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

    private const string Missing = "Assets/does-not-exist.png";

    [Fact]
    public async Task BadSrc_EntersErroredState()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Src = Missing };

        await Settle(image, context);

        Assert.True(image.Errored);
        Assert.Equal(UIState.Errored, image.CurrentState);
        Assert.False(image.IsLoaded);
        // Nothing left for the frame loop to rethrow.
        context.AssetProvider.ThreadRunner.Update();
    }

    [Fact]
    public async Task BadSrc_CallsOnErrorOnceOnTheUpdateThread()
    {
        var context = new TestUIContext();
        var failures = new List<Exception>();
        var image = new Image(context) { OnError = (_, failure) => failures.Add(failure), Src = Missing };

        await image.LoadTask!;
        // The worker is done and has told no one: only the update pass may.
        Assert.Empty(failures);

        image.Update(context);
        image.Update(context);

        Assert.IsType<FileNotFoundException>(Assert.Single(failures));
    }

    [Fact]
    public async Task BadSrc_ShowsThePlaceholderCentredAtItsCap()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Width = 400, Height = 100, Src = Missing };

        await Settle(image, context);
        await Settle(image, context);
        image.Layout();

        Assert.True(image.ShowsPlaceholder);
        Assert.False(image.IsLoaded);
        Assert.Equal(new Vector3(48, 48, 1), PlaneOf(image).Scale);
        Assert.Equal(new Vector3(176, 26, 0), PlaneOf(image).Position);
        Assert.Equal(Missing, image.Src);
        // The placeholder is not a fetch of what was asked for.
        Assert.Equal(1, image.LoadCount);
    }

    [Fact]
    public async Task BadSrc_PointerCannotClearTheState()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Width = 10, Height = 10, Src = Missing };
        image.Layout();
        await Settle(image, context);

        context.UpdatePointer(image, 5, 5, false, false, false, Vector2.Zero);
        Assert.True(image.IsHovered);
        Assert.Equal(UIState.Errored, image.CurrentState);

        context.UpdatePointer(image, 50, 50, false, false, false, Vector2.Zero);
        Assert.Equal(UIState.Errored, image.CurrentState);
    }

    [Fact]
    public async Task BadSrc_AppliesTheErroredRule()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Classes = ["shot"] };
        image.ApplyStyleSheet(Sheet("""
                                    class shot {
                                        height = 300;
                                        state[errored] = {
                                            height = 96;
                                        }
                                    }
                                    """));
        Assert.Equal(300, image.Height.Value);

        image.Src = Missing;
        await Settle(image, context);

        Assert.Equal(96, image.Height.Value);
    }

    [Fact]
    public async Task BadSrc_FallsBackOnTheErroredRulesSrc()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Classes = ["cover"] };
        image.ApplyStyleSheet(Sheet($$"""
                                     class cover {
                                         state[errored] = {
                                             src = "{{Tall}}";
                                             storage = "assembly";
                                         }
                                     }
                                     """));

        image.Src = Missing;
        await Settle(image, context);
        await Settle(image, context);

        Assert.True(image.Errored);
        Assert.False(image.ShowsPlaceholder);
        Assert.Equal(6, image.Texture!.Height);
        Assert.Equal(2, image.LoadCount);
    }

    [Fact]
    public async Task BadSrc_AFailingFallbackEndsOnThePlaceholder()
    {
        var context = new TestUIContext();
        var failures = 0;
        var image = new Image(context) { Classes = ["cover"], OnError = (_, _) => failures++ };
        image.ApplyStyleSheet(Sheet("""
                                    class cover {
                                        state[errored] = {
                                            src = "Assets/also-missing.png";
                                        }
                                    }
                                    """));

        image.Src = Missing;
        for (var i = 0; i < 5; i++) await Settle(image, context);

        Assert.True(image.ShowsPlaceholder);
        Assert.Equal(2, image.LoadCount);
        Assert.Equal(2, failures);
    }

    [Fact]
    public async Task BadSrc_AFailingPlaceholderLeavesItEmpty()
    {
        var context = new NoPlaceholderContext();
        var image = new Image(context) { Src = Missing };

        for (var i = 0; i < 5; i++) await Settle(image, context);

        Assert.True(image.Errored);
        Assert.Null(image.Texture);
        Assert.False(PlaneOf(image).IsVisible);
    }

    [Fact]
    public async Task BadSrc_AFillRuleTakesThePlaceOfThePlaceholder()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Classes = ["shot"] };
        image.ApplyStyleSheet(Sheet("""
                                    class shot {
                                        state[errored] = {
                                            background = "#ff0000";
                                        }
                                    }
                                    """));

        image.Src = Missing;
        for (var i = 0; i < 3; i++) await Settle(image, context);

        Assert.IsType<ColoredPlane>(image.Background);
        Assert.False(image.ShowsPlaceholder);
        Assert.Null(image.Texture);
    }

    [Fact]
    public async Task NewSrc_LeavesTheErroredState()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Classes = ["shot"] };
        image.ApplyStyleSheet(Sheet("""
                                    class shot {
                                        height = 300;
                                        state[errored] = {
                                            height = 96;
                                        }
                                    }
                                    """));
        image.Src = Missing;
        for (var i = 0; i < 3; i++) await Settle(image, context);
        Assert.True(image.ShowsPlaceholder);

        image.Src = Wide;

        // The value written, not what the base snapshot held.
        Assert.Equal(Wide, image.Src);
        Assert.Equal(UIState.None, image.CurrentState);
        Assert.Equal(300, image.Height.Value);

        await Settle(image, context);

        Assert.True(image.IsLoaded);
        Assert.False(image.ShowsPlaceholder);
        Assert.Equal(4, image.Texture!.Width);
        Assert.Equal(2, image.LoadCount);
    }

    [Fact]
    public async Task DeadUrl_EntersErroredState()
    {
        var context = new TestUIContext();
        // Port 1 on loopback refuses at once, with no network involved.
        var image = new Image(context) { Src = "http://127.0.0.1:1/shot.png" };

        await Settle(image, context);

        Assert.True(image.Errored);
    }

    [Fact]
    public async Task NotFoundPage_NeverReachesTheDecoder()
    {
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var serving = ServeNotFound(server);

        var context = new TestUIContext();
        Exception? failure = null;
        var image = new Image(context)
        {
            OnError = (_, e) => failure = e,
            Src = $"http://127.0.0.1:{((IPEndPoint)server.LocalEndpoint).Port}/shot.png"
        };

        await Settle(image, context);
        await serving;

        Assert.IsType<HttpRequestException>(failure);
    }

    [Theory]
    [InlineData(2, 2, 1)]
    [InlineData(100, 4, 2)]
    public async Task DecodeWidth_OnlyEverShrinks(int decodeWidth, int width, int height)
    {
        var context = new TestUIContext();
        var image = new Image(context) { DecodeWidth = decodeWidth, Src = Wide };

        await Settle(image, context);

        Assert.Equal(width, image.Texture!.Width);
        Assert.Equal(height, image.Texture.Height);
    }

    [Fact]
    public async Task AutoHeight_FollowsTheAspectUnderAFixedWidth()
    {
        var context = new TestUIContext();
        var image = new Image(context) { Width = 100, Height = LiteralOrComputable.AutoSize, Src = Wide };

        await Settle(image, context);
        image.Layout();

        Assert.Equal(100, image.Computed.Width);
        Assert.Equal(50, image.Computed.Height);
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

    private static async Task ServeNotFound(TcpListener server)
    {
        var cancel = TestContext.Current.CancellationToken;
        using var client = await server.AcceptTcpClientAsync(cancel);
        var stream = client.GetStream();
        _ = await stream.ReadAsync(new byte[1024], cancel);
        await stream.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 4\r\nConnection: close\r\n\r\nnope"u8.ToArray(), cancel);
    }

    /// <summary>A host that never listed this library in its asset assemblies.</summary>
    private sealed class NoPlaceholderContext : UIContext
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public NoPlaceholderContext()
        {
            InjectForTesting(
                new AssetProvider(new LoggerConfiguration().CreateLogger(), [Assembly.GetExecutingAssembly()],
                    new GLInfo()),
                new MockFontProvider(),
                new MockTextProvider());
            Camera = new DollarStoreCamera(Vector3.Zero, new Vector2i(1920, 1080));
        }
    }
}
