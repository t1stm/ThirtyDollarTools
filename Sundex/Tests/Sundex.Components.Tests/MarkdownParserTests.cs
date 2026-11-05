using System.Reflection;
using Sundex.Components.Markdown;

namespace Sundex.Components.Tests;

/// <summary>The parser, against small cases and against the real release bodies saved in Assets/Markdown.</summary>
public class MarkdownParserTests
{
    private static string Fixture(string name)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"Sundex.Components.Tests.Assets.Markdown.{name}.md")!;
        return new StreamReader(stream).ReadToEnd();
    }

    private static T Single<T>(string markdown) where T : MarkdownBlock
    {
        return Assert.IsType<T>(Assert.Single(MarkdownParser.Parse(markdown)));
    }

    private static IEnumerable<MarkdownInline> Inlines(MarkdownBlock block)
    {
        return block switch
        {
            MarkdownHeading heading => heading.Inlines,
            MarkdownParagraph paragraph => paragraph.Inlines,
            MarkdownListItem item => item.Inlines,
            MarkdownQuote quote => quote.Inlines,
            _ => []
        };
    }

    [Theory]
    [InlineData("v2.0.0-rc1")]
    [InlineData("v2.0.0-rc7")]
    [InlineData("v1.1.6")]
    [InlineData("nightly-master")]
    public void ARealBody_LeavesNoSyntaxBehind(string name)
    {
        var blocks = MarkdownParser.Parse(Fixture(name));

        Assert.NotEmpty(blocks);
        foreach (var inline in blocks.SelectMany(Inlines).Where(inline => !inline.Code))
        {
            Assert.DoesNotContain("<img", inline.Text);
            Assert.DoesNotContain("](", inline.Text);
            Assert.DoesNotContain("**", inline.Text);
            Assert.DoesNotContain("\r", inline.Text);
            if (inline.Url == null) Assert.DoesNotContain("https://", inline.Text);
        }
    }

    [Theory]
    [InlineData("v2.0.0-rc1", 11)]
    [InlineData("v2.0.0-rc7", 12)]
    [InlineData("v1.1.6", 7)]
    [InlineData("nightly-master", 0)]
    public void ARealBody_KeepsEveryImageWithItsUrl(string name, int count)
    {
        var body = Fixture(name);
        var images = MarkdownParser.Parse(body).OfType<MarkdownImage>().ToList();

        Assert.Equal(count, images.Count);
        foreach (var image in images) Assert.Contains(image.Url, body);
    }

    [Fact]
    public void ARealBody_HasItsHeadingsListsAndLinks()
    {
        var blocks = MarkdownParser.Parse(Fixture("v1.1.6"));

        Assert.Contains(blocks, block => block is MarkdownHeading { Level: 1 });
        Assert.Contains(blocks, block => block is MarkdownHeading { Level: 2 });
        Assert.Contains(blocks, block => block is MarkdownListItem { Marker: "•" });
        Assert.Contains(blocks.SelectMany(Inlines), inline => inline.Code);
        Assert.Contains(blocks.SelectMany(Inlines), inline => inline.Url != null);
    }

    [Fact]
    public void Nightly_EveryCommitIsALink()
    {
        var items = MarkdownParser.Parse(Fixture("nightly-master")).OfType<MarkdownListItem>().ToList();

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Contains(item.Inlines, inline => inline.Url != null));
    }

    [Theory]
    [InlineData("# One", 1)]
    [InlineData("## Two ##", 2)]
    [InlineData("###### Six", 6)]
    public void Heading_TakesItsLevel(string markdown, int level)
    {
        Assert.Equal(level, Single<MarkdownHeading>(markdown).Level);
    }

    [Fact]
    public void Hash_WithNoSpaceIsNotAHeading()
    {
        Single<MarkdownParagraph>("#define thing");
    }

    [Fact]
    public void Newline_IsALineBreak_AndABlankLineAParagraph()
    {
        var blocks = MarkdownParser.Parse("one\r\ntwo\r\n\r\nthree");

        Assert.Equal(2, blocks.Count);
        Assert.Equal("one\ntwo", Assert.Single(Inlines(blocks[0])).Text);
        Assert.Equal("three", Assert.Single(Inlines(blocks[1])).Text);
    }

    [Fact]
    public void ListItem_KeepsNumbersAndBullets()
    {
        var blocks = MarkdownParser.Parse("- a\n* b\n+ c\n2. d\n3) e\n");

        Assert.Equal(["•", "•", "•", "2.", "3."], blocks.Cast<MarkdownListItem>().Select(item => item.Marker));
    }

    [Fact]
    public void ListItem_ALineUnderItContinuesIt()
    {
        var item = Single<MarkdownListItem>("- one\n  two");
        Assert.Equal("one\ntwo", Assert.Single(item.Inlines).Text);
    }

    [Fact]
    public void Bold_IsItsOwnRun_AndAListStarIsNotBold()
    {
        var inlines = Single<MarkdownParagraph>("a **b** c").Inlines;
        Assert.Equal([new MarkdownInline("a "), new MarkdownInline("b", true), new MarkdownInline(" c")], inlines);

        Single<MarkdownListItem>("* a");
    }

    [Theory]
    [InlineData("*should*", "should")]
    [InlineData("_camera_ a", "camera a")]
    [InlineData("snake_case_name", "snake_case_name")]
    [InlineData("2 * 3 * 4", "2 * 3 * 4")]
    [InlineData(@"\*kept\*", "*kept*")]
    public void Italic_LosesItsMarkers_AndNothingElseDoes(string markdown, string text)
    {
        Assert.Equal(text, string.Concat(Single<MarkdownParagraph>(markdown).Inlines.Select(inline => inline.Text)));
    }

    [Fact]
    public void Code_KeepsItsMarkersOut()
    {
        var inlines = Single<MarkdownParagraph>("set `!bg@#2087ee,0|_pause=4` now").Inlines;
        Assert.Equal(new MarkdownInline("!bg@#2087ee,0|_pause=4", false, true), inlines[1]);
    }

    [Fact]
    public void Fence_KeepsItsLinesAsWritten()
    {
        var code = Single<MarkdownCodeBlock>("```\n**a**\n\n  b\n```");
        Assert.Equal("**a**\n\n  b", code.Code);
    }

    [Fact]
    public void Fence_NeverClosed_RunsToTheEnd()
    {
        Assert.Equal("a", Single<MarkdownCodeBlock>("```\na").Code);
    }

    [Fact]
    public void Quote_JoinsItsLines()
    {
        var quote = Single<MarkdownQuote>("> a\n> b");
        Assert.Equal("a\nb", Assert.Single(quote.Inlines).Text);
    }

    [Fact]
    public void Link_KeepsItsAddress()
    {
        var inlines = Single<MarkdownParagraph>("see [the docs](https://a.example/x), ok").Inlines;
        Assert.Equal(new MarkdownInline("the docs", false, false, "https://a.example/x"), inlines[1]);
        Assert.Equal(", ok", inlines[2].Text);
    }

    [Fact]
    public void BareAddress_IsALink_WithoutTheSentencesPunctuation()
    {
        var inlines = Single<MarkdownParagraph>("at https://a.example/x: yes").Inlines;
        Assert.Equal("https://a.example/x", inlines[1].Url);
        Assert.Equal(": yes", inlines[2].Text);
    }

    [Theory]
    [InlineData("[x](file:///etc/passwd)")]
    [InlineData("[x](steam://run/1)")]
    [InlineData("[x](C:\\Windows\\calc.exe)")]
    [InlineData("[x](/usr/bin/xterm)")]
    [InlineData("[x](javascript:alert(1))")]
    public void AnythingNotWeb_StaysPlainText(string markdown)
    {
        var inline = Assert.Single(Single<MarkdownParagraph>(markdown).Inlines);

        Assert.Null(inline.Url);
        Assert.StartsWith("x", inline.Text);
    }

    [Theory]
    [InlineData("""<img src="file:///etc/passwd" alt="secret"/>""")]
    [InlineData("""<img src="C:\Windows\a.png" alt="secret"/>""")]
    [InlineData("![secret](/home/me/a.png)")]
    [InlineData("![secret](steam://a.png)")]
    public void AnImageNotOnTheWeb_IsItsAltText(string markdown)
    {
        Assert.Equal("secret", Assert.Single(Single<MarkdownParagraph>(markdown).Inlines).Text);
    }

    [Fact]
    public void ImgTag_ReadsItsAttributes()
    {
        var image = Single<MarkdownImage>("""<img width="1650" height="760" alt="4-automation" src="https://a.example/x?a=1&amp;b=2" />""");

        Assert.Equal("https://a.example/x?a=1&b=2", image.Url);
        Assert.Equal("4-automation", image.Alt);
        Assert.Equal(1650, image.Width);
        Assert.Equal(760, image.Height);
    }

    [Fact]
    public void ImgTag_APercentWidthIsNotPixels()
    {
        Assert.Null(Single<MarkdownImage>("""<img width="50%" src="https://a.example/x"/>""").Width);
    }

    [Fact]
    public void Image_EndsTheParagraphAboveIt()
    {
        var blocks = MarkdownParser.Parse("text\n![image](https://a.example/x)\nmore");

        Assert.IsType<MarkdownParagraph>(blocks[0]);
        Assert.Equal("https://a.example/x", Assert.IsType<MarkdownImage>(blocks[1]).Url);
        Assert.Equal("more", Assert.Single(Inlines(blocks[2])).Text);
    }

    [Fact]
    public void OtherHtml_LosesItsTags_AndKeepsItsText()
    {
        Assert.Equal("a b", Assert.Single(Single<MarkdownParagraph>("<div>a <b>b</b></div><!-- gone -->").Inlines).Text);
    }

    [Fact]
    public void Table_IsPlainText()
    {
        Assert.Contains("| a | b |", Assert.Single(Single<MarkdownParagraph>("| a | b |\n|---|---|").Inlines).Text);
    }

    [Fact]
    public void Entities_Decode()
    {
        Assert.Equal("a & b", Assert.Single(Single<MarkdownParagraph>("a &amp; b").Inlines).Text);
    }

    [Fact]
    public void RunsOfSpaces_Collapse()
    {
        Assert.Equal("a b", Assert.Single(Single<MarkdownParagraph>("a   b").Inlines).Text);
    }

    [Fact]
    public void Nothing_IsNoBlocks()
    {
        Assert.Empty(MarkdownParser.Parse(" \n\n"));
    }
}
