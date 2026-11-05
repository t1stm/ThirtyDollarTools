using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Markdown;
using Sundex.Components.Panels;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Style.DSL;

namespace Sundex.Components.Tests;

/// <summary>
///     What <see cref="MarkdownView" /> builds and how it follows its width. MockGlyphProvider's
///     characters are 0.6 em wide, so at the labels' default 16 px each is 9.6 px across.
/// </summary>
public class MarkdownViewTests
{
    private const string Long = "one two three four five six seven eight nine ten eleven twelve thirteen fourteen";

    private static (MarkdownView View, TestUIContext Context) View(string markdown, float width = 400)
    {
        var context = new TestUIContext();
        var view = new MarkdownView(context) { Width = width, Height = 100, Markdown = markdown };
        view.Layout();
        return (view, context);
    }

    private static IEnumerable<UIElement> Descendants(UIElement element)
    {
        yield return element;
        if (element is not Panel panel) yield break;
        foreach (var descendant in panel.Children.SelectMany(Descendants)) yield return descendant;
    }

    private static string Text(UIElement root)
    {
        return string.Concat(Descendants(root).OfType<Label>().Select(label => label.Value.TrimEnd('\0').ToString()));
    }

    [Fact]
    public void Paragraph_IsOneLabel_InTheBodyFont()
    {
        var (view, _) = View("hello there");

        var label = Assert.IsType<Label>(Assert.Single(view.Children));
        Assert.Equal(["md-p"], label.Classes);
        Assert.Equal("hello there", label.Value.ToString());
    }

    [Theory]
    [InlineData("# a", "md-h1")]
    [InlineData("## a", "md-h2")]
    [InlineData("### a", "md-h3")]
    [InlineData("###### a", "md-h3")]
    public void Heading_TakesItsClass(string markdown, string expected)
    {
        var (view, _) = View(markdown);
        Assert.Equal([expected], Assert.IsType<Label>(Assert.Single(view.Children)).Classes);
    }

    [Fact]
    public void Paragraph_WrapsToTheInnerWidth_AndFollowsAResize()
    {
        var (view, _) = View(Long);
        var label = Assert.IsType<Label>(view.Children[0]);
        var narrow = label.Height.Value;
        Assert.True(narrow > 19.2f, "it should take more than one line");

        view.Width = 800;
        view.InvalidateLayout();
        view.Layout();

        Assert.True(label.Height.Value < narrow, "a wider view should take fewer lines");
    }

    [Fact]
    public void WrappedHeight_ReachesTheScrollRange()
    {
        var (view, _) = View(string.Join(' ', Enumerable.Repeat(Long, 20)));

        Assert.Equal(view.Children[0].Height.Value, view.ContentHeight);
        Assert.Equal(view.ContentHeight - 100, view.MaxScroll);
        Assert.True(view.MaxScroll > 0);

        view.Width = 200;
        view.InvalidateLayout();
        var before = view.MaxScroll;
        view.Layout();

        Assert.True(view.MaxScroll > before);
    }

    [Fact]
    public void LinkParagraph_IsOneElementPerWord_KeepingTheSpaces()
    {
        var (view, _) = View("see [the docs](https://a.example/x) now");

        var flow = Assert.IsType<FlexPanel>(Assert.Single(view.Children));
        Assert.True(flow.Wrap);
        Assert.Equal("see the docs now", Text(flow));

        var links = Descendants(flow).OfType<Link>().ToList();
        Assert.Equal(["the ", "docs"], links.Select(link => link.Value.ToString()));
        Assert.All(links, link => Assert.Equal("https://a.example/x", link.Url));
    }

    [Fact]
    public void LinkParagraph_WrapsWithinTheView()
    {
        var (view, _) = View($"{Long} [link](https://a.example/x) {Long}", 300);

        var flow = Assert.IsType<FlexPanel>(view.Children[0]);
        view.Layout();

        foreach (var word in flow.Children) Assert.True(word.Computed.X + word.Computed.Width <= flow.Computed.Width + 0.01f);
        Assert.True(flow.Computed.Height > 19.2f * 2, "it should take several lines");
        Assert.Equal(flow.Computed.Height, view.ContentHeight);
    }

    [Fact]
    public void Link_AWordTouchingTheNextStaysOnItsLine()
    {
        var (view, _) = View("[RC2](https://a.example/2), [RC3](https://a.example/3).");

        var flow = Assert.IsType<FlexPanel>(view.Children[0]);
        // "RC2" and ", " together, then "RC3" and ".".
        Assert.Equal(2, flow.Children.Count);
        Assert.All(flow.Children, child => Assert.IsType<FlexPanel>(child));
        Assert.Equal("RC2, RC3.", Text(flow));
    }

    [Fact]
    public void HoveringOneWord_UnderlinesTheWholeLink()
    {
        var (view, context) = View("[the docs](https://a.example/x) and [more](https://a.example/y)");
        var links = Descendants(view).OfType<Link>().ToList();
        var first = links[0];

        context.UpdatePointer(view, first.Computed.AbsoluteX + 1, first.Computed.AbsoluteY + 1, false, false, false, Vector2.Zero);

        Assert.Equal([true, true, false], links.Select(link => link.Underlined));

        context.UpdatePointer(view, 399, 99, false, false, false, Vector2.Zero);

        Assert.All(links, link => Assert.False(link.Underlined));
    }

    [Fact]
    public void HoveringAWord_RequestsThePointer()
    {
        var (view, context) = View("[docs](https://a.example/x)");
        CursorType? requested = null;
        context.RequestCursor = type => requested = type;
        var link = Descendants(view).OfType<Link>().Single();

        context.UpdatePointer(view, link.Computed.AbsoluteX + 1, link.Computed.AbsoluteY + 1, false, false, false, Vector2.Zero);
        view.Update(context);

        Assert.Equal(CursorType.Pointer, requested);
    }

    [Theory]
    [InlineData("[x](file:///etc/passwd)")]
    [InlineData("[x](steam://run/1)")]
    [InlineData("[x](/usr/bin/xterm)")]
    public void NonWebLink_StaysALabel(string markdown)
    {
        var (view, _) = View(markdown);

        Assert.Empty(Descendants(view).OfType<Link>());
        Assert.IsType<Label>(Assert.Single(view.Children));
    }

    [Theory]
    [InlineData("""<img src="file:///etc/passwd" alt="x"/>""")]
    [InlineData("![x](/home/me/a.png)")]
    public void NonWebImage_StartsNoFetch(string markdown)
    {
        var (view, _) = View(markdown);

        Assert.Empty(Descendants(view).OfType<Image>());
    }

    [Fact]
    public void Bold_IsInItsOwnClass()
    {
        var (view, _) = View("a **b** c");

        var bold = Descendants(view).OfType<Label>().Single(label => label.Classes.Contains("md-strong"));
        Assert.Equal("b", bold.Value.TrimEnd(' ').ToString());
    }

    [Fact]
    public void Code_SitsOnAFill()
    {
        var (view, _) = View("a `b c` d");

        var code = Descendants(view).OfType<FlexPanel>().Single(panel => panel.Classes.Contains("md-code"));
        Assert.Contains("b c", Text(code));
    }

    [Fact]
    public void ListItems_GroupIntoOneList_MarkerBesideText()
    {
        var (view, _) = View($"- {Long}\n- two\n\nafter");

        var list = Assert.IsType<FlexPanel>(view.Children[0]);
        Assert.Equal(["md-list"], list.Classes);
        Assert.Equal(2, list.Children.Count);

        var row = Assert.IsType<FlexPanel>(list.Children[0]);
        var marker = Assert.IsType<Label>(row.Children[0]);
        var text = Assert.IsType<Label>(row.Children[1]);
        Assert.Equal("•", marker.Value.ToString());
        // Leaves the marker's room, or the text would run past the view's edge.
        Assert.True(text.WrapWidth < view.Computed.Width - marker.Width.Value);
        Assert.IsType<Label>(view.Children[1]);
    }

    [Fact]
    public void Heading_AfterTheFirstGetsAGapAbove()
    {
        var (view, _) = View("# a\n\nb\n\n## c");

        Assert.Equal(4, view.Children.Count);
        Assert.Contains("md-heading-gap", view.Children[2].Classes);
    }

    [Fact]
    public void Image_ReservesItsAttributesAspectUntilItLands()
    {
        var (view, _) = View("""<img width="1000" height="500" src="http://127.0.0.1:1/a.png"/>""");

        var frame = Assert.IsType<MarkdownImageFrame>(view.Children[0]);
        Assert.Equal(frame.Computed.Width / 2, frame.Computed.Height);
    }

    [Fact]
    public void Image_WithoutAttributesReserves16By9()
    {
        var (view, _) = View("![a](http://127.0.0.1:1/a.png)");

        var frame = Assert.IsType<MarkdownImageFrame>(view.Children[0]);
        Assert.Equal(frame.Computed.Width * 9 / 16, frame.Computed.Height);
    }

    [Fact]
    public void Image_StopsShortOfTheScrollBar()
    {
        var (view, _) = View("words\n\n![a](http://127.0.0.1:1/a.png)");

        var label = Assert.IsType<Label>(view.Children[0]);
        Assert.Equal(label.WrapWidth, view.Children[1].Computed.Width);
    }

    [Fact]
    public void Image_IsNeverWiderThanItsAttribute()
    {
        var (view, _) = View("""<img width="100" height="50" src="http://127.0.0.1:1/a.png"/>""");

        Assert.Equal(100, view.Children[0].Computed.Width);
    }

    [Fact]
    public void Image_FetchesFromTheNetworkOnly_DecodedAtItsShownWidth()
    {
        var (view, _) = View("""<img width="100" src="http://127.0.0.1:1/a.png"/>""");

        var image = Assert.IsType<MarkdownImageFrame>(view.Children[0]).Image;
        Assert.Equal(StorageLocation.Network, image.Storage);
        Assert.Equal(100, image.DecodeWidth);
        Assert.Equal(1, image.LoadCount);
    }

    [Fact]
    public async Task Image_OnAFailureFollowsTheSheetsHeight()
    {
        var context = new TestUIContext();
        var view = new MarkdownView(context) { Width = 400, Height = 100 };
        view.ApplyStyleSheet(new StyleSheet(StyleParser.Parse("""
                                                              class md-image {
                                                                  state[errored] = {
                                                                      height = 96;
                                                                  }
                                                              }
                                                              """)));
        view.Markdown = "![a](http://127.0.0.1:1/a.png)";
        view.Layout();

        var frame = Assert.IsType<MarkdownImageFrame>(view.Children[0]);
        await frame.Image.LoadTask!;
        view.Update(context);
        view.Layout();

        Assert.True(frame.Image.Errored);
        Assert.Equal(96, frame.Computed.Height);
        Assert.Equal(96, view.ContentHeight);
    }

    [Fact]
    public async Task Image_OnAFailureWithNoRuleKeepsItsReservedHeight()
    {
        var (view, context) = View("![a](http://127.0.0.1:1/a.png)");

        var frame = Assert.IsType<MarkdownImageFrame>(view.Children[0]);
        var reserved = frame.Computed.Height;
        await frame.Image.LoadTask!;
        view.Update(context);
        view.Layout();

        Assert.True(frame.Image.Errored);
        Assert.Equal(reserved, frame.Computed.Height);
    }

    [Fact]
    public void ChangingTheText_ReplacesTheOldContent()
    {
        var (view, _) = View("old [link](https://a.example)");

        view.Markdown = "new";
        view.Layout();

        Assert.Equal("new", Text(view));
    }
}
