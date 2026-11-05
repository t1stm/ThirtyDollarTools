using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Serilog;

namespace Shared.Updates;

/// <summary>One GitHub release, cut down to the fields the update notice and its dialog need.</summary>
public sealed record GitHubRelease(
    string TagName,
    string HtmlUrl,
    bool Prerelease,
    bool Draft,
    DateTimeOffset CreatedAt,
    string? Name = null,
    string? Body = null)
{
    /// <summary>The nightly workflow tags every branch's build "nightly-{branch}".</summary>
    public bool IsNightly => TagName.StartsWith("nightly-", StringComparison.Ordinal);
}

/// <summary>
///     Opt-in check of the repository's releases, started once by the loader and read by the
///     home screen. Fire and forget: a failed check leaves <see cref="Available" /> null and
///     the version note unchanged.
/// </summary>
public static class UpdateChecker
{
    internal const string Repository = "t1stm/ThirtyDollarTools";
    private const string ReleasesUrl = $"https://api.github.com/repos/{Repository}/releases?per_page=30";

    // GitHub's fields are snake_case, so this can't be Sundex's camelCase SerializerOptions.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>The newer release the check found, or null while none is known.</summary>
    public static GitHubRelease? Available { get; private set; }

    /// <summary>
    ///     Every release newer than <see cref="Build" /> that the channel opt-ins accept, newest
    ///     first. Written before <see cref="Available" />, so whoever sees that one sees this too.
    /// </summary>
    public static IReadOnlyList<GitHubRelease> Releases { get; private set; } = [];

    /// <summary>The build the check compared against. Null until a check has started.</summary>
    public static VersionInfo? Build { get; private set; }

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
#if DEBUG
        build = PretendBuild() ?? build;
#endif
        if (build?.Date is null) return;
        Build = build;

        Task.Run(async () =>
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ThirtyDollarVisualizer");

                var releases = await client.GetFromJsonAsync<GitHubRelease[]>(ReleasesUrl, Options) ?? [];
                var newer = Newer(releases, build, prereleases, nightlies);
#if DEBUG
                newer = WithPretendBody(newer);
#endif
                Releases = newer;
                Available = newer.FirstOrDefault();

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
        return Newer(releases, build, prereleases, nightlies).FirstOrDefault();
    }

    /// <summary>
    ///     Every release that this build's channel opt-ins accept and that was created after
    ///     this build, newest first.
    /// </summary>
    public static IReadOnlyList<GitHubRelease> Newer(IEnumerable<GitHubRelease> releases, VersionInfo build,
        bool prereleases, bool nightlies)
    {
        if (build.Date is not { } buildDate) return [];

        return releases
            .Where(release => !release.Draft && release.CreatedAt > buildDate)
            .Where(release => Wanted(release, build, prereleases, nightlies))
            .OrderByDescending(release => release.CreatedAt)
            .ToList();
    }

    private static bool Wanted(GitHubRelease release, VersionInfo build, bool prereleases, bool nightlies)
    {
        // Some other branch's nightly isn't an update to anything: only master's counts,
        // plus the branch this build itself came from.
        if (release.IsNightly)
            return nightlies && (release.TagName == build.Tag || release.TagName == "nightly-master");

        return !release.Prerelease || prereleases;
    }

#if DEBUG
    /// <summary>
    ///     A developer build has no date, so it never checks. TDT_PRETEND_BUILD_DATE (an ISO date,
    ///     with TDT_PRETEND_BUILD_TAG naming it) makes it check as a build from then, so the update
    ///     note and its dialog can be driven headless.
    /// </summary>
    private static VersionInfo? PretendBuild()
    {
        return DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("TDT_PRETEND_BUILD_DATE"),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? new VersionInfo(Environment.GetEnvironmentVariable("TDT_PRETEND_BUILD_TAG") ?? "v0.0.0-pretend",
                null, false, false, date)
            : null;
    }

    /// <summary>TDT_RELEASE_BODY names a file that replaces the newest offered release's body: a dead image, say.</summary>
    private static IReadOnlyList<GitHubRelease> WithPretendBody(IReadOnlyList<GitHubRelease> releases)
    {
        if (Environment.GetEnvironmentVariable("TDT_RELEASE_BODY") is not { } path || releases.Count == 0)
            return releases;
        return [releases[0] with { Body = File.ReadAllText(path) }, .. releases.Skip(1)];
    }
#endif
}
