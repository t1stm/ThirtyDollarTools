using DSharpPlus;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.SlashCommands;
using Serilog;
using ThirtyDollarConverter;
using ThirtyDollarConverter.DiscordBot;

var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
var serilogLogger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "{Level:u3}: {Message:lj}{NewLine}{Exception}")
    .MinimumLevel.Debug()
    .CreateLogger();

if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("No Discord token provided.");
    return;
}

var samples_location = Environment.GetEnvironmentVariable("SAMPLES_LOCATION");
if (!string.IsNullOrEmpty(samples_location))
    Static.SampleHolder = new SampleHolder(serilogLogger)
    {
        SamplesLocation = samples_location
    };
else Static.SampleHolder = new SampleHolder(serilogLogger);

Console.WriteLine("Starting Discord Bot...");
Console.WriteLine($"Using token: {token}");

await Static.SampleHolder.LoadSampleList();
await Static.SampleHolder.DownloadSamples();
Static.SampleHolder.LoadSamplesIntoMemory();

var builder = DiscordClientBuilder.CreateDefault(token,
    TextCommandProcessor.RequiredIntents | SlashCommandProcessor.RequiredIntents);
// yes its deprecated. i know man
builder.UseSlashCommands(setup => { setup.RegisterCommands<SlashCommands>(); });

var client = builder.Build();

await client.ConnectAsync().ConfigureAwait(false);
await Task.Delay(-1);