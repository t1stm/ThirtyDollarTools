using System.Buffers;
using System.Reflection;

namespace ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;

public static class AssemblyExtensions
{
    extension(Assembly[] assemblies)
    {
        public ManifestResourceInfo? GetManifestResourceInfo(ReadOnlySpan<char> name)
        {
            var newLocation = ArrayPool<char>.Shared.Rent(name.Length);
            var location = newLocation.AsSpan()[..name.Length];
            name.Replace(location, '/', '.');

            ReadOnlySpan<char> locationSpan = location;
            try
            {
                foreach (var assembly in assemblies)
                {
                    var assemblyName = assembly.GetName().Name;
                    var info = assembly.GetManifestResourceInfo($"{assemblyName}.{locationSpan}");
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
            name.Replace(location, '/', '.');

            ReadOnlySpan<char> locationSpan = location;
            try
            {
                foreach (var assembly in assemblies)
                {
                    var assemblyName = assembly.GetName().Name;
                    var stream = assembly.GetManifestResourceStream($"{assemblyName}.{locationSpan.ToString()}");
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