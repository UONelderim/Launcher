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
        private const string MANIFEST_FILE_NAME = "Nelderim.manifest.json";
        private readonly HttpClient _HttpClient = new();

        private GraphicsDeviceManager _gdm;
        private ImGuiRenderer _ImGuiRenderer;

        private IntPtr? _BackgroundTexture;
        private IntPtr? _LaunchTexture;

        private bool _UpdateAvailable;

        private Manifest _LocalManifest;
        private Manifest _ServerManifest;
        private List<FileInfo> _ChangedFiles;

        public NelderimLauncher(string[] args)
        {
            _gdm = new GraphicsDeviceManager(this);
            _gdm.PreferredBackBufferWidth = 600;
            _gdm.PreferredBackBufferHeight = 400;
            _gdm.PreferMultiSampling = true;
            IsMouseVisible = true;
            Window.AllowUserResizing = false;
            _downloadProgressHandler = new Progress<float>(f => _DownloadProgressValue = f);
            
            Window.Title = $"Nelderim Launcher {Version}";
            String.Join(' ', args);
        }

        protected override void Initialize()
        {
            _ImGuiRenderer = new ImGuiRenderer(_gdm.GraphicsDevice);
            _ImGuiRenderer.RebuildFontAtlas();
            ImGui.StyleColorsDark();
            if(File.Exists(MANIFEST_FILE_NAME))
            {
                var jsonText = File.ReadAllText(MANIFEST_FILE_NAME);
                _LocalManifest = JsonSerializer.Deserialize<Manifest>(jsonText);
            }
            else
            {
                _LocalManifest = new Manifest(0, []);
            }
            //TODO: Bring me back
            // _autoUpdateInfos = FetchAutoUpdateInfo();
            // _updateAvailable = IsUpdateAvailable();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _BackgroundTexture = _ImGuiRenderer.BindTexture(Texture2D.FromStream(_gdm.GraphicsDevice, GetType().Assembly.GetManifestResourceStream("NelderimLauncher.background.png")));
            _LaunchTexture = _ImGuiRenderer.BindTexture(Texture2D.FromStream(_gdm.GraphicsDevice, GetType().Assembly.GetManifestResourceStream("NelderimLauncher.launch.png")));
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
        
        private bool _ShowDebugWindow;

        private bool _ShowLogs;
        private bool _ShowOptions;
        
        private string _LogText = "";
        private string _LastLogMessage = "";
        private bool _Refreshing;
        private bool _Downloading;
        private string _DownloadFileName = "";
        private float _DownloadProgressValue;
        private string PatchUrl => Config.Instance.PatchUrl;

        private void DrawUI()
        {
            var padding = _ShowOptions || _ShowLogs ? new Num.Vector2(8, 8) : new Num.Vector2(0, 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, padding);
            DrawMainMenu();
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
                else if (_ShowLogs)
                {
                    DrawLogsUI();
                }
                else if (_ShowOptions)
                {
                    DrawOptionsUI();
                }
                else
                {
                    DrawMainUI();
                }
                ImGui.End();
            }
            if (_ShowDebugWindow)
            {
                ImGui.SetNextWindowPos(new Num.Vector2(650, 20), ImGuiCond.FirstUseEver);
                ImGui.ShowDemoWindow(ref _ShowDebugWindow);
            }
            if (ImGui.IsKeyPressed(ImGuiKey.F12))
            {
                _ShowDebugWindow = !_ShowDebugWindow;
            }
            ImGui.PopStyleVar();
        }

        private void DrawMainMenu()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.MenuItem("Logi", "", _ShowLogs))
                {
                    _ShowLogs = !_ShowLogs;
                    _ShowOptions = false;
                }
                if (ImGui.MenuItem("Opcje", "", _ShowOptions))
                {
                    _ShowLogs = false;
                    _ShowOptions = !_ShowOptions;
                }
                ImGui.EndMainMenuBar();
            }
        }

        Num.Vector4 buttonTintColor = new Num.Vector4(1f, 1f, 1f, 1);
        
        private void DrawMainUI()
        {
            var availSize = ImGui.GetContentRegionAvail();
            var cursorPos = ImGui.GetCursorPos();
            ImGui.Image(_BackgroundTexture.Value, availSize);
            ImGui.SetCursorPos(cursorPos);
            //Header
            ImGui.Dummy(new Num.Vector2(availSize.X, availSize.Y * 0.45f));
            //Run button
            ImGui.SetCursorPosX(availSize.X * 0.375f);
            var launchSize = new Num.Vector2(availSize.X * 0.25f, availSize.Y * 0.25f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.5f, 0.5f, 0.5f, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Num.Vector4(0.8f, 0.8f, 0.8f, 1));
            if (ImGui.ImageButton("Uruchom", _LaunchTexture.Value, launchSize))
            {
                // Run Game
            }
            ImGui.PopStyleColor(2);
            ImGui.EndDisabled();
            //Spacing
            ImGui.Dummy(new Num.Vector2(availSize.X, availSize.Y * 0.1f));
            //Status text
            ImGui.SetCursorPosX(availSize.X * 0.5f - ImGui.CalcTextSize(_LastLogMessage).X * 0.5f);
            ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(0,0,0,1));
            ImGui.TextUnformatted(_LastLogMessage);
            ImGui.PopStyleColor();
            //Progress bar
            var progressBarStart = ImGui.GetCursorPos();
            var bottomAvailSize = ImGui.GetContentRegionAvail();
            var progressBarSize = new Num.Vector2(bottomAvailSize.X * 0.9f, bottomAvailSize.Y * 0.6f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + bottomAvailSize.X * 0.05f );
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5);
            ImGui.ProgressBar(_DownloadProgressValue, progressBarSize, "");
            ImGui.PopStyleVar();
            var text = $"{_DownloadFileName} {_DownloadProgressValue * 100f:F0}%";
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(availSize.X / 2 - textSize.X / 2);
            ImGui.SetCursorPosY(progressBarStart.Y + progressBarSize.Y * 0.5f - textSize.Y * 0.5f);
            ImGui.TextUnformatted(text);
        }

        private void DrawOptionsUI()
        {
            if (ImGui.InputText("Patch Url", ref Config.Instance.PatchUrl, 256))
            {
                Config.Save();
            }
            if (ImGui.InputText("Ultima Online Directory", ref Config.Instance.GamePath, 256))
            {
                Config.Save();
            }
            ImGui.BeginDisabled(_Refreshing);
            if (ImGui.MenuItem("Sprawdz aktualizacje"))
            {
                new Task(CheckUpdate).Start();
            }
            ImGui.EndDisabled();
        }

        private void DrawLogsUI()
        {
            ImGui.BeginChild("Log", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Border);
            ImGui.TextUnformatted(_LogText);
            ImGui.EndChild();
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
            _Refreshing = true;
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
                _Refreshing = false;
            }
        }


        private Progress<float> _downloadProgressHandler;

        private async void Update()
        {
            _Downloading = true;
            try
            {
                foreach (var fileInfo in _ChangedFiles)
                {
                    if (fileInfo.Version != -1)
                    {
                        Log($"Pobieram {fileInfo.File}");
                        _DownloadFileName = fileInfo.File;
                        if(File.Exists(fileInfo.File))
                            File.Delete(fileInfo.File);
                        var directory = Path.GetDirectoryName(fileInfo.File);
                        if(directory != null)
                            Directory.CreateDirectory(directory);
                        await using var file = new FileStream(Path.GetFullPath(fileInfo.File), FileMode.OpenOrCreate);
                        await _HttpClient.DownloadDataAsync($"{PatchUrl}/Nelderim/{fileInfo.File}", //TODO: How to pass 'Nelderim' here?
                            file,
                            _downloadProgressHandler);
                    }
                    else
                    {
                        if(File.Exists(fileInfo.File))
                            File.Delete(fileInfo.File);
                    }
                }
                Log("Wszystkie pliki sa aktualne");
                _LocalManifest = _ServerManifest;
                await File.WriteAllTextAsync(MANIFEST_FILE_NAME, JsonSerializer.Serialize(_LocalManifest));
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
            finally
            {
                _Downloading = false;
                _ChangedFiles = [];
                _DownloadProgressValue = 0f;
                _DownloadFileName = "";
                _Downloading = false;
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
            _LastLogMessage = text;
            _LogText += text + "\n";
        }
    }
}