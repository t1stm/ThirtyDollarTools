using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Sundex.Engine.Asset_Management.Extensions;

public static class AssemblyExtensions
{
    private static readonly ConditionalWeakTable<Assembly, string> AssetRoots = new();

    private static string AssetRoot(Assembly assembly) => AssetRoots.GetValue(assembly, static source =>
        source.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "SundexAssetRoot")?.Value ?? source.GetName().Name!);

    extension(Assembly[] assemblies)
    {
        private static void Sanitize(ReadOnlySpan<char> src, Span<char> dest, char searchChar, char replaceChar)
        {
            src.Replace(dest, searchChar, replaceChar);
        }

        private static void Sanitize(ReadOnlySpan<char> src, Span<char> dest)
        {
            Sanitize(src, dest, '/', '.');
            Sanitize(dest, dest, ' ', '_');
        }

        public ManifestResourceInfo? GetManifestResourceInfo(ReadOnlySpan<char> name)
        {
            var newLocation = ArrayPool<char>.Shared.Rent(name.Length);
            var location = newLocation.AsSpan()[..name.Length];
            Sanitize(name, location);

            ReadOnlySpan<char> locationSpan = location;
            try
            {
                foreach (var assembly in assemblies)
                {
                    var info = assembly.GetManifestResourceInfo($"{AssetRoot(assembly)}.{locationSpan}");
                    if (info != null) return info;
                }

                return null;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(newLocation);
            }
        }

        public Stream? GetManifestResourceStream(ReadOnlySpan<char> name)
        {
            var newLocation = ArrayPool<char>.Shared.Rent(name.Length);
            var location = newLocation.AsSpan()[..name.Length];
            Sanitize(name, location);

            ReadOnlySpan<char> locationSpan = location;
            try
            {
                foreach (var assembly in assemblies)
                {
                    var stream = assembly.GetManifestResourceStream($"{AssetRoot(assembly)}.{locationSpan}");
                    if (stream != null) return stream;
                }

                return null;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(newLocation);
            }
        }
    }
}
