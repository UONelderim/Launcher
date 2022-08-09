using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NelderimLauncher.ViewModels;
using NelderimLauncher.Views;

namespace NelderimLauncher
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.Args.Length == 2 && desktop.Args[0] == "update")
                {
                    var model = new UpdateWindowViewModel(desktop.Args);
                    desktop.MainWindow = new UpdateWindow
                    {
                        DataContext = model
                    };
                    model.OnUpdateFinished += (s, e) => desktop.MainWindow.Close();
                }
                else
                {
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = new MainWindowViewModel(desktop.Args)
                    };
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}