using System.Text.Json;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Types.Asset;
using Sundex.Engine.Common;

namespace Shared.Updates;

/// <summary>
///     The JSON contents of the embedded "VERSION" file, written by the release workflows.
///     Lives in Shared because every scene that shows or checks the version can reach it.
/// </summary>
public sealed record VersionInfo(
    string Tag,
    string? ReleaseTitle,
    bool Nightly,
    bool Prerelease,
    DateTimeOffset? Date)
{
    public const string DeveloperBuild = "Developer Build";

    /// <summary>What the home screen's version note shows.</summary>
    public string Display => string.IsNullOrWhiteSpace(ReleaseTitle) ? Tag : $"{Tag} - {ReleaseTitle}";

    /// <summary>
    ///     Reads the embedded "VERSION" asset, or null when it's absent or unreadable - that is,
    ///     when this build didn't come out of a workflow.
    /// </summary>
    public static VersionInfo? Read(IAssetProvider assetProvider)
    {
        var info = new AssetInfo { Location = "VERSION", Storage = StorageLocation.Assembly };
        if (!assetProvider.Query<AssetStream, AssetInfo>(info)) return null;

        try
        {
            using var stream = assetProvider.Load<AssetStream, AssetInfo>(info).Stream;
            return JsonSerializer.Deserialize<VersionInfo>(stream, SerializerOptions.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
