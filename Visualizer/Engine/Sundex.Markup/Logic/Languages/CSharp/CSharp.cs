using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Sundex.Markup.Abstract;

namespace Sundex.Markup.Logic.Languages.CSharp;

public class CSharp : SundexScript
{
    public override Action<object?> Compile(string sourceCode, ISundexContext context,
        SundexComponent component, List<string> logicLanguageImports)
    {
        /* AddReferences doesn't work for some reason on SingleFilePublish when -p:IncludeAllContentForSelfExtract is not enabled.
         * See: https://github.com/dotnet/roslyn/issues/50719 */
        var options = ScriptOptions.Default
            .AddReferences([typeof(CSharp).Assembly, ..context.UIContext.AssetProvider.AssetAssemblies])
            .AddImports("System", "Sundex.Markup.Abstract", "Sundex.Markup.Logic.Languages.CSharp", "Sundex.Components",
                "Sundex.Components.Abstractions")
            .AddImports(logicLanguageImports);

        var script = CSharpScript.Create(sourceCode, options, typeof(ScriptGlobals));
        script.Compile();

        return obj =>
        {
            var globals = new ScriptGlobals(context, component, obj);
            script.RunAsync(globals).GetAwaiter().GetResult();
        };
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