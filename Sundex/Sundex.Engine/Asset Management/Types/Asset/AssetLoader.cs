using System.Reflection;
using Sundex.Engine.Asset_Management.Abstract.Loading;
using Sundex.Engine.Asset_Management.Abstract.Metadata;
using Sundex.Engine.Asset_Management.Extensions;

namespace Sundex.Engine.Asset_Management.Types.Asset;

public class AssetLoader : IAssetLoader<AssetStream, AssetInfo>, IMetadataLoader<AssetMetadata, AssetInfo>
{
    private static readonly Lazy<HttpClient> HttpClient = new(() => new HttpClient());

    public bool Query(AssetInfo createInfo, AssetProvider assetProvider)
    {
        return createInfo.Storage switch
        {
            StorageLocation.Unknown => IsWebLocation(createInfo.Location) ||
                                       FindInSourceRoots(createInfo.Location, assetProvider.SourceRoots) != null ||
                                       ExistsOnDisk(createInfo.Location) ||
                                       assetProvider.AssetAssemblies.GetManifestResourceInfo(createInfo.Location) !=
                                       null,
            StorageLocation.Disk => ExistsOnDisk(createInfo.Location),
            StorageLocation.Assembly => assetProvider.AssetAssemblies.GetManifestResourceInfo(createInfo.Location) !=
                                        null,
            StorageLocation.Network => true,
            _ => false
        };
    }

    public AssetStream Load(AssetInfo createInfo, AssetProvider assetProvider,
        Func<AssetInfo, AssetProvider, AssetStream> create)
    {
        return create(createInfo, assetProvider);
    }

    public AssetStream Load(AssetInfo createInfo, AssetProvider assetProvider)
    {
        return Load(createInfo, assetProvider, Create);
    }

    public static AssetStream Create(AssetInfo createInfo, AssetProvider assetProvider)
    {
        return createInfo.Storage switch
        {
            StorageLocation.Unknown => IsWebLocation(createInfo.Location)
                ? CreateFromNetwork(createInfo)
                : TryCreateFromDiskAndThenAssembly(createInfo, assetProvider),
            StorageLocation.Disk => CreateFromDisk(createInfo),
            StorageLocation.Assembly => CreateFromAssemblies(createInfo, assetProvider.AssetAssemblies),
            StorageLocation.Network => CreateFromNetwork(createInfo),
            _ => throw new ArgumentOutOfRangeException(nameof(createInfo), createInfo,
                "Invalid AssetInfo.Storage value")
        };
    }

    public AssetMetadata Metadata(AssetInfo createInfo)
    {
        return createInfo.Storage switch
        {
            StorageLocation.Disk => new AssetMetadata
            {
                Found = File.Exists(createInfo.Location),
                ModifiedDate = File.GetLastWriteTime(createInfo.Location)
            },
            StorageLocation.Unknown or StorageLocation.Network or StorageLocation.Assembly => new AssetMetadata
            {
                Found = true,
                ModifiedDate = DateTime.UnixEpoch // it's best to not overcomplicate things sometimes
            },
            _ => throw new ArgumentOutOfRangeException(nameof(createInfo), createInfo,
                "Invalid AssetInfo.Storage value")
        };
    }

    /// <summary>
    ///     Whether an <see cref="StorageLocation.Unknown" /> location is really a URL. Checked
    ///     before disk and assembly, since a "https://..." string can never name either of
    ///     those and would otherwise be probed as a file path.
    /// </summary>
    public static bool IsWebLocation(string location)
    {
        return location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               location.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ExistsOnDisk(string path)
    {
        if (!path.Contains('*')) return File.Exists(path);

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
            directory = Directory.GetCurrentDirectory();

        var fileName = Path.GetFileName(path);
        if (!Directory.Exists(directory)) return false;

        var lookup = Directory.EnumerateFiles(directory, fileName);
        return lookup.Any();
    }

    private static AssetStream TryCreateFromDiskAndThenAssembly(AssetInfo createInfo,
        AssetProvider assetProvider)
    {
        // Source tree first: in Debug this is the file being edited, and resolving to it
        // rather than to the build-time copy in the assembly is what makes reloading the
        // UI without a rebuild possible. SourceRoots is empty in Release.
        if (FindInSourceRoots(createInfo.Location, assetProvider.SourceRoots) is { } sourcePath)
        {
            createInfo.Location = sourcePath;
            return CreateFromDisk(createInfo);
        }

        return ExistsOnDisk(createInfo.Location)
            ? CreateFromDisk(createInfo)
            : CreateFromAssemblies(createInfo, assetProvider.AssetAssemblies);
    }

    /// <summary>
    ///     Resolves a project-relative asset location against the source directories of the
    ///     asset assemblies, returning the first that exists. Asset locations are already
    ///     written relative to their project ("Scenes/Layout/Home.snx.xml"), which is
    ///     exactly what these roots are the base for.
    /// </summary>
    public static string? FindInSourceRoots(string location, string[] sourceRoots)
    {
        if (sourceRoots.Length == 0 || Path.IsPathRooted(location)) return null;

        foreach (var root in sourceRoots)
        {
            var candidate = Path.Combine(root, location);
            if (ExistsOnDisk(candidate)) return candidate;
        }

        return null;
    }


    private static AssetStream CreateFromDisk(AssetInfo createInfo)
    {
        if (createInfo.Location.Contains('*'))
        {
            var directory = Path.GetDirectoryName(createInfo.Location);
            if (string.IsNullOrEmpty(directory)) directory = Directory.GetCurrentDirectory();
            var fileName = Path.GetFileName(createInfo.Location);

            var firstMatch = Directory.EnumerateFiles(directory, fileName).FirstOrDefault();
            if (firstMatch is null)
                throw new FileNotFoundException($"File matching pattern: \'{createInfo.Location}\' not found on disk.");

            createInfo.Location = firstMatch;
            createInfo.Storage = StorageLocation.Disk;
            return new AssetStream
            {
                Stream = File.OpenRead(createInfo.Location),
                Info = createInfo
            };
        }

        if (!File.Exists(createInfo.Location))
            throw new FileNotFoundException($"File at location: \'{createInfo.Location}\' not found on disk.");

        createInfo.Storage = StorageLocation.Disk;
        return new AssetStream
        {
            Stream = File.OpenRead(createInfo.Location),
            Info = createInfo
        };
    }

    private static AssetStream CreateFromAssemblies(AssetInfo createInfo, Assembly[] assetAssemblies)
    {
        var assetStream = assetAssemblies.GetManifestResourceStream(createInfo.Location);

        if (assetStream is null)
        {
            var available = assetAssemblies.SelectMany(a => a.GetManifestResourceNames()).ToList();
            throw new FileNotFoundException(
                $"Assembly file: \'{createInfo.Location}\' not found. \nAvailable assembly files: {string.Join(", ", available)}");
        }

        createInfo.Storage = StorageLocation.Assembly;
        return new AssetStream { Stream = assetStream, Info = createInfo };
    }

    private static AssetStream CreateFromNetwork(AssetInfo createInfo)
    {
        var httpClient = HttpClient.Value;
        var connection = httpClient.GetAsync(createInfo.Location).GetAwaiter().GetResult();
        createInfo.Storage = StorageLocation.Network;

        return new AssetStream
            { Stream = connection.Content.ReadAsStreamAsync().GetAwaiter().GetResult(), Info = createInfo };
    }
}