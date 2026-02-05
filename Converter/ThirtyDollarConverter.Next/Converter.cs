using ThirtyDollarConverter.Next.Audio_Building;
using ThirtyDollarConverter.Next.Samples;
using ThirtyDollarConverter.Next.Sound_Placement;
using ThirtyDollarConverter.Objects;

namespace ThirtyDollarConverter.Next;

public class Converter(EncoderSettings settings)
{
    private readonly SampleProviderCollection _collection = new(settings.DownloadLocation);
    private PlacementCalculator _placementCalculator = new(settings);
    private DifferenceChecker _checker = new();
    private Exporter _exporter = new();

    
    public async Task Initialize()
    {
        await _collection.Initialize();
    }
    
    
    public void ComputeDifference()
    {
        
    }
    
    public void RenderDifference()
    {
        
    }
}