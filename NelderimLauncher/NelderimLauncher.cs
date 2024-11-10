﻿using System.Diagnostics;
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
        private const string Version = "2.0.0-beta"; //Pass me from outside
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
        private List<FileInfo> _ChangedFiles;

        public NelderimLauncher(string[] args)
        {
            _gdm = new GraphicsDeviceManager(this);
            _gdm.PreferredBackBufferWidth = 600;
            _gdm.PreferredBackBufferHeight = 400;
            _gdm.PreferMultiSampling = true;
            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            _DownloadProgressHandler = new Progress<float>(f => _DownloadProgressValue = f);
            
            Window.Title = $"Nelderim Launcher {Version}";
        }

        protected override void Initialize()
        {
            _ImGuiRenderer = new ImGuiRenderer(_gdm.GraphicsDevice);
            _ImGuiRenderer.RebuildFontAtlas();

            //Init style
            ImGui.StyleColorsDark();
            ImGui.GetStyle().FramePadding = new Num.Vector2(8, 4);
            ImGui.GetStyle().FrameRounding = 3;
            ImGui.GetStyle().Colors[(int)ImGuiCol.Button] = new Num.Vector4(0.5f, 0.5f, 0.5f, 1);
            ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered] = new Num.Vector4(0.8f, 0.8f, 0.8f, 1);
            ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive] = Constants.NelderimColor;
            ImGui.GetStyle().Colors[(int)ImGuiCol.Text] = Num.Vector4.One;
            ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg] =  new Num.Vector4(0.3f, 0.3f, 0.3f, 1); //ProgressBar empty
            ImGui.GetStyle().Colors[(int)ImGuiCol.PlotHistogram] =  Constants.NelderimColor; //ProgressBar filled
            
            //Init manifest
            if(File.Exists(MANIFEST_FILE_NAME))
            {
                var jsonText = File.ReadAllText(MANIFEST_FILE_NAME);
                _LocalManifest = JsonSerializer.Deserialize<Manifest>(jsonText);
            }
            else
            {
                _LocalManifest = new Manifest(0, [], null, "");
            }
            Task.Run(Update);
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
            var png = $"{fileName}.png";
            Stream fileStream;
            if (File.Exists(png))
            {
                fileStream = File.OpenRead(png);
            }
            else
            {
                fileStream = GetType().Assembly.GetManifestResourceStream($"NelderimLauncher.{png}");
            }
            var texture = Texture2D.FromStream(_gdm.GraphicsDevice, fileStream);
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
        private bool _ShowAdvancedOptions;
        private bool _ShowCompositionGuides;

        private bool _ShowLogs;
        private bool _ShowOptions;
        
        private string _LogText = "";
        private string _LastLogMessage = "";
        private bool _Updating;
        private string _DownloadFileName = "";
        private Progress<float> _DownloadProgressHandler;
        private float _DownloadProgressValue;
        private string PatchUrl => Config.Instance.PatchUrl;

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
            if (ImGui.IsKeyDown(ImGuiKey.ModCtrl) && ImGui.IsKeyPressed(ImGuiKey.F12))
            {
                _ShowAdvancedOptions = !_ShowAdvancedOptions;
            }
            else if (ImGui.IsKeyDown(ImGuiKey.ModAlt) &&ImGui.IsKeyPressed(ImGuiKey.F12))
            {
                _ShowDebugWindow = !_ShowDebugWindow;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.F11))
            {
                _ShowCompositionGuides = !_ShowCompositionGuides;
            }
        }

        private void DrawMainUI()
        {
            var viewport = ImGui.GetMainViewport();
            var minPos = ImGui.GetCursorStartPos();
            var maxPos = ImGui.GetContentRegionMax();
            
            //Style
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(2,2));
            
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
            ImGui.SetCursorPosX(maxPos.X - squareButtonSize.X * 2 - ImGui.GetStyle().ItemSpacing.X );
            if (ImGui.Button("Logi", squareButtonSize))
            {
                _ShowOptions = false;
                _ShowLogs = true;
            }
            ImGui.SameLine();
            if(ImGui.Button("Opcje", squareButtonSize))
            {
                _ShowOptions = true;
                _ShowLogs = false;
            }
            
            //Logo
            var imageSize = new Num.Vector2(_LoadedTextures["logo"].Bounds.Width, _LoadedTextures["logo"].Bounds.Height) * 0.35f;
            ImGui.SetCursorPosX(maxPos.X / 2 - imageSize.X / 2);
            ImGui.SetCursorPosY(ImGui.GetCursorStartPos().Y + 20);
            ImGui.Image(_LogoTexture, imageSize);
            
            //Run button
            var launchSize = new Num.Vector2(maxPos.X * 0.25f, maxPos.Y * 0.25f);
            var launchPos = new Num.Vector2((maxPos.X  - launchSize.X) * 0.5f, maxPos.Y * 0.55f);
            ImGui.SetCursorPos(launchPos);
            ImGui.Dummy(launchSize);
            ImGui.SetCursorPos(launchPos);
            ImGui.PushStyleColor(ImGuiCol.Button, Num.Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Num.Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Num.Vector4.Zero);
            var canRun = !_Updating && !string.IsNullOrEmpty(_LocalManifest.EntryPoint) && File.Exists(_LocalManifest.EntryPoint);
            var launchTint = canRun && ImGui.IsItemHovered() ? new Num.Vector4(1, 1, 1, 1) : new Num.Vector4(0.6f, 0.6f, 0.6f, 1);
            ImGui.BeginDisabled(!canRun);
            if (ImGui.ImageButton("Uruchom", _LaunchTexture, launchSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, launchTint))
            {
                var startInfo = new ProcessStartInfo(_LocalManifest.EntryPoint);
                startInfo.WorkingDirectory = Path.GetDirectoryName(_LocalManifest.EntryPoint);
                Process.Start(startInfo);
                Exit();
            }
            ImGui.PopStyleColor(3);
            ImGui.EndDisabled();
            
            //Status text
            var textSize = ImGui.CalcTextSize(_LastLogMessage);
            var textPos = new Num.Vector2((maxPos.X - textSize.X) * 0.5f, maxPos.Y * 0.85f);
            ImGui.SetCursorPos(textPos);
            if(_LastLogMessage != "")
            {
                ImGui.GetWindowDrawList().AddRectFilled(textPos - ImGui.GetStyle().FramePadding,
                    textPos + textSize + ImGui.GetStyle().FramePadding,
                    ImGui.GetColorU32(new Num.Vector4(0f, 0f, 0f, 0.5f)));
            }
            ImGui.SetCursorPos(textPos);
            ImGui.TextUnformatted(_LastLogMessage);
            
            //Progress bar
            var progressBarStart = ImGui.GetCursorPos();
            var bottomAvailSize = ImGui.GetContentRegionAvail();
            var progressBarSize = new Num.Vector2(bottomAvailSize.X * 0.9f, bottomAvailSize.Y * 0.6f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + bottomAvailSize.X * 0.05f );
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
            ImGui.ProgressBar(_DownloadProgressValue, progressBarSize, "");
            ImGui.PopStyleVar(2);
            
            //Progress bar text
            var text = $"{_DownloadFileName} {_DownloadProgressValue * 100f:F0}%";
            var progressBarTextSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(maxPos.X / 2 - progressBarTextSize.X / 2);
            ImGui.SetCursorPosY(progressBarStart.Y + progressBarSize.Y * 0.5f - progressBarTextSize.Y * 0.5f);
            ImGui.TextUnformatted(text);
            
            //EndStyle
            ImGui.PopStyleVar();

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
            ImGui.BeginDisabled(_Updating);
            if (ImGui.Button("Aktualizuj", new Num.Vector2(0, 24)))
            {
                _ShowOptions = false;
                Task.Run(Update);
                
            }
            if (ImGui.Button("Weryfikuj instalacje", new Num.Vector2(0, 24)))
            {
                _ShowOptions = false;
                Task.Run(Verify);
                
            }
            ImGui.EndDisabled();
            ImGui.Spacing();
            if (_ShowAdvancedOptions)
            {
                ImGui.Text("Patch url");
                if (ImGui.InputText("##PatchUrl", ref Config.Instance.PatchUrl, 256))
                {
                    Config.Save();
                }
            }
        }

        private void DrawLogsUI()
        {
            BackButton();
            ImGui.InputTextMultiline("Log", ref _LogText, UInt32.MaxValue, ImGui.GetContentRegionAvail(), ImGuiInputTextFlags.ReadOnly);
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

        private async Task<Manifest> FetchManifest()
        {
            var response = await _HttpClient.GetAsync($"{PatchUrl}/Nelderim.manifest.json");
            var responseBody = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<Manifest>(responseBody);
        }

        private async void Update()
        {
            var serverManifest = await FetchManifest();
            _ChangedFiles = _LocalManifest.ChangesBetween(serverManifest);
            await UpdateFiles(_ChangedFiles);
            SaveManifest(serverManifest);
        }
        
        private async void Verify()
        {
            var serverManifest = await FetchManifest();
            await UpdateFiles(serverManifest.Files);
            SaveManifest(serverManifest);
        }
        
        private async Task<bool> UpdateFiles(List<FileInfo> files)
        {
            _Updating = true;
            try
            {
                if(files.Count > 0)
                {
                    foreach (var fileInfo in files)
                    {
                        if (fileInfo.Version != -1)
                        {
                            if (File.Exists(fileInfo.File))
                            {
                                Log($"Weryfikuje {fileInfo.File}");
                                if (Utils.Sha1Hash(fileInfo.File) == fileInfo.Sha1)
                                    continue;
                                else
                                    File.Delete(fileInfo.File);
                            }
                            
                            var directory = Path.GetDirectoryName(fileInfo.File);
                            if (!string.IsNullOrEmpty(directory))
                                Directory.CreateDirectory(directory);
                            
                            Log($"Pobieram {fileInfo.File}");
                            _DownloadFileName = fileInfo.File;
                            await using var file = new FileStream(Path.GetFullPath(fileInfo.File),
                                FileMode.OpenOrCreate);
                            await _HttpClient.DownloadDataAsync(
                                $"{PatchUrl}/Nelderim/{fileInfo.File}", //TODO: How to pass 'Nelderim' here?
                                file,
                                _DownloadProgressHandler);
                        }
                        else
                        {
                            if (File.Exists(fileInfo.File))
                                File.Delete(fileInfo.File);
                        }
                        _DownloadProgressValue = 0f;
                        _DownloadFileName = "";
                    }
                    Log("Aktualizacja zakonczona");
                }
                else
                {
                    Log("Wszystkie pliki aktualne");
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
                return false;
            }
            finally
            {
                _Updating = false;
                _DownloadProgressValue = 0f;
                _DownloadFileName = "";
            }
            return true;
        }

        private async void SaveManifest(Manifest manifest)
        {
            await File.WriteAllTextAsync(MANIFEST_FILE_NAME, JsonSerializer.Serialize(manifest));
            _LocalManifest = manifest;
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
            _LogText += $"{DateTime.UtcNow}: {text}\n";
        }
    }
}