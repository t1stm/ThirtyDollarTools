using System.Buffers;
using System.Reflection;
using ThirtyDollarVisualizer.Engine.Asset_Management.Abstract;
using ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Types.Asset;

public class AssetLoader : IAssetLoader<AssetStream, AssetInfo>
{
    private static readonly Lazy<HttpClient> HttpClient = new(() => new HttpClient());

    public bool Query(AssetInfo createInfo, AssetProvider assetProvider)
    {
        return createInfo.Storage switch
        {
            StorageLocation.Unknown => ExistsOnDisk(createInfo.Location) ||
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
            StorageLocation.Unknown => TryCreateFromDiskAndThenAssembly(createInfo, assetProvider.AssetAssemblies),
            StorageLocation.Disk => CreateFromDisk(createInfo),
            StorageLocation.Assembly => CreateFromAssemblies(createInfo, assetProvider.AssetAssemblies),
            StorageLocation.Network => CreateFromNetwork(createInfo),
            _ => throw new ArgumentOutOfRangeException(nameof(createInfo), createInfo,
                "Invalid AssetInfo.Storage value")
        };
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
        Assembly[] assetAssemblies)
    {
        return ExistsOnDisk(createInfo.Location)
            ? CreateFromDisk(createInfo)
            : CreateFromAssemblies(createInfo, assetAssemblies);
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
        var newLocation = ArrayPool<char>.Shared.Rent(createInfo.Location.Length);
        var newLocationSpan = newLocation.AsSpan();
        createInfo.Location.AsSpan().Replace(newLocationSpan, '/', '.');

        ReadOnlySpan<char> readonlySpan = newLocationSpan;
        var assetStream = assetAssemblies.GetManifestResourceStream(readonlySpan);

        if (assetStream is null)
            throw new FileNotFoundException($"Assembly file: \'{createInfo.Location}\' not found.");

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