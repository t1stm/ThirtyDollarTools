using System.Xml;
using Sundex.Markup.Document.Root;

namespace Sundex.Markup.Document.Style;

public class StyleContainer(RootContainer root, XmlElement styleElement)
{
    public RootContainer Root { get; } = root;
    public string SourceCode { get; private set; } = styleElement.InnerText;
    public string SrcLocation { get; private set; } = styleElement.GetAttribute("src");
    public string Language { get; } = styleElement.GetAttribute("language");

    public void UpdateSourceCode(string sourceCode)
    {
        SourceCode = sourceCode;
    }
}