using System.Xml;
using Sunder.Markup.Document.Root;

namespace Sunder.Markup.Document.Style;

public class StyleContainer(RootContainer root, XmlElement styleElement)
{
    public RootContainer Root { get; } = root;
    public string StyleString { get; } = styleElement.InnerText;
    public string Language { get; } = styleElement.GetAttribute("language");
}