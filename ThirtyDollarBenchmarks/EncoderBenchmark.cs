using System.Reflection;
using BenchmarkDotNet.Attributes;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

namespace ThirtyDollarBenchmarks;

public class EncoderBenchmark
{
    private PcmEncoder _encoder = null!;
    private SampleHolder? _holder;
    private Sequence _sequence = null!;

    [Params(48000, 96000, 192000, 384000)] public uint SampleRate = 48000;

    [Params("ThirtyDollarBenchmarks.Sequences.another.🗿")]
    public string SequenceLocation = null!;

    [Params(1, 4, 8, 16, 32, 64, 128)] public int ThreadCount = 1;

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
        if (_holder == null)
        {
            _holder = new SampleHolder
            {
                DownloadLocation =
                    "/home/kris/RiderProjects/ThirtyDollarWebsiteConverter/ThirtyDollarConverter.GUI/bin/Debug/net7.0/Sounds/"
            };

            Task.Run(async () =>
            {
                _holder.PrepareDirectory();
                await _holder.LoadSampleList();
                await _holder.DownloadSamples();
            }).Wait();

            _holder.LoadSamplesIntoMemory();
        }

        _encoder = new PcmEncoder(_holder, new EncoderSettings
        {
            SampleRate = SampleRate,
            Channels = 2,
            CutFadeLengthMs = 25,
            CombineDelayMs = 0,
            MultithreadingSlices = ThreadCount,
            AddVisualEvents = false
        });

        var stream = GetResource(SequenceLocation);
        var reader = new StreamReader(stream);

        var sequence_text = reader.ReadToEnd();
        _sequence = Sequence.FromString(sequence_text);
    }

    [Benchmark]
    public async Task Standard_PCM_Encoder_NoSave()
    {
        await _encoder.GetSequenceAudio(_sequence);
    }
}