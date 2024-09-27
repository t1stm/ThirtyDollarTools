using CommandLine;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Audio.Resamplers;
using ThirtyDollarConverter.CLI;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

var options = new Options();

Parser.Default.ParseArguments<Options>(args)
    .WithParsed(o => { options = o; });

var inputs = options.Input.ToArray();
var outputs = options.Output.ToArray();

if (inputs.Length < 1)
{
    Console.WriteLine("No input files were specified. Use -i and optionally -o to specify them.");
    return;
}

if (outputs.Length == 0) outputs = inputs.Select(s => $"{s}.wav").ToArray();

if (inputs.Length != outputs.Length)
{
    Console.WriteLine("The amount of input and output files specified isn't the same. Quitting.");
    return;
}

var sequences = await Readers.GetSequencesFromFileList(inputs);

var holder = new SampleHolder
{
    DownloadLocation = options.DownloadLocation ?? "./Sounds"
};

await holder.LoadSampleList();
await holder.DownloadSamples();
holder.LoadSamplesIntoMemory();

for (var i = 0; i < inputs.Length; i++)
{
    var input = sequences[i];
    var output = outputs[i];

    var sequence = Sequence.FromString(input.Data);

    var encoder = new PcmEncoder(holder, new EncoderSettings
    {
        SampleRate = (uint)(options.SampleRate ?? 48000),
        Channels = 2,
        CutFadeLengthMs = 10,
        Resampler = new HermiteResampler()
    }, LogAction, ProgressAction);

    var audioData = await encoder.GetSequenceAudio(sequence);
    encoder.WriteAsWavFile(output, audioData);

    continue;

    void ProgressAction(ulong current, ulong total)
    {
        var progress_bar = Progressbar.Generate(current, (long)total);
        var percentage = (float)current / total;

        Console.Clear();
        Console.WriteLine($"[Converting] \'{input.Location}\'");
        Console.WriteLine($"({percentage:0%}) {progress_bar}");
    }
}

return;

void LogAction(string s)
{
}