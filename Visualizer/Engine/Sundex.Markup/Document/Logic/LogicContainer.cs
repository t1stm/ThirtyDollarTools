using System.Xml;
using Sunder.Markup.Document.Root;

namespace Sunder.Markup.Document.Logic;

public class LogicContainer(RootContainer root, XmlElement logicElement)
{
    public RootContainer Root { get; } = root;
    public string SourceCode { get; } = logicElement.InnerText;
    public string Language { get; } = logicElement.GetAttribute("language");
}