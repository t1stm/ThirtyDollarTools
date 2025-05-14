using System.Diagnostics;
using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using ThirtyDollarConverter.Objects;
using ThirtyDollarParser;
using Encoding = System.Text.Encoding;

namespace ThirtyDollarConverter.DiscordBot;

public class SlashCommands : ApplicationCommandModule
{
    [ContextMenu(DiscordApplicationCommandType.MessageContextMenu, "TDW to OGG Opus")]
    public async Task ConvertFileToOGG(ContextMenuContext ctx)
    {
        var file = ctx.TargetMessage.Attachments.Count > 0 ? ctx.TargetMessage.Attachments[0] : null;
        if (file is null || string.IsNullOrWhiteSpace(file.Url))
        {
            await ctx.CreateResponseAsync("```Message doesn't have any files attached.```");
            return;
        }
        
        await ctx.CreateResponseAsync("```Converting TDW to OGG.```");
        await ConvertTDWToAudio(ctx, file.Url);
    }
    
    [ContextMenu(DiscordApplicationCommandType.MessageContextMenu, "TDW to MP3")]
    public async Task ConvertFileToMP3(ContextMenuContext ctx)
    {
        var file = ctx.TargetMessage.Attachments.Count > 0 ? ctx.TargetMessage.Attachments[0] : null;
        if (file is null || string.IsNullOrWhiteSpace(file.Url))
        {
            await ctx.CreateResponseAsync("```Message doesn't have any files attached.```");
            return;
        }
        
        await ctx.CreateResponseAsync("```Converting TDW to MP3.```");
        await ConvertTDWToAudio(ctx, file.Url, true);
    }

    protected static async Task ConvertTDWToAudio(ContextMenuContext ctx, string url, bool mp3 = false)
    {
        var http_client = new HttpClient();
        byte[] request;
        try
        {
            request = await http_client.GetByteArrayAsync(url);
        }
        catch (Exception e)
        {
            await ctx.FollowUpAsync(
                new DiscordFollowupMessageBuilder()
                    .WithContent("```Unable to read message attachment. Please report this error to the developer.```\n" + $"```Error: {e}```"));
            return;
        }

        string sequence_text;
        try
        {
            Encoding utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            sequence_text = utf8.GetString(request, 0, request.Length);
        }
        catch (Exception)
        {
            await ctx.FollowUpAsync(
                new DiscordFollowupMessageBuilder()
                    .WithContent("```Unable to read message attachment. Likely a non-text file was uploaded.```"));
            return;
        }

        var sequence = Sequence.FromString(sequence_text);
        await EncoderTask(ctx, [sequence], mp3);
    }

    protected static async Task EncoderTask(ContextMenuContext ctx, IList<Sequence> sequences, bool mp3 = false)
    {
        var message = await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
            .WithContent("```Fully read attached file. Starting conversion.```"));
        
        var calculator = new PlacementCalculator(Static.EncoderSettings);
        var array = sequences as Sequence[] ?? sequences.ToArray();
        var placement = calculator.CalculateMany(array);
        var placement_array = placement.ToArray();

        var length_seconds = (float)placement_array[^1].Index / Static.EncoderSettings.SampleRate;
        if (length_seconds > 1200)
        {
            await message.ModifyAsync("```Passed Sequence is longer than 20 minutes. Stopping execution.```");
            return;
        }
        
        var timed_events = new TimedEvents
        {
            Sequences = array,
            Placement = placement_array,
            TimingSampleRate = (int)Static.EncoderSettings.SampleRate,
        };
        
        var last_update = Stopwatch.GetTimestamp();
        var message_timeout = TimeSpan.FromMilliseconds(1250);
        
        var encoder = new PcmEncoder(Static.SampleHolder, Static.EncoderSettings, null, 
            async void (current, max) =>
            {
                try
                {
                    if (Stopwatch.GetElapsedTime(last_update) < message_timeout)
                        return;
                    
                    last_update = Stopwatch.GetTimestamp();

                    var percentage = current / (double)max;
                    await message.ModifyAsync($"```Conversion percentage is: {percentage:P}```");
                }
                catch (Exception)
                {
                    // ignored
                }
            });
        
        var audio_data = await encoder.GetAudioFromTimedEvents(timed_events);

        var codec = mp3 ? "libmp3lame" : "libopus";
        const int bitrate = 192;
        var extension = mp3 ? "mp3" : "ogg";
        
        var ffmpeg_process = Process.Start(new ProcessStartInfo("ffmpeg")
        {
            Arguments = $"-v quiet -nostats -f wav -i - -c:a {codec} -b:a {bitrate}k -vn -dn -f {extension} pipe:1",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });

        if (ffmpeg_process == null)
        {
            await message.ModifyAsync("```Failed to start conversion process. Please report this error to the developer.```");
            return;
        }

        var thread = new Thread(() =>
        {
            encoder.WriteAsWavFile(ffmpeg_process.StandardInput.BaseStream, audio_data);
        });
        thread.Start();

        var memory_stream = new MemoryStream();
        await ffmpeg_process.StandardOutput.BaseStream.CopyToAsync(memory_stream);
        
        memory_stream.Seek(0, SeekOrigin.Begin);
        await message.ModifyAsync(new DiscordMessageBuilder()
            .WithContent("```Successfully converted file.```")
            .AddFile($"sequence.{extension}", memory_stream));
    }
}