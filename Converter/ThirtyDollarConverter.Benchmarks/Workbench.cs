using Serilog.Core;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Encoder.PCM;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Parser;

namespace ThirtyDollarConverter.Benchmarks;

/// <summary>
///     Shared setup for every benchmark here: the real TDW sample set, the encoder configured
///     the way the editor configures it (see ThirtyDollarWorkflow), and the real covers the
///     scenarios edit.
/// </summary>
public static class Workbench
{
    private static SampleHolder? _holder;

    /// <summary>Where the covers live. Override with TDW_SEQUENCES.</summary>
    public static string SequenceRoot =>
        Environment.GetEnvironmentVariable("TDW_SEQUENCES")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tdw");

    public static string RepoRoot
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "ThirtyDollarTools.slnx")))
                dir = Path.GetDirectoryName(dir);
            return dir ?? throw new DirectoryNotFoundException("Couldn't find the repository root.");
        }
    }

    /// <summary>The project the incremental renderer's cut handling actually chokes on.</summary>
    public static string EditorProjectPath =>
        Environment.GetEnvironmentVariable("TDW_PROJECT")
        ?? Path.Combine(RepoRoot, "Visualizer", "ThirtyDollarVisualizer", "bin", "Release", "net10.0",
            "amalgamam.tdwproj");

    /// <summary>
    ///     Loads every TDW sample into memory once per process - a few seconds and a few
    ///     hundred MB, so it must never happen inside a measured region.
    /// </summary>
    public static SampleHolder Samples()
    {
        if (_holder != null) return _holder;

        var holder = new SampleHolder(Logger.None) { SamplesLocation = Path.Combine(RepoRoot, "Sounds") };
        holder.LoadSampleList().GetAwaiter().GetResult();
        holder.LoadSamplesIntoMemory();
        return _holder = holder;
    }

    /// <summary>The editor's own encoder settings: 48 kHz stereo, no normalization, 4 ms cut fade.</summary>
    public static PcmEncoder Encoder()
    {
        return new PcmEncoder(Samples(), new EncoderSettings
        {
            SampleRate = 48000,
            Channels = 2,
            EnableNormalization = false,
            CutFadeLengthMs = 4
        });
    }

    public static Sequence LoadSequence(string relativePath)
    {
        return Sequence.FromString(File.ReadAllText(Path.Combine(SequenceRoot, relativePath)));
    }

    public static ThirtyDollarProject LoadProject()
    {
        return ProjectFile.Load(File.ReadAllText(EditorProjectPath));
    }

    /// <summary>
    ///     Peak absolute difference between two renders, and whether they even agree on
    ///     length - what "are these two exports the same" comes down to.
    /// </summary>
    public static (int LengthA, int LengthB, float MaxDelta) Compare(AudioData<float> a, AudioData<float> b)
    {
        var max = 0f;
        for (var channel = 0; channel < Math.Min(a.ChannelCount, b.ChannelCount); channel++)
        {
            var left = a.GetChannel(channel);
            var right = b.GetChannel(channel);
            for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
                max = Math.Max(max, Math.Abs(left[i] - right[i]));
        }

        return (a.GetLength(), b.GetLength(), max);
    }
}