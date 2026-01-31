using System.Diagnostics.CodeAnalysis;
using ThirtyDollarConverter.Next.Samples.Custom;
using ThirtyDollarConverter.Next.Samples.Thirty_Dollar_Website;

namespace ThirtyDollarConverter.Next.Samples;

public class SampleProviderCollection(string downloadLocation) : ISampleProvider
{
    private readonly string _defaultCustomSampleLocation = $"{downloadLocation}/Custom/";
    private readonly string _defaultTDWSampleLocation = $"{downloadLocation}/";
    private readonly SemaphoreSlim _semaphore = new(1);
    
    public bool Initialized { get; private set; }
    private ISampleProvider[] _providers = [];

    public async Task Initialize()
    {
        await _semaphore.WaitAsync();
        if (Initialized)
        {
            _semaphore.Release();
            return;
        }
        
        _providers =
        [
            new TDWSampleProvider(_defaultTDWSampleLocation),
            new CustomSampleProvider(_defaultCustomSampleLocation),
        ];

        try
        {
            foreach (var provider in _providers)
            {
                await provider.Initialize();
            }
        
            Initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool TryGetSample(ReadOnlySpan<char> name, [NotNullWhen(true)] out Sample? sample)
    {
        sample = null;
        foreach (var provider in _providers)
        {
            if (provider.TryGetSample(name, out sample))
                return true;
        }
        
        return false;
    }
}