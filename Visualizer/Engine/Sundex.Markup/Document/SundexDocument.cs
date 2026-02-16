using Sunder.Markup.Document.Layout;
using Sunder.Markup.Document.Logic;
using Sunder.Markup.Document.Root;
using Sunder.Markup.Document.Style;

namespace Sunder.Markup.Document;

public class SundexDocument
{
    public required RootContainer Root { get; init; }

    public LayoutContainer Layout => Root.Layout;
    public LogicContainer? Logic => Root.Logic;
    public StyleContainer? Style => Root.Style;
}