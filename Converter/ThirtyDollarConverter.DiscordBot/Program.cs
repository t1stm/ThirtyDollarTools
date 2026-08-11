using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
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

Static.Logger = serilogLogger;

var samples_location = Environment.GetEnvironmentVariable("SAMPLES_LOCATION");
if (!string.IsNullOrEmpty(samples_location))
    Static.SampleHolder = new SampleHolder(serilogLogger)
    {
        SamplesLocation = samples_location
    };
else Static.SampleHolder = new SampleHolder(serilogLogger);

serilogLogger.Information("Starting Discord Bot...");

await Static.SampleHolder.LoadSampleList();
await Static.SampleHolder.DownloadSamples();
Static.SampleHolder.LoadSamplesIntoMemory();

var builder = DiscordClientBuilder.CreateDefault(token,
    TextCommandProcessor.RequiredIntents | SlashCommandProcessor.RequiredIntents);
builder.UseCommands((_, extension) =>
{
    extension.AddCommands<MessageCommands>();

    extension.CommandExecuted += (_, args) =>
    {
        serilogLogger.Information("Command {Command} invoked by {User} in {Channel}",
            args.Context.Command.FullName, args.Context.User, args.Context.Channel);
        return Task.CompletedTask;
    };

    extension.CommandErrored += (_, args) =>
    {
        serilogLogger.Error(args.Exception, "Command {Command} invoked by {User} in {Channel} threw an exception",
            args.Context.Command.FullName, args.Context.User, args.Context.Channel);
        return Task.CompletedTask;
    };
});

var client = builder.Build();

await client.ConnectAsync().ConfigureAwait(false);
serilogLogger.Information("Discord Bot connected.");
await Task.Delay(-1);