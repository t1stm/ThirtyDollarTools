namespace BMS2TDW.Objects;

public struct BmsHeader()
{
    public int PlayerCount;
    public string Genre;
    public string Title;
    public string Artist;
    public int Bpm;
    public int PlayLevel;
    public int Rank;
    public int Total;
    public bool StageFile;

    public readonly Dictionary<string, string> ChannelMap = new()
    {
        { "00", "_pause" }
    };
}