using Shared.Updates;

namespace Shared.Tests;

/// <summary>
///     Which release the update note offers, given the build and the two channel opt-ins.
///     Only the picking is covered - the fetch around it is one HTTP call in a try/catch.
/// </summary>
public class UpdateCheckerTests
{
    private static readonly DateTimeOffset BuildDate = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly VersionInfo Stable = new("v1.1.6", "Release", false, false, BuildDate);

    private static readonly GitHubRelease[] Releases =
    [
        Release("v1.1.5", -3, false), // predates the build
        Release("v1.1.7", 1, false),
        Release("v1.1.8-prerelease-1", 2, true),
        Release("nightly-master", 3, true),
        Release("nightly-some-feature", 4, true)
    ];

    /// <param name="daysFromBuild">Release age relative to the build - negative is older.</param>
    private static GitHubRelease Release(string tag, int daysFromBuild, bool prerelease, bool draft = false)
    {
        return new GitHubRelease(tag, $"https://example.invalid/{tag}", prerelease, draft,
            BuildDate.AddDays(daysFromBuild));
    }

    [Fact]
    public void StableOnlySkipsPrereleasesAndNightlies()
    {
        Assert.Equal("v1.1.7", UpdateChecker.PickNewer(Releases, Stable, false, false)?.TagName);
    }

    [Fact]
    public void PrereleasesOptInTakesTheNewerPrerelease()
    {
        Assert.Equal("v1.1.8-prerelease-1", UpdateChecker.PickNewer(Releases, Stable, true, false)?.TagName);
    }

    [Fact]
    public void NightliesOptInTakesMasterButNeverAnotherBranch()
    {
        Assert.Equal("nightly-master", UpdateChecker.PickNewer(Releases, Stable, true, true)?.TagName);
    }

    [Fact]
    public void ANightlyBuildIsOfferedItsOwnBranch()
    {
        var build = new VersionInfo("nightly-some-feature", null, true, true, BuildDate);
        Assert.Equal("nightly-some-feature", UpdateChecker.PickNewer(Releases, build, true, true)?.TagName);
    }

    [Fact]
    public void NothingOlderThanTheBuildIsOffered()
    {
        GitHubRelease[] onlyOlder = [Release("v1.1.5", -3, false)];
        Assert.Null(UpdateChecker.PickNewer(onlyOlder, Stable, true, true));
    }

    [Fact]
    public void DraftsAreIgnored()
    {
        GitHubRelease[] drafted = [Release("v2.0.0", 5, false, true)];
        Assert.Null(UpdateChecker.PickNewer(drafted, Stable, true, true));
    }

    [Fact]
    public void ADeveloperBuildHasNothingToCompareAgainst()
    {
        var noDate = Stable with { Date = null };
        Assert.Null(UpdateChecker.PickNewer(Releases, noDate, true, true));
    }

    [Fact]
    public void NewerListsEveryAcceptedReleaseNewestFirst()
    {
        var newer = UpdateChecker.Newer(Releases, Stable, true, true);
        Assert.Equal(["nightly-master", "v1.1.8-prerelease-1", "v1.1.7"], newer.Select(release => release.TagName));
    }

    [Fact]
    public void TheNotesComeFromTheSameResponse()
    {
        const string json = """
                            [{"tag_name":"v2","html_url":"https://a","prerelease":false,"draft":false,
                              "created_at":"2026-10-21T00:00:00Z","name":"Two","body":"notes"},
                             {"tag_name":"v3","html_url":"https://b","prerelease":false,"draft":false,
                              "created_at":"2026-10-22T00:00:00Z","name":null,"body":null}]
                            """;

        var releases = System.Text.Json.JsonSerializer.Deserialize<GitHubRelease[]>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        })!;

        Assert.Equal("Two", releases[0].Name);
        Assert.Equal("notes", releases[0].Body);
        Assert.Null(releases[1].Body);
    }
}
