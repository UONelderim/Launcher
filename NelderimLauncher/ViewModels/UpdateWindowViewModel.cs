using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Threading;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using NelderimLauncher.Utility;
using ReactiveUI;

namespace NelderimLauncher.Views;

public class UpdateWindowViewModel : ReactiveObject
{
    private float _progressValue = 0.45f;
    public float ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    private string _updateMessage;
    public string UpdateMessage
    {
        get => _updateMessage;
        set => this.RaiseAndSetIfChanged(ref _updateMessage, value);
    }

    public event EventHandler OnUpdateFinished;
    
    public UpdateWindowViewModel(string[] args)
    {
        Dispatcher.UIThread.Post(() => Update(args), DispatcherPriority.Background);
    }

    private async void Update(string[] args)
    {
        try
        {
            string updateTarget = args[1];
            var patch = Updater.FetchPatch();
            var progress = new Progress<float>();
            progress.ProgressChanged += (s, f) =>
            {
                ProgressValue = f;
                UpdateMessage = $"{f:0}%";
            };
            using (var file = new FileStream(Path.GetFullPath(updateTarget), FileMode.OpenOrCreate))
            {
                await Utils.DownloadDataAsync(Utils.HttpClient, $"{Config.Get(Config.Key.PatchUrl)}/{patch.File}",
                    file,
                    progress);
            }

            var process = new Process();
            process.StartInfo.FileName = Path.GetFullPath(updateTarget);
            process.StartInfo.Arguments = "Aktualizacja zakończona pomyślnie";
            process.Start();
            OnUpdateFinished(this, new EventArgs());
        }
        catch(Exception e)
        {
            MessageBoxManager.GetMessageBoxStandardWindow("Błąd krytyczny", e.ToString(), ButtonEnum.Ok, Icon.Error).Show();
        }
    }
}
