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

    [Params("ThirtyDollarBenchmarks.Compositions.particle-accelerator.🗿",
        "ThirtyDollarBenchmarks.Compositions.sounds.🗿")]
    public string _composition_location;
    private Composition _composition;

    private static Stream GetResource(string location)
    {
        if (File.Exists(location))
        {
            return File.Open(location, FileMode.Open);
        }
        
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(location);
        if (stream == null)
        {
            throw new FileNotFoundException($"Unable to find resource: \'{location}\'");
        }
        
        return stream;
    }
    
    [GlobalSetup]
    public void Setup()
    {
        _holder = new SampleHolder
        {
            DownloadLocation = "/home/kris/RiderProjects/ThirtyDollarWebsiteConverter/ThirtyDollarGUI/bin/Debug/net7.0/Sounds/"
        };
        _encoder = new PcmEncoder(_holder, new EncoderSettings
        {
            SampleRate = 48000,
            Channels = 2,
            CutDelayMs = 250,
            CombineDelayMs = 0,
            Resampler = new LinearResampler(),
            AddVisualEvents = false
        });
        
        Task.Run(async () =>
        {
            _holder.DownloadedAllFiles();
            await _holder.LoadSampleList();
            await _holder.DownloadSamples();
        }).Wait();
        
        _holder.LoadSamplesIntoMemory();
        var stream = GetResource(_composition_location);
        var reader = new StreamReader(stream);

        var composition_text= reader.ReadToEnd();
        _composition = Composition.FromString(composition_text);
    }
    
    [Benchmark]
    public void Standard_PCM_Encoder_OneThread()
    {
        _encoder.SampleComposition(_composition, 1);
    }

    [Benchmark]
    public void Standard_PCM_Encoder_NoSave()
    {
        _encoder.SampleComposition(_composition);
    }
    
    [Benchmark]
    public void Standard_PCM_Encoder_Save()
    {
        var sampled = _encoder.SampleComposition(_composition);
        var tmp = Path.GetTempFileName();
        _encoder.WriteAsWavFile(tmp, sampled);
        
        Console.WriteLine(tmp);
    }
}