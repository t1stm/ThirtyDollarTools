namespace BMS2TDW.Converter;

public readonly struct CombineStack()
{
    public readonly List<string> Events = new();
    public string Export() => string.Join("!combine|", Events.Where(r => !string.IsNullOrWhiteSpace(r) && r is not "_pause")) + '\n';
}