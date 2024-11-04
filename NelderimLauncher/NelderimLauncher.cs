using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nelderim.Utility;
using Num = System.Numerics;

namespace Nelderim.Launcher
{
    public class NelderimLauncher : Game
    {
        private const string Version = "1.1.0"; //Pass me from outside
        private readonly HttpClient _HttpClient = new();

        private GraphicsDeviceManager _gdm;
        private ImGuiRenderer _ImGuiRenderer;

        private Texture2D? _XnaTexture;
        private IntPtr? _ImGuiTexture;

        private bool _UpdateAvailable;

        private Manifest _LocalManifest;
        private Manifest _ServerManifest;
        private List<FileInfo> _ChangedFiles;

        public NelderimLauncher(string[] args)
        {
            _gdm = new GraphicsDeviceManager(this);
            _gdm.PreferredBackBufferWidth = 600;
            _gdm.PreferredBackBufferHeight = 300;
            _gdm.PreferMultiSampling = true;
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            _downloadProgressHandler = new Progress<float>(f => _downloadProgressValue = f);
            
            Window.Title = $"Nelderim Launcher {Version}";
            String.Join(' ', args);
        }

        protected override void Initialize()
        {
            _ImGuiRenderer = new ImGuiRenderer(_gdm.GraphicsDevice);
            _ImGuiRenderer.RebuildFontAtlas();
            // _autoUpdateInfos = FetchAutoUpdateInfo();
            // _updateAvailable = IsUpdateAvailable();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _XnaTexture = CreateTexture(GraphicsDevice,
                300,
                150,
                pixel =>
                {
                    var red = pixel % 300 / 2;
                    return new Color(red, 1, 1);
                });
            _ImGuiTexture = _ImGuiRenderer.BindTexture(_XnaTexture);
            base.LoadContent();
        }
        
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _ImGuiRenderer.BeforeDraw(gameTime, IsActive);
            DrawUI();
            _ImGuiRenderer.AfterDraw();

            base.Draw(gameTime);
        }
        
        //We can later use this for displaying graphics
        private Texture2D CreateTexture(GraphicsDevice device, int width, int height, Func<int, Color> paint)
        {
            var texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];
            for (var pixel = 0; pixel < data.Length; pixel++)
            {
                data[pixel] = paint(pixel);
            }
            texture.SetData(data);
            return texture;
        }

        private bool _showDebugWindow;
        private string _logText = "";
        private bool _refreshing;
        private bool _downloading;
        private string _downloadFileName = "";
        private float _downloadProgressValue;
        private string PatchUrl => Config.Instance.PatchUrl;

        private const int ButtonHeight = 40;

        private void DrawUI()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);

            if (ImGui.Begin("MainWindow",
                    ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
            {
                if (_UpdateAvailable)
                {
                    DrawUpdateUI();
                }
                else
                {
                    DrawMainUI();
                }
            }

            ImGui.End();
        }

        private void DrawMainUI()
        {
            if (ImGui.InputText("", ref Config.Instance.PatchUrl, 256))
            {
                Config.Save();
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(_refreshing);
            if (ImGui.Button("Odswiez", new Num.Vector2(ImGui.GetContentRegionAvail().X, ButtonHeight)))
            {
                new Task(CheckUpdate).Start();
            }

            ImGui.EndDisabled();
            var size2 = ImGui.GetContentRegionAvail();
            size2.Y -= ButtonHeight;
            ImGui.BeginChild("Log", size2, ImGuiChildFlags.Border);
            ImGui.TextUnformatted(_logText);
            ImGui.EndChild();
            var size3 = ImGui.GetContentRegionAvail();
            if (!_downloading)
            {
                if (ImGui.Button("Aktualizuj", size3))
                {
                    new Task(Update).Start();
                }
            }
            else
            {
                ImGui.ProgressBar(_downloadProgressValue, size3, "");
                var text = $"{_downloadFileName} {_downloadProgressValue * 100f:F0}%";
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPosX(size3.X / 2 - textSize.X / 2);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - ButtonHeight / 2 - textSize.Y / 2);
                ImGui.TextUnformatted(text);
            }

            if (_showDebugWindow)
            {
                ImGui.SetNextWindowPos(new Num.Vector2(650, 20), ImGuiCond.FirstUseEver);
                ImGui.ShowDemoWindow(ref _showDebugWindow);
            }

            if (ImGui.IsKeyPressed(ImGuiKey.F12))
            {
                _showDebugWindow = !_showDebugWindow;
            }
        }

        private void DrawUpdateUI()
        {
            ImGui.Text("Dostepna aktualizacja Nelderim Launcher");
            ImGui.Text($"Obecna wersja: {Version}");
            ImGui.Text("Nowa wersja: TODO");
            if (ImGui.Button("Aktualizuj"))
            {
                AutoUpdate();
            }
            if (ImGui.Button("Pomin"))
            {
                _UpdateAvailable = false;
            }
        }
        private async void CheckUpdate()
        {
            _refreshing = true;
            try
            {
                var response = await _HttpClient.GetAsync($"{PatchUrl}/Nelderim.manifest.json");
                var responseBody = await response.Content.ReadAsStringAsync();
                _ServerManifest = JsonSerializer.Deserialize<Manifest>(responseBody);
                _ChangedFiles = _LocalManifest.ChangesBetween(_ServerManifest);
                if(_ChangedFiles.Count > 0)
                {
                    Log("Dostepne sa nowe aktualizacje!");
                    foreach (var fileInfo in _ChangedFiles)
                    {
                        if (fileInfo.Version == -1)
                        {
                            Log("Usunieto " + fileInfo.File);
                        }
                        else if (fileInfo.Version == 1)
                        {
                            Log("Dodano " + fileInfo.File);
                        }
                        else
                        {
                            Log("Zaktualizowano " + fileInfo.File);
                        }
                    }
                }
                else
                {
                    Log("Wszystkie pliki sa aktualne");
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
            finally
            {
                _refreshing = false;
            }
        }


        private Progress<float> _downloadProgressHandler;

        private async void Update()
        {
            _downloading = true;
            try
            {
                foreach (var fileInfo in _ChangedFiles)
                {
                    if (fileInfo.Version != -1)
                    {
                        Log($"Pobieram {fileInfo.File}");
                        _downloadFileName = fileInfo.File;
                        File.Delete(fileInfo.File);
                        using var file = new FileStream(Path.GetFullPath(fileInfo.File), FileMode.OpenOrCreate);
                        await _HttpClient.DownloadDataAsync($"{PatchUrl}/Nelderim/{fileInfo.File}", //TODO: How to pass 'Nelderim' here?
                            file,
                            _downloadProgressHandler);
                    }
                    else
                    {
                        //TODO: Usun plik
                    }
                }
                Log("Wszystkie pliki sa aktualne");
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
            finally
            {
                _downloading = false;
                _ChangedFiles = [];
                _downloadProgressValue = 0f;
                _downloadFileName = "";
                _downloading = false;
            }
        }
        
        private bool IsUpdateAvailable()
        {
            // if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            // return Utils.Sha1Hash(Environment.ProcessPath ?? "") != _ServerManifest.First().Sha1;
            return false;
        }

        private Manifest FetchAutoUpdateInfo()
        {
            // var patchUrl = Config.Instance.PatchUrl;
            // var patchJson = _HttpClient.GetAsync($"{patchUrl}/NelderimLauncher.manifest.json").Result.Content.ReadAsStream();
            // return JsonSerializer.Deserialize<List<Patch>>(patchJson);
            return null;
        }
        
        private async void AutoUpdate()
        {
            // var currentPath = Environment.ProcessPath;
            // var dir = Path.GetDirectoryName(currentPath);
            // var filename = Path.GetFileName(currentPath);
            // var newPath = $"{dir}/_{filename}";
            // if(File.Exists(newPath))
            // {
            //     File.Delete(newPath);
            // }
            // await using (var file = new FileStream(newPath, FileMode.OpenOrCreate))
            // {
            //     await _HttpClient.DownloadDataAsync($"{PatchUrl}/{_ServerManifest.First().File}",
            //         file,
            //         _downloadProgressHandler);
            // }
            //
            // var process = new Process();
            // process.StartInfo.FileName = Path.GetFullPath(newPath);
            // process.StartInfo.Arguments = $"autoupdate {filename}";
            // process.Start();
            // Exit();
        }

        
        private void Log(string text)
        {
            _logText += text + "\n";
        }
    }
}