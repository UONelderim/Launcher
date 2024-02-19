using System.Diagnostics;
using System.Reflection;
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

        private List<Patch> _autoUpdateInfos = new();
        private List<PatchInfo> _patchInfos = new();

        public NelderimLauncher(string[] args)
        {
            _gdm = new GraphicsDeviceManager(this);
            _gdm.PreferredBackBufferWidth = 600;
            _gdm.PreferredBackBufferHeight = 300;
            _gdm.PreferMultiSampling = true;
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            _downloadProgressHandler = new Progress<float>(f => _downloadProgressValue = f);
            var version = Assembly.GetEntryAssembly().GetName().Version;
            Window.Title = $"Nelderim Launcher {version.Major}.{version.Minor}.{version.Revision}";
        }

        protected override void Initialize()
        {
            _imGuiRenderer = new ImGuiRenderer(_gdm.GraphicsDevice);
            _imGuiRenderer.RebuildFontAtlas();
            _autoUpdateInfos = FetchAutoUpdateInfo();
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
            if (ImGui.InputText("", ref Config.Instance.PatchUrl, 256))
            {
                Config.Save();
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(_refreshing);
            if (ImGui.Button("Odswiez", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, ButtonHeight)))
            {
                new Task(Refresh).Start();
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
            ImGui.Text("Obecna wersja: TODO");
            ImGui.Text("Nowa wersja: TODO");
            if (ImGui.Button("Aktualizuj"))
            {
               new Task(AutoUpdate).Start();
            }
            if (ImGui.Button("Pomin"))
            {
                _updateAvailable = false;
            }
        }
        private async void Refresh()
        {
            _refreshing = true;
            try
            {
                var response = await HttpClient.GetAsync($"{PatchUrl}/NelderimPatch.json");
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

        private async void Patch()
        {
            _downloading = true;
            try
            {
                foreach (var info in _patchInfos)
                {
                    Log($"Pobieram {info.Filename}");
                    _downloadFileName = info.Filename;
                    File.Delete(info.Filename);
                    using var file = new FileStream(Path.GetFullPath(info.Filename), FileMode.OpenOrCreate);
                    await HttpClient.DownloadDataAsync($"{PatchUrl}/{info.Filename}", file, _downloadProgressHandler);
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
                _downloading = false;
            }
        }
        
        private async void AutoUpdate()
        {
            var currentPath = Environment.ProcessPath;
            var newPath = $"{currentPath}.autoupdate";
            using var file = new FileStream(newPath, FileMode.OpenOrCreate);
            await HttpClient.DownloadDataAsync($"{PatchUrl}/{_autoUpdateInfos.First().Filename}", file, _downloadProgressHandler);
            var process = new Process();
            process.StartInfo.FileName = newPath;
            process.StartInfo.Arguments = $"autoupdate {currentPath}";
            process.Start();
            Exit();
        }

        private bool IsUpdateAvailable()
        {
            using FileStream stream = File.OpenRead(Environment.ProcessPath ?? "");
            return Crypto.Sha1Hash(stream) != _autoUpdateInfos.First().Sha1;
        }

        private List<Patch> FetchAutoUpdateInfo()
        {
            var patchUrl = Config.Instance.PatchUrl;
            var patchJson = HttpClient.GetAsync($"{patchUrl}/Nelderim.json").Result.Content.ReadAsStream();
            return JsonSerializer.Deserialize<List<Patch>>(patchJson);
        }
        
        private void Log(string text)
        {
            _logText += text + "\n";
        }
    }
}