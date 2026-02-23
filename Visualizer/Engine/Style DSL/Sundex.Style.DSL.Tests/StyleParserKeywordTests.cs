using Sundex.Style.DSL.Abstract.Values;
using Sundex.Style.DSL.Abstract.Values.Keywords;

namespace Sundex.Style.DSL.Tests;

public class StyleParserKeywordTests
{
    private static string GetProjectRoot()
    {
        var basePath = AppContext.BaseDirectory;
        var projectRoot = basePath;
        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "ThirtyDollarTools.sln")))
        {
            projectRoot = Path.GetDirectoryName(projectRoot);
        }

        return projectRoot!;
    }

    [Test]
    public void KeyframesValue_TypedAccess_Works()
    {
        var root = GetProjectRoot();
        var path = Path.Combine(root, "Visualizer/Engine/Style DSL/Sundex.Style.DSL/Examples/default.snxs");
        var dsl = File.ReadAllText(path);
        var sheet = StyleParser.Parse(dsl);

        var anim = sheet.Animations["fade-in"];
        var kf = (KeyframesValue)anim["keyframes"];

        Assert.That(kf.Keyframes, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(kf.Keyframes[0].Percentage, Is.EqualTo(0));
            Assert.That(kf.Keyframes[1].Percentage, Is.EqualTo(100));
            Assert.That(kf.Keyframes[0].Properties.ContainsKey("opacity"), Is.True);
            Assert.That(kf.Keyframes[1].Properties.ContainsKey("opacity"), Is.True);
        }
    }

    [Test]
    public void GradientValue_Linear_TypedAccess_Works()
    {
        var root = GetProjectRoot();
        var path = Path.Combine(root, "Visualizer/Engine/Style DSL/Sundex.Style.DSL/Examples/default.snxs");
        var dsl = File.ReadAllText(path);
        var sheet = StyleParser.Parse(dsl);

        var cls = sheet.Classes["gradient-linear"];
        var grad = (GradientValue)cls["background"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(grad.Type, Is.EqualTo("linear"));
            Assert.That(grad.Direction, Is.TypeOf<NumberValue>());
            var dir = (NumberValue)grad.Direction!;
            Assert.That(dir.Unit, Is.EqualTo("deg"));
            Assert.That(dir.Value, Is.EqualTo(90));
        }

        Assert.That(grad.Stops, Has.Count.EqualTo(3));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(grad.Stops[0].Color.Value, Is.EqualTo("#ff0000"));
            Assert.That(grad.Stops[0].Percentage, Is.EqualTo(0));
            Assert.That(grad.Stops[1].Color.Value, Is.EqualTo("#ffff00"));
            Assert.That(grad.Stops[1].Percentage, Is.EqualTo(50));
            Assert.That(grad.Stops[2].Color.Value, Is.EqualTo("#00ff00"));
            Assert.That(grad.Stops[2].Percentage, Is.EqualTo(100));
        }
    }

    [Test]
    public void GradientValue_Radial_TypedAccess_Works()
    {
        var root = GetProjectRoot();
        var path = Path.Combine(root, "Visualizer/Engine/Style DSL/Sundex.Style.DSL/Examples/default.snxs");
        var dsl = File.ReadAllText(path);
        var sheet = StyleParser.Parse(dsl);

        var cls = sheet.Classes["gradient-radial"];
        var grad = (GradientValue)cls["background"];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(grad.Type, Is.EqualTo("radial"));
            Assert.That(grad.Direction, Is.TypeOf<DirectionValue>());
            var dir = (DirectionValue)grad.Direction!;
            Assert.That(dir.Value.Value, Is.EqualTo("outward"));
        }

        Assert.That(grad.Stops, Has.Count.EqualTo(3));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(grad.Stops[0].Color.Value, Is.EqualTo("#ff0000"));
            Assert.That(grad.Stops[0].Percentage, Is.EqualTo(0));
            Assert.That(grad.Stops[1].Color.Value, Is.EqualTo("#ffff00"));
            Assert.That(grad.Stops[1].Percentage, Is.EqualTo(50));
            Assert.That(grad.Stops[2].Color.Value, Is.EqualTo("#00ffff"));
            Assert.That(grad.Stops[2].Percentage, Is.EqualTo(100));
        }
    }

    [Test]
    public void OverrideValue_TypedAccess_Works()
    {
        var root = GetProjectRoot();
        var path = Path.Combine(root, "Visualizer/Engine/Style DSL/Sundex.Style.DSL/Examples/default.snxs");
        var dsl = File.ReadAllText(path);
        var sheet = StyleParser.Parse(dsl);

        var button = sheet.Components["button"];
        var ov = (OverrideValue)button["state[pressed]"];

        Assert.That(ov.Properties.Properties, Is.Not.Empty);
        Assert.That(ov.Properties.Properties.ContainsKey("background"), Is.True);
    }
}
