using System.Globalization;
using System.Reflection;
using OpenTK.Mathematics;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using Sundex.Reactive;
using Sundex.Style.DSL;

namespace Sundex.Markup;

/// <summary>What generated markup calls. Not meant to be written by hand.</summary>
public static class MarkupRuntime
{
    /// <summary>An element's id and classes, then its document's sheet, cascading into what its constructor built.</summary>
    public static void Init(UIElement element, StyleSheet? sheet, string? id, string? classes)
    {
        if (id != null) element.ID = id;
        foreach (var name in ClassList(classes)) element.Classes.Add(name);
        if (sheet != null) element.ApplyStyleSheet(sheet);
    }

    /// <summary>Classes and an id on a component's root, written where the component is used.</summary>
    public static void Usage(UIElement root, string? id, string? classes)
    {
        if (id != null) root.ID = id;
        foreach (var name in ClassList(classes)) root.SetClass(name, true);
    }

    public static void Add(Panel parent, UIElement child) => parent.AddChild(child);

    /// <summary>An attribute bound to an expression: set now, and again when what it read changes.</summary>
    public static void Bind(Action set) => Rx.Effect(set);

    /// <summary><c>class.name="{on}"</c>.</summary>
    public static void Class(UIElement element, string name, Func<bool> on) => Rx.Effect(() =>
    {
        var value = on();
        Rx.Untrack(() => element.SetClass(name, value));
    });

    public static Action<T> Handler<T>(Action handler) => _ => handler();
    public static Action<T> Handler<T>(Action<T> handler) => handler;

    public static IEnumerable<T> Items<T>(IEnumerable<T> items) => items;
    public static IEnumerable<T> Items<T>(IReadOnlyState<IEnumerable<T>> items) => items.Value;

    /// <summary>
    ///     <c>&lt;if&gt;</c>, <c>&lt;else-if&gt;</c>, <c>&lt;else&gt;</c>. <paramref name="which" /> picks a branch
    ///     (-1 for none). A branch is built when picked and ended when another is: rebuilt only when
    ///     the pick changes, not whenever something the conditions read does. With
    ///     <paramref name="keep" />, every branch picked once is kept and only hidden.
    /// </summary>
    public static Region If(Panel parent, Func<int> which, bool keep, params Action<Panel>[] branches)
    {
        var region = new Region(parent.Context);
        parent.AddChild(region);
        var picked = Rx.Computed(which);

        if (!keep)
        {
            // The effect reruns only when the computed's answer changes, and each run's own scope
            // is what the branch is built in, so the old branch ends before the new one starts.
            Rx.Effect(() =>
            {
                var index = picked.Value;
                Rx.OnCleanup(region.Clear);
                if (index < 0 || index >= branches.Length) return;
                Rx.Untrack(() => branches[index](region));
            });
            return region;
        }

        var built = new Region?[branches.Length];
        var owner = Scope.Current;
        Rx.Effect(() =>
        {
            var index = picked.Value;
            Rx.Untrack(() =>
            {
                if (index >= 0 && index < branches.Length && built[index] == null)
                {
                    var holder = new Region(parent.Context);
                    region.AddChild(holder);
                    built[index] = holder;
                    Scope.ChildOf(owner).Run(() => branches[index](holder));
                }

                for (var position = 0; position < built.Length; position++)
                    if (built[position] is { } holder)
                        holder.Visible = position == index;
            });
        });
        return region;
    }

    /// <summary>
    ///     <c>&lt;each&gt;</c> and <c>&lt;empty&gt;</c>. Rows are keyed: a row whose key survives a change
    ///     keeps its elements and state, and only its index moves. A row whose item was replaced by
    ///     an unequal one is rebuilt.
    /// </summary>
    public static Region Each<T>(Panel parent, Func<IEnumerable<T>> items, Func<T, object?> key,
        Action<Panel, T, State<int>> build, Action<Panel>? empty)
    {
        var region = new Region(parent.Context);
        parent.AddChild(region);
        var owner = Scope.Current;
        var rows = new Dictionary<object, Row<T>>();
        var count = new State<int>(-1);
        // The empty branch lives in a holder of its own, kept after the rows.
        Region? emptyHolder = null;
        if (empty != null)
        {
            emptyHolder = new Region(parent.Context);
            region.AddChild(emptyHolder);
        }

        Rx.Effect(() =>
        {
            var list = items().ToList();
            Rx.Untrack(() =>
            {
                var next = new Dictionary<object, Row<T>>(list.Count);
                var order = new List<UIElement>(list.Count);
                for (var index = 0; index < list.Count; index++)
                {
                    var item = list[index];
                    var id = key(item) ?? NullKey.Instance;
                    // A repeated key gets a row of its own rather than stealing the first one's.
                    if (next.ContainsKey(id)) id = new DuplicateKey(id, index);

                    if (rows.Remove(id, out var row) && EqualityComparer<T>.Default.Equals(row.Item, item))
                    {
                        row.Index.Value = index;
                    }
                    else
                    {
                        row?.End(region);
                        row = new Row<T>(item, new State<int>(index), new Region(parent.Context), Scope.ChildOf(owner));
                        var current = row;
                        region.AddChild(current.Holder);
                        current.Scope.Run(() => build(current.Holder, current.Item, current.Index));
                    }

                    next[id] = row;
                    order.Add(row.Holder);
                }

                foreach (var row in rows.Values) row.End(region);
                rows = next;
                if (emptyHolder != null) order.Add(emptyHolder);
                region.Order(order);
                count.Value = list.Count;
            });
        });

        if (emptyHolder != null) If(emptyHolder, () => count.Value == 0 ? 0 : -1, false, empty!);
        return region;
    }

    /// <summary>A literal attribute the generator couldn't turn into C# itself.</summary>
    public static T Parse<T>(string text) => (T)Parse(typeof(T), text);

    private static object Parse(Type type, string text)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(Vector4)) return Color(text);
        if (type.IsEnum) return Enum.Parse(type, text.Replace("-", ""), true);

        var withCulture = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static,
            [typeof(string), typeof(IFormatProvider)]);
        if (withCulture != null) return withCulture.Invoke(null, [text, CultureInfo.InvariantCulture])!;
        var plain = type.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (plain != null) return plain.Invoke(null, [text])!;
        return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
    }

    /// <summary><c>#rgb</c>, <c>#rrggbb</c> or <c>#rrggbbaa</c> as a 0–1 colour.</summary>
    public static Vector4 Color(string text)
    {
        var hex = text.TrimStart('#');
        if (hex.Length is 3 or 4) hex = string.Concat(hex.Select(character => $"{character}{character}"));
        if (hex.Length == 6) hex += "ff";
        if (hex.Length != 8) throw new FormatException($"'{text}' isn't a colour: write #rgb, #rrggbb or #rrggbbaa.");
        var value = uint.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Vector4((value >> 24 & 0xff) / 255f, (value >> 16 & 0xff) / 255f, (value >> 8 & 0xff) / 255f,
            (value & 0xff) / 255f);
    }

    private static IEnumerable<string> ClassList(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes)) yield break;
        var text = classes.Trim();
        if (text.StartsWith('[') && text.EndsWith(']')) text = text[1..^1];
        foreach (var name in text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)) yield return name;
    }

    private sealed class Row<T>(T item, State<int> index, Region holder, Scope scope)
    {
        public T Item { get; } = item;
        public State<int> Index { get; } = index;
        public Region Holder { get; } = holder;
        public Scope Scope { get; } = scope;

        public void End(Region region)
        {
            Scope.Dispose();
            Holder.Dispose();
        }
    }

    private sealed class NullKey
    {
        public static readonly NullKey Instance = new();
    }

    private sealed record DuplicateKey(object Key, int Index);
}
