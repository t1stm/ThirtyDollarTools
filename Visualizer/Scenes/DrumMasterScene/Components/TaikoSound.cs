using ThirtyDollarParser;

namespace DrumMasterScene.Components;

public class TaikoSound
{
    public required BaseEvent Event { get; set; }
    public string Sound { get; set; } = "";
    public string Volume { get; set; } = "";
    public string Pan { get; set; } = "";
    
    public bool IsHit { get; set; }
    public double? HitTime { get; set; }
    public double Timestamp { get; set; }
    public double ScrollSpeed { get; set; }
}