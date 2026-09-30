namespace Nelderim.Launcher
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                using var game = new NelderimLauncher(args);
                game.Run();
            }
            catch (Exception e)
            {
                File.WriteAllText($"Crash_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt", e.ToString());
            }
        }
    }
}
