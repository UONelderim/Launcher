using System.Diagnostics;

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

            using var game = new NelderimLauncher(args);
            try
            {
                game.Run();
            }
            catch (Exception e)
            {
                File.WriteAllText($"Crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt", e.ToString());
            }
        }

        private static void AutoUpdate(string targetPath)
        {
            try
            {
                var currentProcessPath = Environment.ProcessPath;
                if (currentProcessPath != null)
                {
                    if (targetPath != null)
                    {
                        if(File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                        }
                        File.Copy(currentProcessPath, targetPath, true);
                        var process = new Process();
                        process.StartInfo.FileName = Path.GetFullPath(targetPath);
                        process.StartInfo.Arguments = "Aktualizacja zakonczona pomyslnie";
                        process.Start();
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