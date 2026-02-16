using System.Xml;
using Sunder.Markup.Document.Root;

namespace Sunder.Markup.Document.Layout;

public class LayoutContainer(RootContainer root, XmlElement layoutElement)
{
    public RootContainer Root { get; } = root;
    public XmlElement LayoutElement { get; } = layoutElement;
}