using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Reactive;
using Sundex.Style.DSL;

namespace Sundex.Markup;

/// <summary>A built (or buildable) markup document, whatever its context type.</summary>
public interface IMarkupComponent : IDisposable
{
    UIElement? Root { get; }
    Scope? Scope { get; }
    UIElement Create();
}

/// <summary>
///     The base of every class the markup generator writes. One instance is one use of the
///     document: its <c>&lt;logic&gt;</c> runs once, in <see cref="Build" />, so its variables are
///     this instance's own, and everything it starts ends with <see cref="Scope" />.
/// </summary>
public abstract class MarkupComponent<TContext>(SundexContext<TContext> sundex) : IMarkupComponent
{
    /// <summary>The context that built this document.</summary>
    public SundexContext<TContext> Sundex { get; } = sundex;

    /// <summary>The context variables, shared by every document <see cref="Sundex" /> builds.</summary>
    public TContext Context => Sundex.Context;

    protected UIContext Ui => Sundex.UIContext;

    /// <summary>What a use of this component put inside its tag, built where the document has <c>&lt;slot/&gt;</c>.</summary>
    public Action<Panel>? Slot { get; set; }

    /// <summary>What a use put in its children marked <c>slot="name"</c>, built where the document has <c>&lt;slot name="name"/&gt;</c>.</summary>
    public Dictionary<string, Action<Panel>> Slots { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether the use filled the slot: <c>&lt;if condition="{HasSlot("actions")}"&gt;</c>.</summary>
    protected bool HasSlot(string? name = null) => name == null ? Slot != null : Slots.ContainsKey(name);

    protected void FillSlot(string name, Panel parent)
    {
        if (Slots.TryGetValue(name, out var fill)) fill(parent);
    }

    public UIElement? Root { get; private set; }
    public Scope? Scope { get; private set; }

    /// <summary>Builds the document once, in a scope owned by the current one.</summary>
    public UIElement Create()
    {
        if (Root != null) throw new InvalidOperationException($"{GetType().Name} is already built: make another instance.");
        Scope = new Scope();
        Root = Scope.Run(Build);
        if (_mounted != null) Scope.Run(() => _mounted());
        _mounted = null;
        return Root;
    }

    private Action? _mounted;

    /// <summary>
    ///     Runs <paramref name="action" /> once the layout is built, in the document's scope: where
    ///     logic reaches what a <c>ref="…"</c> holds, which is still null while the logic itself runs.
    /// </summary>
    protected void OnMount(Action action) => _mounted += action;

    protected abstract UIElement Build();

    protected StyleSheet LoadStyleSheet(string? source) =>
        source == null ? Sundex.EmptyStyleSheet : Sundex.LoadStyleSheet(source);

    /// <summary>Ends everything the document started and disposes its elements.</summary>
    public void Dispose()
    {
        Scope?.Dispose();
        Root?.Dispose();
    }
}
