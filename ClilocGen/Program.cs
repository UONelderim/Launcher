using System.Text;

var sourceCsv = args.Length > 0 ? args[0] : "Cliloc.csv";
var targetEnu = "Cliloc.enu";
var writtenCount = 0;
using (var writer = new BinaryWriter(new FileStream(targetEnu, FileMode.Create, FileAccess.Write, FileShare.None)))
{
    var lineNumber = 0;
    using var reader = new StreamReader(new FileStream(sourceCsv, FileMode.Open, FileAccess.Read, FileShare.Read));
    writer.Write((int)0); //header1
    writer.Write((short)0); //header2
    while (reader.ReadLine()?.Trim() is { } line)
    {
        lineNumber++;
        if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("Number;")) continue; 
        
        try
        {
            var split = line.Split(';');
            if (split.Length != 2)
            {
                Console.WriteLine($"Invalid entry at line {lineNumber}");
                continue;
            }

            var id = int.Parse(split[0].Trim());
            var text = split[1].Trim();

            writer.Write(id);
            writer.Write((byte)0); //Flag, 0=original, 1=custom, 2=modified
            var utf8String = Encoding.UTF8.GetBytes(text);
            writer.Write(utf8String.Length);
            writer.Write(utf8String);
            writtenCount++;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error at line {lineNumber}: " + e);
        }
    }
}
Console.WriteLine($"Written {writtenCount} entries");
Console.WriteLine("Press any key...");
Console.ReadKey();
