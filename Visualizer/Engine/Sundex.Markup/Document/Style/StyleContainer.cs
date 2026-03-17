using System.Xml;
using Sundex.Markup.Document.Root;

namespace Sundex.Markup.Document.Style;

public class StyleContainer(RootContainer root, XmlElement styleElement)
{
    public RootContainer Root { get; } = root;
    public string StyleString { get; } = styleElement.InnerText;
    public string Language { get; } = styleElement.GetAttribute("language");
}