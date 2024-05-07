namespace ThirtyDollarVisualizer.Objects.Settings;

[Flags]
public enum CameraFollowMode
{
    None = 0,
    No_Animation = 1,
    TDW_Like = 1 << 1,
    Current_Line = 1 << 2,
    No_Animation_TDW = No_Animation | TDW_Like,
    No_Animation_Current_Line = No_Animation | Current_Line
}