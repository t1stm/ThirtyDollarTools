using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Style.DSL.Tests;

public class StyleParserTests
{
    [Test]
    public void Parse_Empty_ReturnsEmptyStyleSheet()
    {
        var sheet = StyleParser.Parse("");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Animations, Is.Empty);
            Assert.That(sheet.Components, Is.Empty);
            Assert.That(sheet.Classes, Is.Empty);
            Assert.That(sheet.IDTags, Is.Empty);
        }
    }

    [Test]
    public void Parse_ComponentWithProperties_ParsesCorrectly()
    {
        const string dsl = """

                           component button {
                               padding = 5px;
                               background = "#010101ff";
                           }
                           """;
        var sheet = StyleParser.Parse(dsl);
        Assert.That(sheet.Components, Has.Count.EqualTo(1));
        Assert.That(sheet.Components.ContainsKey("button"), Is.True);
        var button = sheet.Components["button"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(button["padding"], Is.TypeOf<NumberValue>());
            Assert.That(((NumberValue)button["padding"]).Value, Is.EqualTo(5));
            Assert.That(((NumberValue)button["padding"]).Unit, Is.EqualTo("px"));
            Assert.That(button["background"], Is.TypeOf<ColorValue>());
            Assert.That(((ColorValue)button["background"]).Value, Is.EqualTo("#010101ff"));
        }
    }

    [Test]
    public void Parse_AnimationWithKeyframes_ParsesCorrectly()
    {
        const string dsl = """

                           animation fade-in {
                               timing-function = "ease-in";
                               duration = 1s;
                               keyframes = !keyframes [
                                   0% = { opacity = 0 },
                                   100% = { opacity = 1 }
                               ];
                           }
                           """;
        var sheet = StyleParser.Parse(dsl);
        Assert.That(sheet.Animations, Has.Count.EqualTo(1));
        var anim = sheet.Animations["fade-in"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(anim["timing-function"].Value, Is.EqualTo("ease-in"));
            Assert.That(anim["duration"], Is.TypeOf<NumberValue>());
        }

        var keyframesKeyword = (KeyframesValue)anim["keyframes"];
        var map = (MapValue)keyframesKeyword.Elements;
        Assert.That(map.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_VectorValue_ParsesCorrectly()
    {
        const string dsl = """

                           class test {
                               transform = vec2(10, -20);
                           }
                           """;
        var sheet = StyleParser.Parse(dsl);
        var cls = sheet.Classes["test"];
        var vec = (VectorValue)cls["transform"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(vec.X, Is.EqualTo(10));
            Assert.That(vec.Y, Is.EqualTo(-20));
        }
    }

    [Test]
    public void Parse_DefaultSnxsExample_ParsesWithoutErrors()
    {
        var basePath = AppContext.BaseDirectory;
        // Try to find the project root
        var projectRoot = basePath;
        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "ThirtyDollarTools.slnx")))
            projectRoot = Path.GetDirectoryName(projectRoot);

        var path = Path.Combine(projectRoot!, "Sundex/Sundex.Style.DSL/Examples/default.snx.ss");
        var dsl = File.ReadAllText(path);
        var sheet = StyleParser.Parse(dsl);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Animations, Has.Count.EqualTo(3));
            Assert.That(sheet.Components, Has.Count.EqualTo(4));
            Assert.That(sheet.Classes, Has.Count.EqualTo(3));
            Assert.That(sheet.IDTags, Has.Count.EqualTo(1));
        }

        // Specific check for button states
        var button = sheet.Components["button"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(button.ContainsKey("state[pressed]"), Is.True);
            Assert.That(button.ContainsKey("state[hovered]"), Is.True);
        }
    }

    [Test]
    public void Parse_ImportSnxsExample_ParsesWithoutErrors()
    {
        var basePath = AppContext.BaseDirectory;
        var projectRoot = basePath;
        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "ThirtyDollarTools.slnx")))
            projectRoot = Path.GetDirectoryName(projectRoot);

        var path = Path.Combine(projectRoot!, "Sundex/Sundex.Style.DSL/Examples/import.snx.ss");
        var dsl = File.ReadAllText(path);

        // Use the directory of the style DSL project as base path for the examples to work
        var styleDslRoot = Path.Combine(projectRoot!, "Sundex");
        var sheet = StyleParser.Parse(dsl, p => File.ReadAllText(Path.Combine(styleDslRoot, p)));

        Assert.That(sheet.Components, Has.Count.EqualTo(4));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Components.ContainsKey("button"), Is.True);
            Assert.That(sheet.Components.ContainsKey("label"), Is.True);
            Assert.That(sheet.FullOverrides, Contains.Item("button"));
        }

        Assert.That(sheet.FullOverrides, Does.Not.Contain("label"));

        // Verify that label has properties from both default.snx.ss and import.snx.ss
        var label = sheet.Components["label"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(label["font-size"], Is.TypeOf<NumberValue>()); // From default.snx.ss
            Assert.That(label["font-color"].Value, Is.EqualTo("#000000")); // Overridden in import.snx.ss
            Assert.That(label["background"].Value, Is.EqualTo("#ffffff")); // Overridden in import.snx.ss
        }

        // Verify button is a full override
        var button = sheet.Components["button"];
        Assert.That(button.ContainsKey("font-size"), Is.False); // font-size was in default.snx.ss but should be gone
    }

    [Test]
    public void Parse_RecursiveImport_DoesNotInfiniteLoop()
    {
        var files = new Dictionary<string, string>
        {
            ["a.snxs"] = "import \"b.snxs\"; component a { color = \"red\" }",
            ["b.snxs"] = "import \"a.snxs\"; component b { color = \"blue\" }"
        };

        var sheet = StyleParser.Parse(files["a.snxs"], path => files[Path.GetFileName(path)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sheet.Components.ContainsKey("a"), Is.True);
            Assert.That(sheet.Components.ContainsKey("b"), Is.True);
        }
    }

    [Test]
    public void Parse_ImportWithPropertyMerging_MergesCorrectly()
    {
        var files = new Dictionary<string, string>
        {
            ["base.snxs"] = "component btn { padding = 10px; color = \"blue\" }",
            ["main.snxs"] = "import \"base.snxs\"; component btn { color = \"red\" }"
        };

        var sheet = StyleParser.Parse(files["main.snxs"], path => files[Path.GetFileName(path)]);

        var btn = sheet.Components["btn"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((NumberValue)btn["padding"]).Value, Is.EqualTo(10));
            Assert.That(btn["color"].Value, Is.EqualTo("red"));
        }
    }
}