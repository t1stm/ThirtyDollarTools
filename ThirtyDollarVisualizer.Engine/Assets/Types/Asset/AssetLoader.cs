using System.Buffers;
using System.Reflection;
using ThirtyDollarVisualizer.Engine.Assets.Abstract;
using ThirtyDollarVisualizer.Engine.Assets.Extensions;

namespace ThirtyDollarVisualizer.Engine.Assets.Types.Asset;

public class AssetLoader : IAssetLoader<AssetStream, AssetInfo>
{
    private static readonly Lazy<HttpClient> HttpClient = new(() => new HttpClient());

    public bool Query(AssetInfo createInfo, AssetProvider assetProvider)
    {
        return createInfo.Storage switch
        {
            StorageLocation.Unknown => File.Exists(createInfo.Location) ||
                                       assetProvider.AssetAssemblies.GetManifestResourceInfo(createInfo.Location) != null,
            StorageLocation.Disk => File.Exists(createInfo.Location),
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

    private static AssetStream TryCreateFromDiskAndThenAssembly(AssetInfo createInfo,
        Assembly[] assetAssemblies)
    {
        return File.Exists(createInfo.Location)
            ? CreateFromDisk(createInfo)
            : CreateFromAssemblies(createInfo, assetAssemblies);
    }


    private static AssetStream CreateFromDisk(AssetInfo createInfo)
    {
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
        var assetStream = assetAssemblies.GetManifestResourceStream(readonlySpan.ToString());

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