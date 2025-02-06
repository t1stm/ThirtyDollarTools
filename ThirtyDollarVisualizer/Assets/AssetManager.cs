using System.Reflection;

namespace ThirtyDollarVisualizer.Assets;

public class AssetManager
{
    public static Stream GetAsset(string path)
    {
        Stream source;

        if (File.Exists(path))
        {
            source = File.OpenRead(path);
        }
        else
        {
            var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(path);

            source = stream ??
                     throw new FileNotFoundException($"Unable to find texture \'{path}\' in assembly or real path.");
        }

        return source;
    }
}