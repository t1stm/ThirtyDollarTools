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
}
