namespace ThirtyDollarVisualizer.Base_Objects.Settings;

[Flags]
public enum CameraFollowMode
{
    None = 0,
    NoAnimation = 1,
    TDWLike = 1 << 1,
    CurrentLine = 1 << 2,
    NoAnimationTDW = NoAnimation | TDWLike,
    NoAnimationCurrentLine = NoAnimation | CurrentLine
}