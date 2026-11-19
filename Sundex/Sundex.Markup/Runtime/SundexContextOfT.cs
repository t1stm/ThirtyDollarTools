using Sundex.Components.Abstractions;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Style.DSL;

namespace Sundex.Markup;

/// <summary>
///     What builds version 2.0 documents, and what they share. <see cref="Context" /> is the
///     <c>Context.X</c> every document it builds reads: app-wide state, owned by the app.
/// </summary>
public class SundexContext<TContext>(UIContext ui, TContext context) : SundexContext(ui)
{
    private readonly Dictionary<string, StyleSheet> _sheets = new(StringComparer.Ordinal);

    /// <summary>The context variables: <c>Context.Player</c> in markup is <c>Context.Player</c> here.</summary>
    public TContext Context { get; } = context;

    /// <summary>The sheet of a document that links none: it keeps a parent's sheet from styling it.</summary>
    public StyleSheet EmptyStyleSheet { get; } = new(StyleParser.Parse(""));

    /// <summary>
    ///     A document's stylesheet, parsed once per source and shared by every instance of the
    ///     document; <c>@import</c>s resolve through the asset provider, as the runtime builder does.
    /// </summary>
    public StyleSheet LoadStyleSheet(string source)
    {
        if (_sheets.TryGetValue(source, out var sheet)) return sheet;
        var assets = UIContext.AssetProvider;
        sheet = new StyleSheet(StyleParser.Parse(assets.LoadString(source), path => assets.LoadString(path)));
        _sheets[source] = sheet;
        return sheet;
    }

    /// <summary>Drops the parsed sheets, so the next build reads them from the files again.</summary>
    public void ForgetStyleSheets() => _sheets.Clear();

    /// <summary>Builds <paramref name="component" /> and returns its root element.</summary>
    public UIElement Build(IMarkupComponent component) => component.Create();
}
