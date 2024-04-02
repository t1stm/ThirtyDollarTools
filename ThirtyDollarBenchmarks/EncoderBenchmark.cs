using System.Reflection;
using BenchmarkDotNet.Attributes;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarConverter.Resamplers;
using ThirtyDollarParser;

namespace ThirtyDollarBenchmarks;

public class EncoderBenchmark
{
    private PcmEncoder _encoder = null!;
    private SampleHolder _holder = null!;
    private Sequence _sequence;

    [Params("ThirtyDollarBenchmarks.Sequences.particle-accelerator.🗿",
        "ThirtyDollarBenchmarks.Sequences.sounds.🗿")]
    public string _sequence_location;

    private static Stream GetResource(string location)
    {
        if (File.Exists(location)) return File.Open(location, FileMode.Open);

        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(location);
        if (stream == null) throw new FileNotFoundException($"Unable to find resource: \'{location}\'");

        return stream;
    }

    [GlobalSetup]
    public void Setup()
    {
        _holder = new SampleHolder
        {
            DownloadLocation =
                "/home/kris/RiderProjects/ThirtyDollarWebsiteConverter/ThirtyDollarGUI/bin/Debug/net7.0/Sounds/"
        };
        _encoder = new PcmEncoder(_holder, new EncoderSettings
        {
            SampleRate = 48000,
            Channels = 2,
            CutFadeLengthMs = 250,
            CombineDelayMs = 0,
            Resampler = new LinearResampler(),
            AddVisualEvents = false
        });

        Task.Run(async () =>
        {
            _holder.PrepareDirectory();
            await _holder.LoadSampleList();
            await _holder.DownloadSamples();
        }).Wait();

        _holder.LoadSamplesIntoMemory();
        var stream = GetResource(_sequence_location);
        var reader = new StreamReader(stream);

        var sequence_text = reader.ReadToEnd();
        _sequence = Sequence.FromString(sequence_text);
    }

    [Benchmark]
    public async Task Standard_PCM_Encoder_OneThread()
    {
        await _encoder.GetSequenceAudio(_sequence);
    }

    [Benchmark]
    public async Task Standard_PCM_Encoder_NoSave()
    {
        await _encoder.GetSequenceAudio(_sequence);
    }

    [Benchmark]
    public async Task Standard_PCM_Encoder_Save()
    {
        var sampled = await _encoder.GetSequenceAudio(_sequence);
        var tmp = Path.GetTempFileName();
        _encoder.WriteAsWavFile(tmp, sampled);

        Console.WriteLine(tmp);
    }
}