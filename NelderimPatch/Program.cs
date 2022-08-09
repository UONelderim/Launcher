using System.Diagnostics;
using System.Text.Json;
using Nelderim.Model;

var BaseName = Process.GetCurrentProcess().ProcessName;

var filePatterns = File.ReadAllLines($"{BaseName}.conf");
string[] filenames = filePatterns
    .SelectMany(p => Directory.GetFiles(Directory.GetCurrentDirectory(), p).Select(Path.GetFileName).ToArray())
    .ToArray();
Patch[] patches = filenames.Select(filename => new Patch(filename)).ToArray();

using (FileStream stream = File.OpenWrite($"{BaseName}.json"))
{
    JsonSerializer.Serialize(stream, patches);
}