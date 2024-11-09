using System.Diagnostics;
using System.Text.Json;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NativeFileDialogSharp;
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
        
        Dictionary<string, Texture2D> _LoadedTextures = new();

        private IntPtr _BackgroundTexture;
        private IntPtr _LaunchTexture;
        private IntPtr _DiscordTexture;
        private IntPtr _WebsiteTexture;
        private IntPtr _LogoTexture;

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
            Window.AllowUserResizing = true;
            _downloadProgressHandler = new Progress<float>(f => _DownloadProgressValue = f);
            
            Window.Title = $"Nelderim Launcher {Version}";
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
                _LocalManifest = new Manifest(0, [], "");
            }
            
            //TODO: Bring me back
            // _autoUpdateInfos = FetchAutoUpdateInfo();
            // _updateAvailable = IsUpdateAvailable();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _BackgroundTexture = BindImage("background");
            _LaunchTexture = BindImage("launch");
            _DiscordTexture = BindImage("discord");
            _WebsiteTexture = BindImage("www");
            _LogoTexture = BindImage("logo");
            base.LoadContent();
        }

        private IntPtr BindImage(string fileName)
        {
            var texture = Texture2D.FromStream(_gdm.GraphicsDevice, GetType().Assembly.GetManifestResourceStream($"NelderimLauncher.{fileName}.png"));
            _LoadedTextures[fileName] = texture;
            return _ImGuiRenderer.BindTexture(texture);
        }

        protected override void Update(GameTime gameTime)
        {
            _ImGuiRenderer.Update(gameTime, IsActive);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            _ImGuiRenderer.BeforeDraw();
            DrawUI();
            _ImGuiRenderer.AfterDraw();

            base.Draw(gameTime);
        }
        
        private bool _ShowDebugWindow;
        private bool _ShowCompositionGuides;

        private bool _ShowLogs;
        private bool _ShowOptions;
        
        private string _LogText = "";
        private string _LastLogMessage = "";
        private bool _Refreshing;
        private bool _Downloading;
        private string _DownloadFileName = "";
        private float _DownloadProgressValue;
        private string PatchUrl => Config.Instance.PatchUrl;
        private DialogResult? _DialogResult;

        private void DrawUI()
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            if (ImGui.Begin("MainWindow",
                    ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
            {
                if(Config.Instance.GamePath == "")
                {
                    ImGui.OpenPopup("GamePathPopup");
                }
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
                
                if (ImGui.BeginPopupModal("GamePathPopup"))
                {
                    if (_DialogResult != null)
                    {
                        if(_DialogResult.IsOk)
                        {
                            Config.Instance.GamePath = _DialogResult.Path;
                            Config.Save();
                        }
                        _DialogResult = null;
                    }
                    ImGui.Text("Podaj sciezke gdzie zainstalowac Nelderim");
                    ImGui.InputText("##popup1", ref Config.Instance.GamePath, 512);
                    ImGui.SameLine();
                    if (ImGui.Button("..."))
                    {
                        _DialogResult = Dialog.FolderPicker();
                    }
                    if (ImGui.Button("OK"))
                    {
                        Config.Save();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
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

            if (ImGui.IsKeyPressed(ImGuiKey.F11))
            {
                _ShowCompositionGuides = !_ShowCompositionGuides;
            }
            ImGui.PopStyleVar();
        }
        
        private void DrawMainUI()
        {
            var viewport = ImGui.GetMainViewport();
            var minPos = ImGui.GetCursorStartPos();
            var maxPos = ImGui.GetContentRegionMax();
            
            //Style
            ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.5f, 0.5f, 0.5f, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Num.Vector4(0.8f, 0.8f, 0.8f, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Constants.NelderimColor);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(2, 2));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5);
            
            //Background
            ImGui.PushClipRect(Num.Vector2.Zero, viewport.WorkSize, false);
            ImGui.GetWindowDrawList().AddImage(_BackgroundTexture, Num.Vector2.Zero, viewport.WorkSize);
            ImGui.PopClipRect();
            
            //TopLeftButtons
            var squareImageButtonSize = new Num.Vector2(maxPos.X * 0.08f, maxPos.X * 0.08f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.1f, 0.1f, 0.1f, 0.5f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Num.Vector4(0.2f, 0.2f, 0.2f, 0.5f));
            if (ImGui.ImageButton("Discord", _DiscordTexture, squareImageButtonSize))
            {
                Browser.Open("https://discord.com/invite/K39RYrJG");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton("Website", _WebsiteTexture, squareImageButtonSize))
            {
                Browser.Open("https://nelderim.pl/");
            }
            ImGui.PopStyleColor(2);
            
            //TopRight Buttons 
            ImGui.SameLine();
            var squareButtonSize = squareImageButtonSize + ImGui.GetStyle().FramePadding * 2;
            var style = ImGui.GetStyle();
            ImGui.SetCursorPosX(maxPos.X - squareButtonSize.X * 2 - style.ItemSpacing.X );
            if(ImGui.Button("Opcje", squareButtonSize))
            {
                _ShowOptions = true;
                _ShowLogs = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Logi", squareButtonSize))
            {
                _ShowOptions = false;
                _ShowLogs = true;
            }
            
            //Logo
            ImGui.SameLine();
            var imageSize = new Num.Vector2(_LoadedTextures["logo"].Bounds.Width, _LoadedTextures["logo"].Bounds.Height) * 0.35f;
            ImGui.SetCursorPosX(maxPos.X / 2 - imageSize.X / 2);
            ImGui.SetCursorPosY(30);
            ImGui.Image(_LogoTexture, imageSize);
            
            //Spacing
            ImGui.Dummy(new Num.Vector2(maxPos.X, maxPos.Y * 0.35f));
            
            //Run button
            ImGui.SetCursorPosX(maxPos.X * 0.375f);
            var launchPos = ImGui.GetCursorPos();
            var launchSize = new Num.Vector2(maxPos.X * 0.25f, maxPos.Y * 0.25f);
            ImGui.Dummy(launchSize);
            ImGui.SetCursorPos(launchPos);
            ImGui.PushStyleColor(ImGuiCol.Button, Num.Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Num.Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Num.Vector4.Zero);
            var canRun = !string.IsNullOrEmpty(_LocalManifest.EntryPoint);
            var launchTint = canRun && ImGui.IsItemHovered() ? new Num.Vector4(1, 1, 1, 1) : new Num.Vector4(0.6f, 0.6f, 0.6f, 1);
            ImGui.BeginDisabled(!canRun);
            if (ImGui.ImageButton("Uruchom", _LaunchTexture, launchSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, launchTint))
            {
                Process.Start( _LocalManifest.EntryPoint);
                Exit();
            }
            ImGui.PopStyleColor(3);
            ImGui.EndDisabled();
            
            //Spacing
            ImGui.Dummy(new Num.Vector2(maxPos.X, maxPos.Y * 0.01f));
            
            //Status text
            ImGui.SetCursorPosX(maxPos.X * 0.5f - ImGui.CalcTextSize(_LastLogMessage).X * 0.5f);
            var textPos = ImGui.GetCursorPos();
            var textSize = ImGui.CalcTextSize(_LastLogMessage);
            //Text background
            if(_LastLogMessage != "")
            {
                ImGui.GetWindowDrawList().AddRectFilled(textPos - Num.Vector2.One,
                    textPos + textSize + Num.Vector2.One,
                    ImGui.GetColorU32(new Num.Vector4(1f, 1f, 1f, 0.6f)));
            }
            
            //Status text
            ImGui.SetCursorPos(textPos);
            ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(0,0,0,1));
            ImGui.TextUnformatted(_LastLogMessage);
            ImGui.PopStyleColor();
            
            //Progress bar
            var progressBarStart = ImGui.GetCursorPos();
            var bottomAvailSize = ImGui.GetContentRegionAvail();
            var progressBarSize = new Num.Vector2(bottomAvailSize.X * 0.9f, bottomAvailSize.Y * 0.6f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + bottomAvailSize.X * 0.05f );
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, Num.Vector4.One * 0.5f);//EmptyColor
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, Constants.NelderimColor);//FilledColor
            ImGui.ProgressBar(0.4f, progressBarSize, "");
            ImGui.PopStyleVar(2);
            
            //Progress bar text
            var text = $"{_DownloadFileName} {_DownloadProgressValue * 100f:F0}%";
            var progressBarTextSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(maxPos.X / 2 - progressBarTextSize.X / 2);
            ImGui.SetCursorPosY(progressBarStart.Y + progressBarSize.Y * 0.5f - progressBarTextSize.Y * 0.5f);
            ImGui.TextUnformatted(text);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);

            if (_ShowCompositionGuides)
            {
                var list = ImGui.GetWindowDrawList();
                
                list.AddRect(minPos, maxPos, 0xff0000ff);
                list.AddLine(new Num.Vector2(minPos.X, maxPos.Y * 0.333f), new Num.Vector2(maxPos.X, maxPos.Y * 0.333f), 0xff00ffff);
                list.AddLine(new Num.Vector2(minPos.X, maxPos.Y * 0.667f), new Num.Vector2(maxPos.X, maxPos.Y * 0.667f), 0xff00ffff);
                list.AddLine(new Num.Vector2(maxPos.X * 0.333f, minPos.Y), new Num.Vector2(maxPos.X * 0.333f, maxPos.Y), 0xff00ffff);
                list.AddLine(new Num.Vector2(maxPos.X * 0.667f, minPos.Y), new Num.Vector2(maxPos.X * 0.667f, maxPos.Y), 0xff00ffff);
            }
        }

        private void DrawOptionsUI()
        {
            BackButton();
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
            BackButton();
            ImGui.BeginChild("Log", ImGui.GetContentRegionAvail(), ImGuiChildFlags.Border);
            ImGui.TextUnformatted(_LogText);
            ImGui.EndChild();
        }

        private void BackButton()
        {
            var availSpace = ImGui.GetContentRegionAvail();
            var backButtonSize = new Num.Vector2(availSpace.X * 0.04f, availSpace.X * 0.04f);
            if (ImGui.Button("X", backButtonSize))
            {
                _ShowOptions = false;
                _ShowLogs = false;
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