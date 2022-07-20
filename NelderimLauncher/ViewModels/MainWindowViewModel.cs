using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using NelderimLauncher.Models;
using ReactiveUI;

namespace NelderimLauncher.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private string _patchUrl;
        private string _logText = "";
        private int _logCaretIndex;
        private bool _patchButtonActive;
        private string _patchButtonContent = "Aktualizuj";
        private List<PatchInfo> _patchInfos = new();
        private float _progressValue;

        private static readonly HttpClient HttpClient = new();

        public MainWindowViewModel()
        {
            _patchUrl = Config.Get(Config.Key.PatchUrl, "https://nelderim.pl/patch");
        }

        public string LogText
        {
            get => _logText;
            set => this.RaiseAndSetIfChanged(ref _logText, value);
        }

        public string PatchUrl
        {
            get => _patchUrl;
            set => this.RaiseAndSetIfChanged(ref _patchUrl, value);
        }

        public bool PatchButtonActive
        {
            get => _patchButtonActive;
            set => this.RaiseAndSetIfChanged(ref _patchButtonActive, value);
        }
        
        public string PatchButtonContent
        {
            get => _patchButtonContent;
            set => this.RaiseAndSetIfChanged(ref _patchButtonContent, value);
        }

        public float ProgressValue
        {
            get => _progressValue;
            set => this.RaiseAndSetIfChanged(ref _progressValue, value);
        }

        public int LogCaretIndex
        {
            get => _logCaretIndex;
            set => this.RaiseAndSetIfChanged(ref _logCaretIndex, value);
        }

        void Refresh()
        {
            if(Config.Get(Config.Key.PatchUrl) != PatchUrl)
                Config.Set(Config.Key.PatchUrl, PatchUrl);
            PatchButtonActive = true;
            Dispatcher.UIThread.Post(() => RefreshTask(), DispatcherPriority.Background);
        }
        
        private async Task RefreshTask()
        {
            try
            {
                var async = await HttpClient.GetAsync($"{_patchUrl}/NelderimPatch.json");
                string responseBody = await async.Content.ReadAsStringAsync();
                List<Patch>? patches = JsonSerializer.Deserialize<List<Patch>>(responseBody);
                _patchInfos = patches.ConvertAll(patch => new PatchInfo(patch)).FindAll(info => info.ShouldUpdate);
                if (_patchInfos.Count != 0)
                {
                    LogToConsole("Dostępne są nowe aktualizacje!");
                    _patchInfos.ForEach(info => LogToConsole(info.ToString()));
                }
                else
                {
                    LogToConsole("Wszystkie pliki są aktualne");
                }
                
            }
            catch (Exception e)
            {
                LogToConsole(e.ToString());
            }
        }
        
        
        void Patch()
        {
            Dispatcher.UIThread.Post(() => PatchTask(), DispatcherPriority.Background);
        }

        private async Task PatchTask()
        {
            try
            {
                foreach (var info in _patchInfos)
                {
                    LogToConsole($"Pobieram {info.Filename}");
                    var progress = new Progress<float>();

                    void OnProgressOnProgressChanged(object sender, float f)
                    {
                        ProgressValue = f;
                        PatchButtonContent = $"{info.Filename} {f:0}%";
                    }

                    progress.ProgressChanged += OnProgressOnProgressChanged;
                    using (var file = new FileStream(Path.GetFullPath(info.Filename), FileMode.OpenOrCreate))
                    {
                        await HttpClient.DownloadDataAsync($"{_patchUrl}/{info.Filename}", file, progress);
                    }

                    progress.ProgressChanged -= OnProgressOnProgressChanged;
                }
                LogToConsole("Wszystkie pliki są aktualne");
               
            }
            catch (Exception e)
            {
                LogToConsole(e.ToString());
            }
            finally
            {
                PatchButtonActive = false;
                _patchInfos = new();
                ProgressValue = 0.0f;
                PatchButtonContent = "Aktualizuj";
            }
        }
        
        void LogToConsole(String text)
        {
            LogText += $"{text}\n";
            LogCaretIndex = LogText.Length;
        }
    }
}