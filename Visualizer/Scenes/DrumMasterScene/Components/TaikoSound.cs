using ThirtyDollarConverter.Parser;

namespace DrumMasterScene.Components;

public class TaikoSound
{
    public required BaseEvent Event { get; set; }
    public bool IsHit { get; set; }
    public double? HitTime { get; set; }
    public double Timestamp { get; set; }
    public double ScrollSpeed { get; set; }
}