using Sundex.Markup.Document.Layout;
using Sundex.Markup.Document.Logic;
using Sundex.Markup.Document.Root;
using Sundex.Markup.Document.Style;

namespace Sundex.Markup.Document;

public class SundexDocument
{
    public required RootContainer Root { get; init; }

    public LayoutContainer Layout => Root.Layout;
    public LogicContainer? Logic => Root.Logic;
    public StyleContainer? Style => Root.Style;
}