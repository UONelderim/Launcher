using System.Diagnostics;
using System.Text.Json;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nelderim.Model;
using Nelderim.Utility;
using Num = System.Numerics;

namespace Nelderim.Launcher
{
    public class NelderimLauncher : Game
    {
        private readonly HttpClient HttpClient = new();
        
        private GraphicsDeviceManager _gdm;
        private ImGuiRenderer _imGuiRenderer;

        private Texture2D? _xnaTexture;
        private IntPtr? _imGuiTexture;

        private bool _updateAvailable;

        public NelderimLauncher()
        {
            _gdm = new GraphicsDeviceManager(this);
            _gdm.PreferredBackBufferWidth = 600;
            _gdm.PreferredBackBufferHeight = 300;
            _gdm.PreferMultiSampling = true;
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            _downloadProgressHandler = new Progress<float>(f => _downloadProgressValue = f);
        }

        protected override void Initialize()
        {
            _imGuiRenderer = new ImGuiRenderer(_gdm.GraphicsDevice);
            _imGuiRenderer.RebuildFontAtlas();
            _updateAvailable = IsUpdateAvailable();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _xnaTexture = CreateTexture(GraphicsDevice,
                300,
                150,
                pixel =>
                {
                    var red = pixel % 300 / 2;
                    return new Color(red, 1, 1);
                });
            _imGuiTexture = _imGuiRenderer.BindTexture(_xnaTexture);
            base.LoadContent();
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _imGuiRenderer.BeforeDraw(gameTime, IsActive);
            DrawUI();
            _imGuiRenderer.AfterDraw();

            base.Draw(gameTime);
        }

        private bool _showTestWindow;
        private string _patchUrl = "https://www.nelderim.pl/patch";
        private string _logText = "";
        private bool _refreshing;
        private bool _downloading;
        private string _downloadFileName = "";
        private float _downloadProgressValue;

        private const int buttonHeight = 40;

        private void DrawUI()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            
            if (ImGui.Begin("MainWindow",
                    ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
            {
                if (_updateAvailable)
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
            ImGui.InputText("", ref _patchUrl, 256);
            ImGui.SameLine();
            ImGui.BeginDisabled(_refreshing);
            if (ImGui.Button("Odswiez", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, buttonHeight)))
            {
                new Task(Refresh).Start();
            }

            ImGui.EndDisabled();
            var size2 = ImGui.GetContentRegionAvail();
            size2.Y -= buttonHeight;
            ImGui.BeginChild("Log", size2, ImGuiChildFlags.Border);
            ImGui.TextUnformatted(_logText);
            ImGui.EndChild();
            var size3 = ImGui.GetContentRegionAvail();
            if (!_downloading)
            {
                if (ImGui.Button("Aktualizuj", size3))
                {
                    new Task(Patch).Start();
                }
            }
            else
            {
                var posY = ImGui.GetCursorPosY();
                ImGui.ProgressBar(_downloadProgressValue, size3, "");
                var text = $"{_downloadFileName} {_downloadProgressValue * 100f:F0}%";
                var textSize = ImGui.CalcTextSize(text);
                ImGui.SetCursorPosX(size3.X / 2 - textSize.X / 2);
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() - buttonHeight / 2 - textSize.Y / 2);
                ImGui.TextUnformatted(text);
            }

            if (_showTestWindow)
            {
                ImGui.SetNextWindowPos(new Num.Vector2(650, 20), ImGuiCond.FirstUseEver);
                ImGui.ShowDemoWindow(ref _showTestWindow);
            }
            if (ImGui.IsKeyPressed(ImGuiKey.F12))
            {
                _showTestWindow = !_showTestWindow;
            }
        }

        private void DrawUpdateUI()
        {
            ImGui.Text("Dostepna aktualizacja Nelderim Launcher");
            ImGui.Text("Obecna wersja: TODO");
            ImGui.Text("Nowa wersja: TODO");
            if (ImGui.Button("Aktualizuj"))
            {
                    var nelderimApp = AppName();
                    var appCopyName = $"_{nelderimApp}";
                    File.Copy(nelderimApp, appCopyName, true);
            
                    var process = new Process();
                    process.StartInfo.FileName = appCopyName;
                    process.StartInfo.Arguments = $"update {nelderimApp}";
                    process.Start();
                    Exit();
                LauncherUpdate([]);
            }
            if (ImGui.Button("Pomin"))
            {
                _updateAvailable = false;
            }
        }

        private List<PatchInfo> _patchInfos = new();

        private void Log(string text)
        {
            _logText += text + "\n";
        }

        private async void Refresh()
        {
            _refreshing = true;
            try
            {
                var response = await HttpClient.GetAsync($"{_patchUrl}/NelderimPatch.json");
                var responseBody = await response.Content.ReadAsStringAsync();
                var patches = JsonSerializer.Deserialize<List<Patch>>(responseBody);
                _patchInfos = patches.ConvertAll(p => new PatchInfo(p)).FindAll(p => p.ShouldUpdate);
                if (_patchInfos.Count > 0)
                {
                    Log("Dostepne sa nowe aktualizacje!");
                    _patchInfos.ForEach(p => Log(p.ToString()));
                }
                else
                {
                    Log("Wszystkie pliki aktualne");
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

        private async void Patch()
        {
            _downloading = true;
            try
            {
                foreach (var info in _patchInfos)
                {
                    Log($"Pobieram {info.Filename}");
                    _downloadFileName = info.Filename;
                    using var file = new FileStream(Path.GetFullPath(info.Filename), FileMode.OpenOrCreate);
                    await HttpClient.DownloadDataAsync($"{_patchUrl}/{info.Filename}", file, _downloadProgressHandler);
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
                _patchInfos = new();
                _downloadProgressValue = 0f;
                _downloadFileName = "";
                _downloading  = false;
            }
        }

        private void LauncherUpdate(string[] args)
        {
            // try
            // {
            //     string updateTarget = args[1];
            //     string tempUpdateTarget = $"{updateTarget}.temp";
            //     var patch = Utils.FetchPatch();
            //     var progress = new Progress<float>();
            //     progress.ProgressChanged += (s, f) =>
            //     {
            //         ProgressValue = f;
            //         UpdateMessage = $"{f:0}%";
            //     };
            //     using (var file = new FileStream(Path.GetFullPath(tempUpdateTarget), FileMode.OpenOrCreate))
            //     {
            //         await Http.DownloadDataAsync(Http.HttpClient,
            //             $"{Config.Get(Config.Key.PatchUrl)}/{patch.File}",
            //             file,
            //             progress);
            //     }
            //
            //     File.Move(tempUpdateTarget, updateTarget, true);
            //     var process = new Process();
            //     process.StartInfo.FileName = Path.GetFullPath(updateTarget);
            //     process.StartInfo.Arguments = "Aktualizacja zakończona pomyślnie";
            //     process.Start();
            //     OnUpdateFinished(this, new EventArgs());
            // }
            // catch (Exception e)
            // {
            //     await MessageBoxManager
            //         .GetMessageBoxStandardWindow("Błąd krytyczny", e.ToString(), ButtonEnum.Ok, Icon.Error).Show();
            // }
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


        private bool IsUpdateAvailable()
        {
            using (FileStream stream = File.OpenRead(AppName()))
            {
                return Crypto.Sha1Hash(stream) != FetchPatch().Sha1;
            }
        }

        private string AppName()
        {
            var app = Process.GetCurrentProcess().ProcessName;
            if (!app.EndsWith(".exe")) app += ".exe";
            return app;
        }


        private Patch FetchPatch()
        {
            var patchUrl = Config.Instance.PatchUrl;
            var patchJson = HttpClient.GetAsync($"{patchUrl}/Nelderim.json").Result.Content.ReadAsStream();
            var patches = JsonSerializer.Deserialize<List<Patch>>(patchJson);

            return patches.First();
        }
    }
}