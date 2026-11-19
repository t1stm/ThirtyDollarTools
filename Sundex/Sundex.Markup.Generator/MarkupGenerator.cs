using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Sundex.Markup.Generator;

/// <summary>
///     Compiles every <c>version="2.0"</c> <c>.snx.xml</c> in a project's AdditionalFiles into a
///     <c>MarkupComponent</c> class. Reads three MSBuild properties, which the project has to make
///     visible with <c>CompilerVisibleProperty</c>: <c>RootNamespace</c>, <c>SundexMarkupNamespace</c>
///     (where the classes go; defaults to the root namespace) and <c>SundexMarkupContext</c> (the
///     context type documents get unless they name their own).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MarkupGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var documents = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".snx.xml", StringComparison.OrdinalIgnoreCase))
            .Select((text, token) => MarkupDocument.Parse(text.Path, text.GetText(token)?.ToString() ?? ""))
            .Where(document => document.Version == "2.0" || document.Error != null)
            .Collect();

        var sheets = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".snx.ss", StringComparison.OrdinalIgnoreCase))
            .Select((text, token) => StyleIndex.Parse(text.Path, text.GetText(token)?.ToString() ?? ""))
            .Collect();

        var logicFiles = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".snx.csx", StringComparison.OrdinalIgnoreCase))
            .Select((text, token) => (text.Path, Text: text.GetText(token)?.ToString() ?? ""))
            .Collect();

        var settings = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
        {
            options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var root);
            options.GlobalOptions.TryGetValue("build_property.SundexMarkupNamespace", out var namespaceName);
            options.GlobalOptions.TryGetValue("build_property.SundexMarkupContext", out var markupContext);
            return new Settings(string.IsNullOrEmpty(root) ? "Markup" : root!, namespaceName,
                string.IsNullOrEmpty(markupContext) ? null : markupContext);
        });

        context.RegisterSourceOutput(
            documents.Combine(sheets).Combine(logicFiles).Combine(settings).Combine(context.CompilationProvider),
            (output, input) => Generate(output, input.Left.Left.Left.Left, input.Left.Left.Left.Right,
                input.Left.Left.Right, input.Left.Right, input.Right));
    }

    private static void Generate(SourceProductionContext output, ImmutableArray<MarkupDocument> documents,
        ImmutableArray<StyleIndex> sheets, ImmutableArray<(string Path, string Text)> logicFiles, Settings settings,
        Compilation compilation)
    {
        var diagnostics = new List<Diagnostic>();
        var components = new Dictionary<string, MarkupDocument>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            if (document.Error != null)
            {
                diagnostics.Add(Diagnostics.At(Diagnostics.Parse, document.Path, document.ErrorSpot, document.Error));
                continue;
            }

            if (components.TryGetValue(document.Tag, out var first))
            {
                diagnostics.Add(Diagnostics.At(Diagnostics.DuplicateTag, document.Path, new Spot(1, 1), document.Tag, first.Path));
                continue;
            }

            components[document.Tag] = document;
        }

        foreach (var sheet in sheets)
        foreach (var line in sheet.StateInMediaLines)
            diagnostics.Add(Diagnostics.At(Diagnostics.StateInMedia, sheet.Path, new Spot(line, 1)));

        var emitter = new Emitter(compilation, settings, components, diagnostics, sheets, logicFiles);
        foreach (var document in components.Values.OrderBy(candidate => candidate.Path, StringComparer.Ordinal))
        {
            string? source;
            try
            {
                source = emitter.Emit(document);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Diagnostics.At(Diagnostics.Crash, document.Path, new Spot(1, 1), exception.ToString()));
                continue;
            }

            if (source != null)
                output.AddSource(document.ClassName + ".snx.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        foreach (var diagnostic in diagnostics) output.ReportDiagnostic(diagnostic);
    }
}
