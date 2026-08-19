using System.Buffers;
using System.Reflection;
using Sundex.Components.Abstractions;
using Sundex.Markup.Abstract;
using Sundex.Markup.Attributes;
using Sundex.Markup.Builders;
using Sundex.Markup.Logic;
using Sundex.Engine.Asset_Management.Types.String;
using StringInfo = Sundex.Engine.Asset_Management.Types.String.StringInfo;

namespace Sundex.Markup;

public class SundexContext(UIContext context) : ISundexContext
{
    public Dictionary<string, IComponentBuilder> ComponentBuilderVersions { get; } = new()
    {
        { ComponentBuilderV1.Version, new ComponentBuilderV1() }
    };

    public Dictionary<string, ISundexComponent> LoadedComponents { get; } = [];
    public Dictionary<string, Func<UIContext, UIElement>> ElementFactories { get; } = [];
    public UIContext UIContext { get; } = context;

    public ISundexComponent ResolveComponent(ReadOnlySpan<char> dependency)
    {
        var lookup = LoadedComponents.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(dependency, out var component)
            ? component
            : throw new Exception($"Unable to find component: {dependency}");
    }

    public void RegisterComponent(ISundexComponent component)
    {
        if (component.Name == null) throw new Exception("Component name cannot be null.");
        LoadedComponents.Add(component.Name, component);
    }

    public void RegisterElementFactory(string tagName, Func<UIContext, UIElement> factory)
    {
        if (!ElementFactories.TryAdd(tagName, factory))
            throw new Exception($"A factory for tag '{tagName}' is already registered.");
    }

    public UIElement? CreateElement(string tagName)
    {
        var lookup = ElementFactories.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.TryGetValue(tagName, out var factory) ? factory(UIContext) : null;
    }

    public void UnregisterComponent(ISundexComponent component)
    {
        if (component.Name == null) throw new Exception("Component name cannot be null.");
        LoadedComponents.Remove(component.Name);
    }

    public SundexComponent NewComponent(string smxlMarkup)
    {
        var layout = MarkupParser.Parse(smxlMarkup);
        var version = layout.Root.Version;

        var lookup = ComponentBuilderVersions.GetAlternateLookup<ReadOnlySpan<char>>();
        return !lookup.TryGetValue(version, out var builder)
            ? throw new Exception("No builder found for the version specified in the document markup.")
            : builder.CreateComponent(layout, this);
    }

    /// <summary>
    ///     Compiles the logic block of every markup document embedded in the asset
    ///     assemblies, without building a single element.
    ///     <para>
    ///         Compiling a logic block is the one genuinely expensive step in building a
    ///         component - two orders of magnitude above everything else - and it needs no
    ///         graphics context, so it does not have to happen on the render thread on the
    ///         frame a scene is opened. Called from a worker while the program has time to
    ///         spare, it turns every later <see cref="NewComponent" /> into a cache hit.
    ///     </para>
    ///     <para>
    ///         Documents are found rather than listed, so a new screen is covered by
    ///         existing on the disk. Anything that fails to parse or compile is skipped:
    ///         this is an optimisation, and the real build will raise the error properly
    ///         with the component it belongs to.
    ///     </para>
    /// </summary>
    /// <returns>How many logic blocks were compiled.</returns>
    public int PrecompileLogic()
    {
        var documents =
            from assembly in UIContext.AssetProvider.AssetAssemblies
            from resource in assembly.GetManifestResourceNames()
            where resource.EndsWith(".snx.xml", StringComparison.Ordinal)
            select (assembly, resource);

        var compiled = 0;

        // Half the machine, not all of it. Compilations are independent and the asset
        // loaders behind them hold no shared state, so this scales - but whatever called
        // this is still drawing, and saturating every core would trade the hitch this
        // exists to remove for a stuttering screen while it runs.
        Parallel.ForEach(documents,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) },
            document =>
            {
                using var stream = document.assembly.GetManifestResourceStream(document.resource);
                if (stream is null) return;

                using var reader = new StreamReader(stream);
                if (PrecompileLogic(reader.ReadToEnd())) Interlocked.Increment(ref compiled);
            });

        return compiled;
    }

    /// <summary>
    ///     Compiles one markup document's logic block. See <see cref="PrecompileLogic()" />.
    /// </summary>
    /// <returns>Whether there was a logic block, and it compiled.</returns>
    public bool PrecompileLogic(string smxlMarkup)
    {
        try
        {
            var logic = MarkupParser.Parse(smxlMarkup).Logic;
            if (logic is null) return false;
            if (!LanguageProvider.Languages.TryGetValue(logic.Language, out var language)) return false;

            // Same source resolution ComponentBuilderV1 does, and it has to happen here
            // too: the compiled script is keyed on the source, and the markup carries a
            // path rather than the code itself.
            if (!string.IsNullOrEmpty(logic.SrcLocation))
                logic.UpdateSourceCode(UIContext.AssetProvider
                    .Load<StringAsset, StringInfo>(StringInfo.CreateFromUnknownStorage(logic.SrcLocation)).Value);

            language.Precompile(logic.SourceCode, this, logic.LanguageImports);
            return true;
        }
        catch
        {
            // Deliberately swallowed - see the remarks on PrecompileLogic().
            return false;
        }
    }

    public void RegisterBuilder(string version, IComponentBuilder builder)
    {
        if (!ComponentBuilderVersions.TryAdd(version, builder))
            throw new Exception("A builder with the same version already exists.");
    }

    public void RunLogicAndVerify(SundexComponent component, params ReadOnlySpan<Func<object?>> objectGetters)
    {
        var previousValuesRent = ArrayPool<object?>.Shared.Rent(objectGetters.Length);
        try
        {
            var previousValues = previousValuesRent.AsSpan()[..objectGetters.Length];
            object? target = null;
            for (var index = 0; index < objectGetters.Length; index++)
            {
                var obj = objectGetters[index].Invoke();
                previousValues[index] = obj;
                if (target == null && objectGetters[index].Target != null)
                    target = objectGetters[index].Target;
            }

            component.RunLogic?.Invoke(target ?? this);

            for (var i = 0; i < objectGetters.Length; i++)
            {
                var oldObj = previousValues[i];
                var newObj = objectGetters[i].Invoke();
                if (newObj is null && oldObj is not null)
                    throw new Exception(
                        $"Object getter {objectGetters[i]} returned null but was expecting a non-null value.");
            }

            if (target == null) return;
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            foreach (var property in type.GetProperties(flags))
            {
                if (property.GetCustomAttribute<SetFromLogicAttribute>() == null) continue;
                if (property.GetValue(target) == null)
                    throw new Exception(
                        $"Property {property.Name} in {type.Name} was marked with [SetFromLogic] but remains null after logic execution.");
            }

            foreach (var field in type.GetFields(flags))
            {
                if (field.GetCustomAttribute<SetFromLogicAttribute>() == null) continue;
                if (field.GetValue(target) == null)
                    throw new Exception(
                        $"Field {field.Name} in {type.Name} was marked with [SetFromLogic] but remains null after logic execution.");
            }

            const BindingFlags nonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (var property in type.GetProperties(nonPublicFlags))
                if (property.GetCustomAttribute<SetFromLogicAttribute>() != null)
                    throw new Exception(
                        $"Property {property.Name} in {type.Name} is marked with [SetFromLogic] but is not public.");

            foreach (var field in type.GetFields(nonPublicFlags))
                if (field.GetCustomAttribute<SetFromLogicAttribute>() != null)
                    throw new Exception(
                        $"Field {field.Name} in {type.Name} is marked with [SetFromLogic] but is not public.");
        }
        finally
        {
            ArrayPool<object?>.Shared.Return(previousValuesRent);
        }
    }
}