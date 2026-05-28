using System.Text;
using ThirtyDollarParser;

var sequence = Sequence.FromString("#legacy|" + File.ReadAllText("/home/kris/tdw/death by glamour/death_by_glamour.tdw"));

var copy = sequence.Copy();
for (var index = 0; index < copy.Events.Length; index++)
{
    var ev = copy.Events[index];
    copy.Events[index] = ev;
}

var output = new StringBuilder();

foreach (var ev in copy.Events) output.Append(ev.Stringify() + "|");

File.WriteAllText("/home/kris/tdw/death by glamour/death_by_glamour.moai", output.ToString());