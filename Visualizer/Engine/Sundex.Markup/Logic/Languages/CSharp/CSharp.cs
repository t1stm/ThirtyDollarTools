using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Sundex.Markup.Abstract;

namespace Sundex.Markup.Logic.Languages.CSharp;

public class CSharp : SundexScript
{
    public class ScriptGlobals(ISundexContext sundex, object? contextObject)
    {
        public ISundexContext Sundex { get; } = sundex;
        public object? Context { get; set; } = contextObject;

        public static T As<T>(object? obj)
        {
            return obj is T casted
                ? casted
                : throw new InvalidCastException($"Cannot cast {obj?.GetType()} to {typeof(T)}");
        }
    }

    public override Action<object?> Compile(string sourceCode, ISundexContext context,
        List<string> logicLanguageImports)
    {
        /* TODO: AddReferences doesn't work for some reason on SingleFilePublish. 
         * I don't want to downgrade this to multiple files since it'll look incredibly ugly with the amount of dependencies we have...
         * See: https://github.com/dotnet/roslyn/issues/50719 */
        var options = ScriptOptions.Default
            .AddReferences([typeof(CSharp).Assembly, ..context.UIContext.AssetProvider.AssetAssemblies])
            .AddImports("System", "Sundex.Markup.Abstract", "Sundex.Markup.Logic.Languages.CSharp", "Sundex.Components")
            .AddImports(logicLanguageImports);

        var script = CSharpScript.Create(sourceCode, options, typeof(ScriptGlobals));
        script.Compile();

        return obj =>
        {
            var globals = new ScriptGlobals(context, obj);
            script.RunAsync(globals).GetAwaiter().GetResult();
        };
    }
}