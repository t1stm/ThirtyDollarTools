using System.Xml;
using Sundex.Markup.Document;
using Sundex.Markup.Document.Root;

namespace Sundex.Markup;

public class MarkupParser
{
    public static SundexDocument Parse(string markup)
    {
        var xmlDocument = new XmlDocument();
        xmlDocument.LoadXml(markup);

        var rootElement = xmlDocument.DocumentElement;
        if (rootElement == null) throw new XmlException("The root element is missing.");
        var root = new RootContainer(rootElement);

        return new SundexDocument
        {
            Root = root
        };
    }
}