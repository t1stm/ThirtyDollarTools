namespace Sunder.Markup.Layout;

public class SundexNode
{
    public required string TagName { get; init; }
    public string? Id { get; init; }
    public HashSet<string>? Classes { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = [];
    public List<SundexNode> Children { get; init; } = [];
}