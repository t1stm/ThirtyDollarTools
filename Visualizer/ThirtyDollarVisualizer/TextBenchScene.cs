using System.Diagnostics;
using System.Text;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Shared;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Engine;
using Sundex.Engine.Renderer.Debug;
using Sundex.Engine.Scenes;
using Sundex.Engine.Scenes.Arguments;

namespace ThirtyDollarVisualizer;

/// <summary>
///     Answers one question: does a window full of text take more than
///     <see cref="GateMs" /> of GPU time to draw? Started with <c>--mode text-bench</c>; see
///     docs/handover/sundex-slug-plan.md, Phase 2.
///     <para>
///         Fills the window with seeded printable ASCII, one <see cref="Label" /> per line, at
///         each size in <see cref="Variants" /> in turn. Each variant warms up, then times the
///         scene's whole render on the GPU and the CPU, logs one <c>[TextBench]</c> line, and
///         the window closes after the last with exit code 1 if any variant's p95 is over the
///         gate.
///     </para>
/// </summary>
public class TextBenchScene : Scene
{
    private const double GateMs = 4;
    private const int WarmupFrames = 120;
    private const int MeasuredFrames = 600;

    private static readonly (string Name, float SizePx)[] Variants = [("ui", 14), ("small", 10), ("large", 48)];

    private readonly DollarStoreCamera _camera;
    private readonly UIContext _context;
    private readonly List<double> _cpuSamples = [];

    private Panel? _root;
    private GpuTimer? _timer;
    private int _variant;
    private int _glyphs;
    private int _frames;
    private bool _failed;
    private bool _done;
    private string _renderer = "";

    public TextBenchScene(Game game) : base(game)
    {
        _camera = new DollarStoreCamera(Vector3.Zero, game.ClientSize);
        _context = new UIContext { Camera = _camera };
    }

    /// <summary>
    ///     Builds the screen of text for the current variant. Not done in the constructor: every
    ///     boot builds every scene, and only a benchmark run should pay for this one's glyphs.
    /// </summary>
    private void Build()
    {
        var sizePx = Variants[_variant].SizePx;
        var metrics = _context.TextProvider.GlyphProvider.GetFontMetrics();
        var linePitch = sizePx * (float)(metrics.LineHeight / metrics.EmSize);

        // The same seed for every variant and every run, so the "before" and "after" runs
        // draw the same glyphs.
        var random = new Random(30);
        var line = new StringBuilder();

        _context.Clear();
        _timer?.Dispose();
        _timer = new GpuTimer();
        _cpuSamples.Clear();
        _glyphs = 0;
        _frames = 0;
        _root = new Panel(_context)
        {
            Width = LiteralOrComputable.Percent(100),
            Height = LiteralOrComputable.Percent(100)
        };

        for (float y = 0; y < _camera.Height; y += linePitch)
        {
            line.Clear();
            float x = 0;
            while (true)
            {
                var character = (char)random.Next(0x20, 0x7F);
                var advance = _context.TextProvider.GetGlyph(new ReadOnlySpan<char>(ref character)).Advance * sizePx;
                if (x + advance > _camera.Width) break;

                line.Append(character);
                x += advance;
            }

            _glyphs += line.Length;
            _root.AddChild(new Label(_context, line.ToString()) { FontSizePx = sizePx, Y = y });
        }

        _root.DrawTo(_context);
    }

    private void FinishVariant()
    {
        var gpu = _timer!.Samples;
        var p95 = GpuTimer.Percentile(gpu, 95);
        _failed |= p95 > GateMs;

        var size = Game.FramebufferSize;
        Logger.Information(
            "[TextBench] variant={Variant:l} size={Width}x{Height} glyphs={Glyphs} gpu_median={Median:0.000}ms " +
            "gpu_p95={P95:0.000}ms gpu_max={Max:0.000}ms cpu_median={Cpu:0.000}ms renderer=\"{Renderer:l}\"",
            Variants[_variant].Name, size.X, size.Y, _glyphs, GpuTimer.Percentile(gpu, 50), p95, gpu.Max(),
            GpuTimer.Percentile(_cpuSamples, 50), _renderer);

        if (++_variant < Variants.Length)
        {
            Build();
            return;
        }

        // nohup hides the exit code from viz.sh, so the verdict is logged as well.
        Logger.Information("[TextBench] verdict={Verdict:l} gate={Gate}ms", _failed ? "fail" : "pass", GateMs);
        Environment.ExitCode = _failed ? 1 : 0;
        _done = true;
    }

    public override void Initialize(InitArguments initArguments)
    {
        _renderer = initArguments.GLInfo.Renderer;
    }

    public override void Start()
    {
    }

    public override void Render(RenderArguments renderArgs)
    {
        // The loader's exit draws it underneath this scene for a while; only frames this
        // scene draws alone count.
        var alone = SceneManager.ActiveScenes is [var only] && only == this;
        if (_done || !alone || ++_frames <= WarmupFrames)
        {
            _context.Render();
            return;
        }

        _timer!.Begin();
        var start = Stopwatch.GetTimestamp();
        _context.Render();
        _cpuSamples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        _timer.End();

        if (_timer.Samples.Count >= MeasuredFrames) FinishVariant();
    }

    public override void TransitionedTo()
    {
        if (_root is null) Build();
    }

    public override void Update(UpdateArguments updateArgs)
    {
        if (_done)
        {
            Game.Close();
            return;
        }

        _camera.UpdateMatrix();
        _root?.Layout();
    }

    public override void Resize(int w, int h)
    {
        // ClientSize, not the framebuffer size passed in: the camera works in logical units.
        _camera.Viewport = Game.ClientSize;
        _camera.UpdateMatrix();
    }

    public override void Shutdown()
    {
        _timer?.Dispose();
    }

    public override void FileDrop(string[] locations)
    {
    }

    public override void Keyboard(KeyboardState state)
    {
    }

    public override void Mouse(MouseState mouseState, KeyboardState keyboardState)
    {
    }
}
