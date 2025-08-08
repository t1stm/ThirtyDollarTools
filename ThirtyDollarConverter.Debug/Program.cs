using ThirtyDollarConverter;
using ThirtyDollarConverter.Audio.Resamplers;
using ThirtyDollarConverter.CLI;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;

namespace ThirtyDollarApp;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        const string workingDirectory = "/home/kris/RiderProjects/ThirtyDollarTools/ThirtyDollarConverter.Debug";
        const string sequenceDirectory = $"{workingDirectory}/Included Sequences";

        var holder = new SampleHolder
        {
            DownloadLocation = $"{workingDirectory}/Sounds"
        };
        await holder.LoadSampleList();
        await holder.DownloadSamples();
        holder.LoadSamplesIntoMemory();
        Console.Clear();

        var list = Directory.GetFiles(sequenceDirectory).ToList();

        Directory.CreateDirectory("./Export");

        var output = new List<Readers.SequenceFile>();
        if (args.Length > 0)
            output.AddRange(await Readers.GetSequencesFromFileList(args));
        else
            output.AddRange(await Readers.GetSequencesFromFileList(list));

        const int statusbar_length = 64;
        foreach (var file in output)
        {
            if (file.Location.Contains("LICENSE")) continue;
            var sequence = Sequence.FromString(file.Data, file.ModificationDate);

            var encoder = new PcmEncoder(holder, new EncoderSettings
            {
                SampleRate = 48000,
                Channels = 2,
                CutFadeLengthMs = 10,
                Resampler = new HermiteResampler()
            }, Console.WriteLine, (current, total) =>
            {
                ClearLine();
                Console.Write(Progressbar.Generate(current, (long)total, statusbar_length));
            });

            var audioData = await encoder.GetSequenceAudio(sequence);
            encoder.WriteAsWavFile($"./Export/{file.Location.Split('/').Last()}.wav", audioData);
        }

        Console.WriteLine("Finished Executing. Press any key to exit.");
        // Console.ReadLine();
    }

    private static void ClearLine()
    {
        do
        {
            Console.Write("\b \b");
        } while (Console.CursorLeft > 0);
    }
}