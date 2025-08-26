using System.Text;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;

var sequence = Sequence.FromString(File.ReadAllText("/home/kris/tdw/death by glamour/death_by_glamour.tdw"));

var copy = sequence.Copy();
for (var index = 0; index < copy.Events.Length; index++)
{
    var ev = copy.Events[index];
    copy.Events[index] = Process(ev);
}

var output = new StringBuilder();

foreach (var ev in copy.Events)
{
    output.Append(ev.Stringify() + "|");
}

File.WriteAllText("/home/kris/tdw/death by glamour/death_by_glamour.moai", output.ToString());

return;

BaseEvent Process(BaseEvent ev)
{
    if (ev is PannedEvent ne)
    {
        ne.Pan *= 100;
    }

    return ev;
}