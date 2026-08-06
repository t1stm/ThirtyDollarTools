using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Style.DSL.Tests;

public class StyleParserVariableTests
{
    [Test]
    public void Parse_Variable_SubstitutesTypedValue()
    {
        const string dsl = """
                           var text_color = "#aaaaaaff";
                           var padding_lg = 20px;

                           class card {
                               color = $text_color;
                               padding = $padding_lg;
                           }
                           """;

        var sheet = StyleParser.Parse(dsl);
        var card = sheet.Classes["card"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(card["color"], Is.TypeOf<ColorValue>());
            Assert.That(card["color"].Value, Is.EqualTo("#aaaaaaff"));
            Assert.That(((NumberValue)card["padding"]).Value, Is.EqualTo(20));
            Assert.That(((NumberValue)card["padding"]).Unit, Is.EqualTo("px"));
        }
    }

    [Test]
    public void Parse_VariableInsideNestedValues_Substitutes()
    {
        const string dsl = """
                           var accent = "#ff0000ff";
                           var offset = 4;

                           class card {
                               state[hovered] = { color = $accent; };
                               shadow = [ $accent, $accent ];
                               transform = vec2($offset, $offset);
                           }
                           """;

        var sheet = StyleParser.Parse(dsl);
        var card = sheet.Classes["card"];

        var state = (BlockValue)card["state[hovered]"];
        var array = (ArrayValue)card["shadow"];
        var vector = (VectorValue)card["transform"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.Properties["color"].Value, Is.EqualTo("#ff0000ff"));
            Assert.That(array.Values, Has.Count.EqualTo(2));
            Assert.That(array.Values[0].Value, Is.EqualTo("#ff0000ff"));
            Assert.That(vector.X, Is.EqualTo(4));
            Assert.That(vector.Y, Is.EqualTo(4));
        }
    }

    [Test]
    public void Parse_UndefinedVariable_Throws()
    {
        const string dsl = "class card { color = $nope; }";
        Assert.That(() => StyleParser.Parse(dsl),
            Throws.Exception.With.Message.Contains("Unknown variable 'nope'"));
    }

    [Test]
    public void Parse_DuplicateVariableInSameFile_Throws()
    {
        const string dsl = """
                           var accent = "#ff0000ff";
                           var accent = "#00ff00ff";
                           """;

        Assert.That(() => StyleParser.Parse(dsl),
            Throws.Exception.With.Message.Contains("already defined"));
    }

    [Test]
    public void Parse_LocalVariableShadowingImport_Overrides()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = "var accent = \"#ff0000ff\";",
            ["main.snxs"] = """
                            import "theme.snxs";
                            var accent = "#00ff00ff";
                            class card { color = $accent; }
                            """
        };

        var sheet = StyleParser.Parse(files["main.snxs"], path => files[path]);
        Assert.That(sheet.Classes["card"]["color"].Value, Is.EqualTo("#00ff00ff"));
    }

    [Test]
    public void Parse_PlainImport_MergesVariablesGlobally()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = "var accent = \"#ff0000ff\";",
            ["main.snxs"] = """
                            import "theme.snxs";
                            class card { color = $accent; }
                            """
        };

        var sheet = StyleParser.Parse(files["main.snxs"], path => files[path]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Classes["card"]["color"].Value, Is.EqualTo("#ff0000ff"));
            Assert.That(sheet.Variables.ContainsKey("accent"), Is.True);
        }
    }

    [Test]
    public void Parse_NamedImport_ScopesVariablesButMergesBlocks()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = """
                             var accent = "#ff0000ff";
                             component button { padding = 8px; }
                             """,
            ["main.snxs"] = """
                            import "theme.snxs" as theme;
                            class card { color = $theme.accent; }
                            """
        };

        var sheet = StyleParser.Parse(files["main.snxs"], path => files[path]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Classes["card"]["color"].Value, Is.EqualTo("#ff0000ff"));
            // classes, ids and components stay global even for an aliased import
            Assert.That(sheet.Components.ContainsKey("button"), Is.True);
            // but the variable does not leak into the global scope
            Assert.That(sheet.Variables.ContainsKey("accent"), Is.False);
        }
    }

    [Test]
    public void Parse_NamedImportVariableUsedUnqualified_Throws()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = "var accent = \"#ff0000ff\";",
            ["main.snxs"] = """
                            import "theme.snxs" as theme;
                            class card { color = $accent; }
                            """
        };

        Assert.That(() => StyleParser.Parse(files["main.snxs"], path => files[path]),
            Throws.Exception.With.Message.Contains("Unknown variable 'accent'"));
    }

    [Test]
    public void Parse_UnknownAliasOrMember_Throws()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = "var accent = \"#ff0000ff\";",
            ["missing-alias.snxs"] = "class card { color = $nope.accent; }",
            ["missing-member.snxs"] = """
                                      import "theme.snxs" as theme;
                                      class card { color = $theme.nope; }
                                      """
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => StyleParser.Parse(files["missing-alias.snxs"], path => files[path]),
                Throws.Exception.With.Message.Contains("Unknown import alias 'nope'"));
            Assert.That(() => StyleParser.Parse(files["missing-member.snxs"], path => files[path]),
                Throws.Exception.With.Message.Contains("has no variable 'nope'"));
        }
    }

    [Test]
    public void Parse_SamePathPlainAndAliased_ResolvesBoth()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = "var accent = \"#ff0000ff\";",
            ["main.snxs"] = """
                            import "theme.snxs";
                            import "theme.snxs" as theme;
                            class card { color = $accent; border = $theme.accent; }
                            """
        };

        var sheet = StyleParser.Parse(files["main.snxs"], path => files[path]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Classes["card"]["color"].Value, Is.EqualTo("#ff0000ff"));
            Assert.That(sheet.Classes["card"]["border"].Value, Is.EqualTo("#ff0000ff"));
        }
    }

    [Test]
    public void Parse_SamePathAliasedThenPlain_StillMergesVariables()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = "var accent = \"#ff0000ff\";",
            ["main.snxs"] = """
                            import "theme.snxs" as theme;
                            import "theme.snxs";
                            class card { color = $accent; }
                            """
        };

        var sheet = StyleParser.Parse(files["main.snxs"], path => files[path]);
        Assert.That(sheet.Classes["card"]["color"].Value, Is.EqualTo("#ff0000ff"));
    }

    [Test]
    public void Parse_AliasIsFileLocal_NotVisibleToImporter()
    {
        var files = new Dictionary<string, string>
        {
            ["theme.snxs"] = "var accent = \"#ff0000ff\";",
            ["middle.snxs"] = "import \"theme.snxs\" as theme;",
            ["main.snxs"] = """
                            import "middle.snxs";
                            class card { color = $theme.accent; }
                            """
        };

        Assert.That(() => StyleParser.Parse(files["main.snxs"], path => files[path]),
            Throws.Exception.With.Message.Contains("Unknown import alias 'theme'"));
    }

    [Test]
    public void Parse_CircularImportWithVariables_Terminates()
    {
        var files = new Dictionary<string, string>
        {
            ["a.snxs"] = """
                         import "b.snxs";
                         var a_color = "#ff0000ff";
                         class a { color = $a_color; }
                         """,
            ["b.snxs"] = """
                         import "a.snxs";
                         var b_color = "#00ff00ff";
                         class b { color = $b_color; }
                         """
        };

        var sheet = StyleParser.Parse(files["a.snxs"], path => files[path]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Classes.ContainsKey("a"), Is.True);
            Assert.That(sheet.Classes.ContainsKey("b"), Is.True);
        }
    }

    [Test]
    public void Parse_VariablesSnxsExample_ParsesWithoutErrors()
    {
        var projectRoot = AppContext.BaseDirectory;
        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "ThirtyDollarTools.slnx")))
            projectRoot = Path.GetDirectoryName(projectRoot);

        var styleDslRoot = Path.Combine(projectRoot!, "Sundex");
        var dsl = File.ReadAllText(Path.Combine(styleDslRoot, "Sundex.Style.DSL/Examples/variables.snx.ss"));

        var sheet = StyleParser.Parse(dsl, p => File.ReadAllText(Path.Combine(styleDslRoot, p)));
        var card = sheet.Classes["card"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Components["label"]["font-color"].Value, Is.EqualTo("#ffffffff"));
            Assert.That(((NumberValue)card["padding"]).Value, Is.EqualTo(20));
            Assert.That(card["shadow"], Is.TypeOf<BlockValue>());
            // local declaration shadows the imported one
            Assert.That(card["border-color"].Value, Is.EqualTo("#00ff00ff"));
            // the alias still holds the original
            var hovered = ((OverrideValue)card["state[hovered]"]).Properties;
            Assert.That(hovered.Properties["border-color"].Value, Is.EqualTo("#ff0000ff"));
        }
    }

    [Test]
    public void Parse_UnderscoreIdentifiers_AreValidNames()
    {
        const string dsl = """
                           class my_card {
                               my_property = 4px;
                           }
                           """;

        var sheet = StyleParser.Parse(dsl);
        Assert.That(((NumberValue)sheet.Classes["my_card"]["my_property"]).Value, Is.EqualTo(4));
    }
}
