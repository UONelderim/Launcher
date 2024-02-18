using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Nelderim.Launcher
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "autoupdate")
            {
                var targetPath = args[1];
                AutoUpdate(targetPath);
                return;
            }
            using (var game = new NelderimLauncher(args)) game.Run();
        }

        public static void AutoUpdate(string targetPath)
        {
            try
            {
                var currentProcessPath = Environment.ProcessPath;
                if (currentProcessPath != null)
                {
                    if (targetPath != null)
                    {
                        File.Copy(currentProcessPath, targetPath, true);
                        var process = new Process();
                        process.StartInfo.FileName = Path.GetFullPath(targetPath);
                        process.StartInfo.Arguments = "Aktualizacja zakonczona pomyslnie";
                        process.Start();
                        File.Delete(currentProcessPath); //Will this even work?
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("Press any key to continue");
                Console.ReadKey();
            }
        }
    }
}