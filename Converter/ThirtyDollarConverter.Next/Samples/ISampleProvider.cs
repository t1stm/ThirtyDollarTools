using System.Diagnostics.CodeAnalysis;

namespace ThirtyDollarConverter.Next.Samples;

public interface ISampleProvider
{
    public bool Initialized { get; }
    public Task Initialize();
    public bool TryGetSample(ReadOnlySpan<char> name, [NotNullWhen(true)] out Sample? sample);
    
}