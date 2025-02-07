using System.Reflection;

namespace ThirtyDollarVisualizer.Assets;

public static class AssetManager
{
    public static Stream GetAsset(string path)
    {
        Stream source;
        if (path.Contains('*'))
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }
            var search_pattern = Path.GetFileName(path);
            if (string.IsNullOrEmpty(search_pattern))
            {
                throw new ArgumentException("Invalid pattern; no file name specified.", nameof(path));
            }
            
            var files = Directory.GetFiles(directory, search_pattern);
            if (files.Length == 0)
            {
                throw new FileNotFoundException($"Unable to find any files matching '{path}'.");
            }
            source = File.OpenRead(files[0]);
        }
        else if (File.Exists(path))
        {
            source = File.OpenRead(path);
        }
        else
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
            source = stream ?? throw new FileNotFoundException($"Unable to find asset '{path}' in assembly or real path.");
        }

        return source;
    }
}