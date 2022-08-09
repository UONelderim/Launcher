using Avalonia;
using Avalonia.ReactiveUI;
using System;
using NelderimLauncher.Utility;

namespace NelderimLauncher
{
    class Program
    {
        private static FileLogger log = new(typeof(Program));
        
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch(Exception e)
            {
                log.Fatal(e.ToString());
            }
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}