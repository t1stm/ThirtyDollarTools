namespace Sundex.Markup.Layout;

public class SundexNode
{
    public required string TagName { get; init; }
    public string? Id { get; init; }
    /// <summary>Declaration order is kept: see <c>UIElement.Classes</c>, a later class wins.</summary>
    public List<string>? Classes { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = [];
    public List<SundexNode> Children { get; init; } = [];
}