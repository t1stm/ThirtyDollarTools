using BMS2TDW;
using BMS2TDW.Converter;

// This tool won't be documented for obvious purposes.
// I don't want lazy people doing TDW covers.
// If you are willing to put in the effort, understand it yourself.

// Note to developers: The parser on this small project is far from perfect,
// so don't rely on it for your implementation.

// Put in the effort to reverse engineer your own parser, like how I did.

var read = await File.ReadAllTextAsync("/home/kris/Downloads/Calamity_Fortune_LeaF/_S.bms");
var bms_level = BMSParser.ParseFile(read);

var builder = new TDWexBuilder();
builder.ConvertBMSLevel(bms_level);

var converted = builder.Export();
File.WriteAllText("/home/kris/Downloads/Calamity_Fortune_LeaF/_export.tdw", converted);