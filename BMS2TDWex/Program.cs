using BMS2TDW;
using ThirtyDollarConverter.Editor;

// This tool won't be documented for obvious purposes.
// I don't want lazy people doing TDW covers.
// If you are willing to put in the effort, understand it yourself.

if (args.Length < 1)
{
    Console.WriteLine("usage: BMS2TDWex <chart.bms> [output.tdw] [project.tdwproj]");
    return 1;
}

var input = args[0];
var output = args.Length > 1 ? args[1] : Path.ChangeExtension(input, ".tdw");

var chart = BmsParser.Parse(await File.ReadAllTextAsync(input));
var project = BmsToProject.Convert(chart);

// Optional editor-project dump: GUI stress fixtures without shipping BMS code there.
if (args.Length > 2)
    await File.WriteAllTextAsync(args[2], ProjectFile.Save(project));

// Dividers section the text: one before each tempo change, one every two bars
// within a tempo section (BMS spans without tempo events are whole measures).
// MigrateToStop = null: gaps render as "_pause"s, the TDW-native idiom.
var sequence = project.ToSequence(new SequenceStyle
{
    DividerEveryBars = 2,
    DividerOnSpeedChanges = true,
    MigrateToStop = null
});

await File.WriteAllTextAsync(output, SequenceText.Serialize(sequence));

var events = sequence.Events;
var sounds = events.Count(e => !(e.SoundEvent?.StartsWith('!') ?? true) && e.SoundEvent != "_pause");
var speeds = events.Count(e => e.SoundEvent == "!speed");
var minutes = 0d;
var speed = 1d;
for (var i = 0; i < events.Length; i++)
{
    var name = events[i].SoundEvent ?? "";
    switch (name)
    {
        case "!speed":
            speed = events[i].Value;
            break;
        case "!stop":
            minutes += events[i].Value / speed;
            break;
        default:
        {
            if (!name.StartsWith('!') && (i + 1 >= events.Length || events[i + 1].SoundEvent != "!combine"))
                minutes += 1 / speed;
            break;
        }
    }
}

var duration = TimeSpan.FromMinutes(minutes);
Console.WriteLine($"{chart.Title} - {chart.Artist} @ {chart.Bpm} BPM");
Console.WriteLine($"{project.Tracks.Count} tracks, {sounds} sounds, {events.Length} events, " +
                  $@"{speeds} !speed changes, {duration:mm\:ss\.fff}");
Console.WriteLine($"-> {output}");
return 0;