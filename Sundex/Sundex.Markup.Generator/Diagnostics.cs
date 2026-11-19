using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Sundex.Markup.Generator;

internal static class Diagnostics
{
    private const string Category = "Sundex.Markup";

    public static readonly DiagnosticDescriptor Parse = Error("SDX0001", "The markup doesn't parse: {0}");
    public static readonly DiagnosticDescriptor UnknownTag = Error("SDX0002", "No component is tagged <{0}>: mark its class [MarkupTag(\"{0}\")] or write a {0}.snx.xml");
    public static readonly DiagnosticDescriptor UnknownAttribute = Error("SDX0003", "{1} has no settable property for the attribute '{0}'");
    public static readonly DiagnosticDescriptor BadLiteral = Error("SDX0004", "'{0}' isn't a valid {2} for '{1}'; write {{…}} for an expression");
    public static readonly DiagnosticDescriptor OrphanElse = Error("SDX0005", "<{0}> must directly follow an <if> or <else-if>");
    public static readonly DiagnosticDescriptor LayoutRoot = Error("SDX0006", "<layout> must hold exactly one element, not {0}");
    public static readonly DiagnosticDescriptor InlineStyle = Warning("SDX0007", "Inline styles are ignored: link the document's stylesheet with <style language=\"sundex\" src=\"…snx.ss\"/>");
    public static readonly DiagnosticDescriptor MissingAttribute = Error("SDX0008", "<{0}> needs {1}");
    public static readonly DiagnosticDescriptor DuplicateTag = Error("SDX0009", "Two documents are both <{0}>: {1}");
    public static readonly DiagnosticDescriptor NoConstructor = Error("SDX0010", "{0} can't be built from markup: it needs a public constructor taking just a UIContext");
    public static readonly DiagnosticDescriptor NotAContainer = Error("SDX0011", "<{0}> can't hold children: it isn't a Panel");
    public static readonly DiagnosticDescriptor FlowAsRoot = Error("SDX0012", "<{0}> can't be a layout's root: wrap it in an element");
    public static readonly DiagnosticDescriptor OrphanEmpty = Error("SDX0013", "<empty> must directly follow an <each>");
    public static readonly DiagnosticDescriptor UnknownProp = Error("SDX0014", "<{1}> declares no prop '{0}'");
    public static readonly DiagnosticDescriptor UnknownEvent = Error("SDX0015", "{1} has no event for '{0}'");
    public static readonly DiagnosticDescriptor ReadOnlyAttribute = Error("SDX0016", "'{0}' can't be set on {1} from markup: its setter isn't public");
    public static readonly DiagnosticDescriptor NoChangeEvent = Error("SDX0017", "{1} has no On{0}Changed or OnChange to bind '{0}' both ways");
    public static readonly DiagnosticDescriptor UnknownClass = Warning("SDX0018", "No rule in {1} or what it imports names the class '{0}'");
    public static readonly DiagnosticDescriptor ReservedProp = Error("SDX0019", "A prop can't be called '{0}': on a use, that attribute is the component's own");
    public static readonly DiagnosticDescriptor UnknownUseClass = Warning("SDX0022", "No rule in {1}, <{2}>'s own sheet, names the class '{0}': a class on a component tag is styled by the component's sheet, not this document's");
    public static readonly DiagnosticDescriptor KeywordName = Error("SDX0021", "A prop or event can't be called '{0}': it is a C# keyword, and the markup's expressions read it by name");
    public static readonly DiagnosticDescriptor MissingLogic = Error("SDX0023", "No logic file {0}: add the project's .snx.csx files to AdditionalFiles");
    public static readonly DiagnosticDescriptor StateInMedia = Error("SDX0020", "A media block can't hold a state block: put state[…] beside the media block, in the class");
    public static readonly DiagnosticDescriptor Crash = Error("SDX0099", "The markup generator failed on this document: {0}");

    public static Diagnostic At(DiagnosticDescriptor descriptor, string path, Spot spot, params object[] args)
    {
        var position = new LinePosition(System.Math.Max(0, spot.Line - 1), System.Math.Max(0, spot.Column - 1));
        var location = Location.Create(path, new TextSpan(0, 0), new LinePositionSpan(position, position));
        return Diagnostic.Create(descriptor, location, args);
    }

    private static DiagnosticDescriptor Error(string id, string message) =>
        new(id, message, message, Category, DiagnosticSeverity.Error, true);

    private static DiagnosticDescriptor Warning(string id, string message) =>
        new(id, message, message, Category, DiagnosticSeverity.Warning, true);
}
