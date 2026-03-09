using System.Xml;
using Sunder.Markup.Document.Root;
using Sunder.Markup.Layout;

namespace Sunder.Markup.Document.Layout;

public class LayoutContainer(RootContainer root, XmlElement layoutElement)
{
    public RootContainer Root { get; } = root;
    public XmlElement LayoutElement { get; } = layoutElement;

    public List<SundexNode> BuildTree()
    {
        var nodes = new List<SundexNode>();
        foreach (XmlNode child in LayoutElement.ChildNodes)
            if (child is XmlElement el)
                nodes.Add(ParseNode(el));
        return nodes;
    }

    private static SundexNode ParseNode(XmlElement element)
    {
        var attributes = new Dictionary<string, string>();
        foreach (XmlAttribute attr in element.Attributes)
            attributes[attr.Name] = attr.Value;

        attributes.Remove("id", out var idString);
        attributes.Remove("class", out var classString);

        HashSet<string>? classes = null;
        if (classString is not null)
        {
            classes = [];
            if (classString.StartsWith('[') && classString.EndsWith(']'))
                classes = classString[1..^1].Split(',').ToHashSet();
            else classes.Add(classString);
        }

        var children = new List<SundexNode>();
        foreach (XmlNode child in element.ChildNodes)
            if (child is XmlElement el)
                children.Add(ParseNode(el));

        return new SundexNode
        {
            TagName = element.Name,
            Id = idString,
            Classes = classes,
            Attributes = attributes,
            Children = children
        };
    }
}