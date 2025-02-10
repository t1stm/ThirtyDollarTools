namespace BMS2TDW.Objects;

public struct BMSHeader()
{
    public int PlayerCount;
    public string Genre;
    public string Title;
    public string Artist;
    public int BPM;
    public int PlayLevel;
    public int Rank;
    public int Total;
    public bool StageFile;

    public readonly Dictionary<string, string> ChannelMap = new()
    {
        { "00", "_pause" }
    };
    
    public readonly Dictionary<string, string> BPMMap = new()
    {
        { "00", "" }
    };
    public readonly Dictionary<string, string> StopMap = new()
    {
        {"00", ""}
    };
}