using System.Diagnostics;
using System.Text.Json;
using Nelderim.Model;

var procName = Process.GetCurrentProcess().ProcessName;

var filePatterns = File.ReadAllLines($"{procName}.conf");
var filenames = filePatterns.SelectMany(p =>
    Directory.GetFiles(Directory.GetCurrentDirectory(), p).Select(Path.GetFileName).ToArray());
var patches = filenames.Select(filename => new Patch(filename)).ToArray();

using (var stream = File.OpenWrite($"{procName}.json"))
{
    JsonSerializer.Serialize(stream, patches);
}