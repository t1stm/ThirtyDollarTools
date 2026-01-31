using System.Diagnostics.CodeAnalysis;

namespace ThirtyDollarConverter.Next.Samples.Custom;

public class CustomSampleProvider(string path) : ISampleProvider
{
    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly Dictionary<string, Sample> _samples = [];
    public bool Initialized { get; private set; }
    
    public async Task Initialize()
    {
        await _semaphore.WaitAsync();
        if (Initialized) return;
        Initialized = true;

        foreach (var file in Directory.EnumerateFiles(path, "*.wav"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            _samples.Add(name, new Sample
            {
                Name = name,
                FileLocation = file
            });
        }
        
        _semaphore.Release();
    }

    public bool TryGetSample(ReadOnlySpan<char> name, [NotNullWhen(true)] out Sample? sample)
    {
        var lookup = _samples.GetAlternateLookup<ReadOnlySpan<char>>();
        sample = null;
        return lookup.TryGetValue(name, out sample);
    }
}