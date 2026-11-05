using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Updates;

/// <summary>
///     Turns GitHub releases into the Markdown and the lines the update dialog shows. Works on
///     the release's <c>body</c>, never <c>body_html</c>: the attachment URLs in that one carry a
///     token that dies 300 s after the response, and the dialog can open hours after the check.
/// </summary>
public static partial class ReleaseNotes
{
    /// <summary>What a release with no body shows, the same words as the site's What's new.</summary>
    public const string NoNotes = "No notes for this release.";

    /// <summary>A nightly's body lists every commit since the last release; the site shows this many too.</summary>
    private const int NightlyItems = 8;

    /// <summary>The notes of every release in <paramref name="releases" />, in order, as one document.</summary>
    /// <remarks>The dialog's title names the first release, so only the older ones get a head.</remarks>
    public static string Combine(IReadOnlyList<GitHubRelease> releases)
    {
        var notes = new StringBuilder();
        for (var i = 0; i < releases.Count; i++)
        {
            if (i > 0) notes.Append("\n\n# ").Append(Title(releases[i])).Append("\n\n").Append(Subtitle(releases[i])).Append("\n\n");
            notes.Append(Prepare(releases[i]));
        }

        return notes.ToString();
    }

    /// <summary>One release's body, ready for a Markdown view: GitHub's auto-links written out, a nightly cut short.</summary>
    public static string Prepare(GitHubRelease release)
    {
        if (string.IsNullOrWhiteSpace(release.Body)) return NoNotes;

        var body = release.Body.ReplaceLineEndings("\n");
        if (release.IsNightly) body = KeepFirstItems(body, NightlyItems);

        // Bare addresses GitHub would have shortened when it rendered the page.
        body = CommitUrl().Replace(body, match => $"[{match.Groups[1].Value[..7]}]({match.Value})");
        body = PullUrl().Replace(body, match => $"[#{match.Groups[1].Value}]({match.Value})");
        return PullReference().Replace(body,
            match => $"[#{match.Groups[1].Value}](https://github.com/{UpdateChecker.Repository}/pull/{match.Groups[1].Value})");
    }

    public static string Title(GitHubRelease release)
    {
        return string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name;
    }

    /// <summary>"v2.0.0-rc7 · 21 Oct 2026".</summary>
    public static string Subtitle(GitHubRelease release)
    {
        return $"{release.TagName} · {release.CreatedAt.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
    }

    /// <summary>"2 releases, 72 days newer": what the build card says between the two tags.</summary>
    public static string Gain(IReadOnlyList<GitHubRelease> releases, VersionInfo build)
    {
        var count = releases.Count == 1 ? "1 release" : $"{releases.Count} releases";
        if (releases.Count == 0 || build.Date is not { } buildDate) return count;

        var days = (releases[0].CreatedAt - buildDate).Days;
        return days switch
        {
            < 1 => $"{count}, less than a day newer",
            1 => $"{count}, 1 day newer",
            _ => $"{count}, {days} days newer"
        };
    }

    /// <summary>Cuts the document after its <paramref name="keep" />th list item and says how many went.</summary>
    private static string KeepFirstItems(string body, int keep)
    {
        var items = ListItem().Matches(body);
        if (items.Count <= keep) return body;

        var last = items[keep - 1];
        var end = body.IndexOf('\n', last.Index);
        return $"{body[..end]}\n\nand {items.Count - keep} more on the release page";
    }

    [GeneratedRegex(@"^\s*[-*+] ", RegexOptions.Multiline)]
    private static partial Regex ListItem();

    // Never one that is already a Markdown link's target, or an attribute's value.
    [GeneratedRegex("""(?<!\]\(|["'])https://github\.com/[\w.-]+/[\w.-]+/commit/([0-9a-f]{7,40})(?![\w/])""")]
    private static partial Regex CommitUrl();

    [GeneratedRegex("""(?<!\]\(|["'])https://github\.com/[\w.-]+/[\w.-]+/pull/(\d+)(?![\w/])""")]
    private static partial Regex PullUrl();

    // Not a colour (`!bg@#2087ee`), not the #define in prose, not the text of a link written just above.
    [GeneratedRegex(@"(?<![\w@&#/\[])#(\d+)(?!\w)")]
    private static partial Regex PullReference();
}
