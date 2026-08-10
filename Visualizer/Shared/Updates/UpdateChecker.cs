using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

namespace Shared.Updates;

/// <summary>One GitHub release, cut down to the fields the update notice needs.</summary>
public sealed record GitHubRelease(
    string TagName,
    string HtmlUrl,
    bool Prerelease,
    bool Draft,
    DateTimeOffset CreatedAt)
{
    /// <summary>The nightly workflow tags every branch's build "nightly-{branch}".</summary>
    public bool IsNightly => TagName.StartsWith("nightly-");
}

/// <summary>
///     Opt-in check of the repository's releases, started once by the loader and read by the
///     home screen. Fire and forget: a failed check leaves <see cref="Available" /> null and
///     the version note unchanged.
/// </summary>
public static class UpdateChecker
{
    private const string Repository = "t1stm/ThirtyDollarTools";
    private const string ReleasesUrl = $"https://api.github.com/repos/{Repository}/releases?per_page=30";

    // GitHub's fields are snake_case, so this can't be Sundex's camelCase SerializerOptions.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>The newer release the check found, or null while none is known.</summary>
    public static GitHubRelease? Available { get; private set; }

    /// <summary>
    ///     Set when the check couldn't be completed. The home screen says so, because with
    ///     checking turned on it drops the "check regularly" line - a failed check would
    ///     otherwise look exactly like an up-to-date one.
    /// </summary>
    public static bool Failed { get; private set; }

    /// <summary>
    ///     Starts the check in the background. Releases are compared by date rather than by
    ///     version: a nightly's tag never changes, so its tag says nothing about which build
    ///     is newer, while the build date written into VERSION does.
    /// </summary>
    public static void Start(VersionInfo? build, bool prereleases, bool nightlies, ILogger logger)
    {
        // No build date means a developer build (or a VERSION from before the date was
        // written) - there is nothing to call a release newer than.
        if (build?.Date is not { } buildDate) return;

        Task.Run(async () =>
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ThirtyDollarVisualizer");

                var releases = await client.GetFromJsonAsync<GitHubRelease[]>(ReleasesUrl, Options) ?? [];
                Available = PickNewer(releases, build, prereleases, nightlies);

                if (Available != null)
                    logger.Information("A newer release is available: {Tag}", Available.TagName);
            }
            catch (Exception e)
            {
                Failed = true;
                logger.Warning(e, "Failed to check {Repository} for updates.", Repository);
            }
        });
    }

    /// <summary>
    ///     The newest release that this build's channel opt-ins accept and that was created
    ///     after this build, or null when there's nothing newer.
    /// </summary>
    public static GitHubRelease? PickNewer(IEnumerable<GitHubRelease> releases, VersionInfo build,
        bool prereleases, bool nightlies)
    {
        if (build.Date is not { } buildDate) return null;

        return releases
            .Where(release => !release.Draft && release.CreatedAt > buildDate)
            .Where(release => Wanted(release, build, prereleases, nightlies))
            .MaxBy(release => release.CreatedAt);
    }

    private static bool Wanted(GitHubRelease release, VersionInfo build, bool prereleases, bool nightlies)
    {
        // Some other branch's nightly isn't an update to anything: only master's counts,
        // plus the branch this build itself came from.
        if (release.IsNightly)
            return nightlies && (release.TagName == build.Tag || release.TagName == "nightly-master");

        return !release.Prerelease || prereleases;
    }
}
