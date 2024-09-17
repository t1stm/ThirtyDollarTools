namespace ThirtyDollarConverter.CLI;

public static class Readers
{
    public static async Task<List<SequenceFile>> GetSequencesFromFileList(IEnumerable<string> array)
    {
        var output = new List<SequenceFile>();
        foreach (var location in array)
            try
            {
                if (!File.Exists(location) || Directory.Exists(location))
                {
                    Console.WriteLine($"File: \"{location}\" doesn't exist.");
                    continue;
                }

                var data = await File.ReadAllTextAsync(location);
                output.Add(new SequenceFile
                {
                    Location = location,
                    Data = data
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to open file in args: \"{location}\" - Exception: {e}");
                throw;
            }

        return output;
    }
    
    public struct SequenceFile
    {
        public string Location;
        public string Data;
    }
}