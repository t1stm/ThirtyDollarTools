using Shared.Updates;

namespace Shared.Tests;

/// <summary>What the update dialog makes of a release before Markdown ever sees it.</summary>
public class ReleaseNotesTests
{
    private static readonly DateTimeOffset Date = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static GitHubRelease Release(string? body, string tag = "v2.0.0-rc7", string? name = "Release Candidate 7")
    {
        return new GitHubRelease(tag, "https://example.invalid", false, false, Date, name, body);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n ")]
    public void AnEmptyBodySaysSo(string? body)
    {
        Assert.Equal(ReleaseNotes.NoNotes, ReleaseNotes.Prepare(Release(body)));
    }

    [Fact]
    public void CommitAndPullAddressesAreShortenedAsGitHubDoes()
    {
        const string commit = "https://github.com/t1stm/ThirtyDollarTools/commit/512987c0c8b465ea9d758861c2ef838baa201c3e";
        const string pull = "https://github.com/t1stm/ThirtyDollarTools/pull/61";

        Assert.Equal($"- [512987c]({commit}): a by b", ReleaseNotes.Prepare(Release($"- {commit}: a by b")));
        Assert.Equal($"- Pull Request [#61]({pull}): a", ReleaseNotes.Prepare(Release($"- Pull Request {pull}: a")));
    }

    [Fact]
    public void AnAddressAlreadyLinkedIsLeftAlone()
    {
        const string body = "[the fix](https://github.com/t1stm/ThirtyDollarTools/pull/61)";
        Assert.Equal(body, ReleaseNotes.Prepare(Release(body)));
    }

    [Theory]
    [InlineData("feat: a (#61) by b", "feat: a ([#61](https://github.com/t1stm/ThirtyDollarTools/pull/61)) by b")]
    [InlineData("`!bg@#2087ee,0`", "`!bg@#2087ee,0`")]
    [InlineData("#define is here", "#define is here")]
    public void OnlyARealReferenceBecomesALink(string body, string expected)
    {
        Assert.Equal(expected, ReleaseNotes.Prepare(Release(body)));
    }

    [Fact]
    public void CarriageReturnsAreGone()
    {
        Assert.Equal("a\n\nb", ReleaseNotes.Prepare(Release("a\r\n\r\nb")));
    }

    [Fact]
    public void ANightlyKeepsItsFirstEightItemsAndCountsTheRest()
    {
        var items = string.Join('\n', Enumerable.Range(1, 11).Select(i => $"- item {i}"));
        var notes = ReleaseNotes.Prepare(Release($"intro\n## Commits:\n{items}", "nightly-master"));

        Assert.Contains("- item 8", notes);
        Assert.DoesNotContain("- item 9", notes);
        Assert.EndsWith("and 3 more on the release page", notes);
    }

    [Fact]
    public void AStableReleaseKeepsEveryItem()
    {
        var items = string.Join('\n', Enumerable.Range(1, 11).Select(i => $"- item {i}"));
        Assert.Contains("- item 11", ReleaseNotes.Prepare(Release(items)));
    }

    [Fact]
    public void OnlyOlderReleasesGetAHead()
    {
        var notes = ReleaseNotes.Combine([Release("newest"), Release("older", "v2.0.0-rc6", "Release Candidate 6")]);

        Assert.StartsWith("newest", notes);
        Assert.Contains("# Release Candidate 6\n\nv2.0.0-rc6 · 21 Oct 2026\n\nolder", notes);
        Assert.DoesNotContain("# Release Candidate 7", notes);
    }

    [Fact]
    public void ATitleFallsBackOnTheTag()
    {
        Assert.Equal("v2.0.0-rc7", ReleaseNotes.Title(Release("a", name: " ")));
    }

    [Theory]
    [InlineData(1, 72, "1 release, 72 days newer")]
    [InlineData(2, 1, "2 releases, 1 day newer")]
    [InlineData(2, 0, "2 releases, less than a day newer")]
    public void TheGainSaysHowFarAhead(int releases, int days, string expected)
    {
        var build = new VersionInfo("v1", null, false, false, Date.AddDays(-days));
        Assert.Equal(expected, ReleaseNotes.Gain(Enumerable.Repeat(Release("a"), releases).ToList(), build));
    }
}
