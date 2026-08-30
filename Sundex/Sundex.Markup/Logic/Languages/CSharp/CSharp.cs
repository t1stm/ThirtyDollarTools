using System.Collections.Concurrent;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Sundex.Markup.Abstract;

namespace Sundex.Markup.Logic.Languages.CSharp;

public class CSharp : SundexScript
{
    // Compiled scripts, keyed by imports plus source. Roslyn compilation is by far the
    // most expensive step in building a component; the compiled Script is immutable and
    // takes its globals per run, so one instance serves every component built from the
    // same source and a sub-component compiles once across all its usage sites.
    //
    // Lazy rather than a bare Script: the loading screen precompiles these on a worker
    // (see SundexContext.PrecompileLogic) while the render thread may reach the same
    // source first. Whoever gets there second blocks on the compile already running
    // instead of starting a second one.
    // ponytail: keyed on source+imports only, so two contexts with different asset
    // assemblies would share a script. Add the assembly set to the key if that ever
    // becomes real; today AssetAssemblies is process-wide.
    private static readonly ConcurrentDictionary<string, Lazy<Script<object>>> ScriptCache =
        new(StringComparer.Ordinal);

    public override Action<object?> Compile(string sourceCode, ISundexContext context,
        SundexComponent component, List<string> logicLanguageImports)
    {
        var script = GetOrCompile(sourceCode, context, logicLanguageImports);

        // Captures this component, not the cached script's first one: a rebuilt usage site
        // must run its logic against its own RegisteredIDs or GetID wires the template's
        // elements instead of the ones actually in the tree.
        return obj =>
        {
            var globals = new ScriptGlobals(context, component, obj);
            script.RunAsync(globals).GetAwaiter().GetResult();
        };
    }

    /// <inheritdoc />
    public override void Precompile(string sourceCode, ISundexContext context,
        List<string> logicLanguageImports)
    {
        GetOrCompile(sourceCode, context, logicLanguageImports);
    }

    private static Script<object> GetOrCompile(string sourceCode, ISundexContext context,
        List<string> logicLanguageImports)
    {
        var cacheKey = $"{string.Join(',', logicLanguageImports)} {sourceCode}";
        // The assemblies are read out here rather than inside the factory: GetOrAdd can
        // run a losing factory concurrently, and the context must not be touched from it.
        var assemblies = context.UIContext.AssetProvider.AssetAssemblies;

        return ScriptCache.GetOrAdd(cacheKey,
            _ => new Lazy<Script<object>>(() => Build(sourceCode, logicLanguageImports, assemblies))).Value;
    }

    private static Script<object> Build(string sourceCode, List<string> logicLanguageImports,
        Assembly[] assetAssemblies)
    {
        /* AddReferences doesn't work for some reason on SingleFilePublish when -p:IncludeAllContentForSelfExtract is not enabled.
         * See: https://github.com/dotnet/roslyn/issues/50719 */
        var options = ScriptOptions.Default
            .AddReferences([typeof(CSharp).Assembly, .. assetAssemblies])
            .AddImports("System", "Sundex.Markup.Abstract", "Sundex.Markup.Logic.Languages.CSharp",
                "Sundex.Components",
                "Sundex.Components.Abstractions")
            .AddImports(logicLanguageImports);

        var script = CSharpScript.Create(sourceCode, options, typeof(ScriptGlobals));

        // Compile() reports errors rather than raising them, so surface them here: an
        // unchecked script stays silent until its first run, which for a scene's wiring
        // means when the scene is opened.
        var diagnostics = script.Compile();
        if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            throw new CompilationErrorException(
                string.Join(Environment.NewLine, diagnostics.Where(diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error)),
                diagnostics);

        return script;
    }

    public class ScriptGlobals(ISundexContext sundex, SundexComponent component, object? contextObject)
    {
        [UsedImplicitly] public ISundexContext Sundex { get; } = sundex;

        [UsedImplicitly] public SundexComponent Component { get; } = component;

        [UsedImplicitly] public object? Context { get; set; } = contextObject;

        [UsedImplicitly]
        public static T As<T>(object? obj)
        {
            return obj is T casted
                ? casted
                : throw new InvalidCastException($"Cannot cast {obj?.GetType()} to {typeof(T)}");
        }
    }
}