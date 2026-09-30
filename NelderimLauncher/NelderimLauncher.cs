using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Nelderim.Utility;
using Num = System.Numerics;
using Hexa.NET.ImGui;

namespace Nelderim.Launcher
{
    public class NelderimLauncher : IDisposable
    {
        private const string Version = "2.1.0"; //Pass me from outside
        private const string MANIFEST_FILE_NAME = "Nelderim.manifest.json";
        private readonly HttpClient _HttpClient = new();
        
        private ImGuiRenderer _ImGuiRenderer;
        
        private enum SelfUpdateState { Checking, Updating, Failed, Done }
        private volatile SelfUpdateState _SelfUpdateState = SelfUpdateState.Checking;
        private string _SelfUpdateError = "";
        private volatile bool _Exit;

        private Manifest _LocalManifest;
        private List<FileInfo> _ChangedFiles;

        public unsafe NelderimLauncher(string[] args)
        {
            _ImGuiRenderer = new ImGuiRenderer(Version);
            _DownloadProgressHandler = new Progress<float>(f => _DownloadProgressValue = f);
            
            ImGui.StyleColorsDark();
            ImGui.GetStyle().FramePadding = new Num.Vector2(8, 4);
            ImGui.GetStyle().FrameRounding = 3;
            ImGui.GetStyle().Colors[(int)ImGuiCol.Button] = new Num.Vector4(0.5f, 0.5f, 0.5f, 1);
            ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered] = new Num.Vector4(0.8f, 0.8f, 0.8f, 1);
            ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive] = Constants.NelderimColor;
            ImGui.GetStyle().Colors[(int)ImGuiCol.Text] = Num.Vector4.One;
            ImGui.GetStyle().Colors[(int)ImGuiCol.FrameBg] =  new Num.Vector4(0.3f, 0.3f, 0.3f, 0.9f); //ProgressBar empty
            ImGui.GetStyle().Colors[(int)ImGuiCol.PlotHistogram] =  Constants.NelderimColor with {W = 0.65f}; //ProgressBar filled
            
            //Init manifest
            if(File.Exists(MANIFEST_FILE_NAME))
            {
                try
                {
                    var jsonText = File.ReadAllText(MANIFEST_FILE_NAME);
                    _LocalManifest = JsonSerializer.Deserialize(jsonText, ManifestJsonContext.Default.Manifest) ?? Manifest.Empty;
                }
                catch (JsonException)
                {
                    _LocalManifest = Manifest.Empty;
                }
            }
            else
            {
                _LocalManifest = Manifest.Empty;
            }

            CleanupOldLauncher();
#if DEBUG
            _SelfUpdateState = SelfUpdateState.Done; //Dev build never matches server hash
#else
            Task.Run(SelfUpdate);
#endif

            _ImGuiRenderer.LoadFontResource("Footlight-mt-light.ttf", 24);
        }

        public TextureData GetTexture(string name)
        {
            return _ImGuiRenderer.GetTexture(name);
        }
            
        private static Platform CurrentPlatform =>
            OperatingSystem.IsWindows() ? Platform.win : OperatingSystem.IsMacOS() ? Platform.osx : Platform.linux;
        
        public void Run()
        {
            bool done = false;
            while (!done)
            {
                if (_ImGuiRenderer.BeforeDraw(ref done))
                {
                    DrawUI(ref done);
                }
                _ImGuiRenderer.AfterDraw();
                if (_Exit)
                    done = true;
            }
        }
        
        private bool _ShowDebugWindow;
        private bool _ShowAdvancedOptions;
        private bool _ShowCompositionGuides;

        private bool _ShowLogs;
        private bool _ShowOptions;
        
        private string _LogText = "";
        private string _LastLogMessage = "";
        private volatile bool _Updating;
        private CancellationTokenSource? _UpdateCancellation;
        private string _DownloadFileName = "";
        private Progress<float> _DownloadProgressHandler;
        private float _DownloadProgressValue;
        private string PatchUrl => Config.Instance.PatchUrl;

        private void DrawUI(ref bool done)
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            if (ImGui.Begin("MainWindow",
                    ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings))
            {
                if (_SelfUpdateState != SelfUpdateState.Done)
                {
                    DrawSelfUpdateUI();
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
                    DrawMainUI(ref done);
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

        public Num.Vector4 NelderimTint = new(0.8f, 0.4f, 0.4f, 0.75f);
        
        private unsafe void DrawMainUI(ref bool done)
        {
            var viewport = ImGui.GetMainViewport();
            var minPos = ImGui.GetCursorStartPos();
            var maxPos = ImGui.GetContentRegionAvail();
            
            //Style
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(2,2));
            
            //Background
            ImGui.PushClipRect(Num.Vector2.Zero, viewport.WorkSize, false);
            ImGui.GetWindowDrawList().AddImage(new ImTextureRef(null, GetTexture("background").Id), Num.Vector2.Zero, viewport.WorkSize);
            ImGui.PopClipRect();
            
            //Logo
            var imageSize = new Num.Vector2(GetTexture("logo").Width, GetTexture("logo").Height) * 0.9f;
            var imagePos = new Num.Vector2(100, 20);
            ImGui.SetCursorPos(imagePos + new Num.Vector2(-3, 1));
            ImGui.ImageWithBg(new ImTextureRef(null, GetTexture("logo").Id), imageSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, new Num.Vector4(0,0,0,1)); //Outline
            ImGui.SetCursorPos(imagePos);
            ImGui.ImageWithBg(new ImTextureRef(null, GetTexture("logo").Id), imageSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, NelderimTint); //Logo
            
            //TopButtons
            var smallButtonSize = new Num.Vector2(37, 37);
            var smallButtonsStartPos = new Num.Vector2(maxPos.X - 140, 10);
            ImGui.SetCursorPos(smallButtonsStartPos);
            ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.1f, 0.1f, 0.1f, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Num.Vector4(0.2f, 0.2f, 0.2f, 0.7f));
            if(ImGui.ImageButton("WWW", new ImTextureRef(null, GetTexture("www").Id), smallButtonSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, new Num.Vector4(1,1,1,0.8f)))
            {
                Browser.Open("https://www.nelderim.pl");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton("Discord", new ImTextureRef(null, GetTexture("discord").Id), smallButtonSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, new Num.Vector4(1,1,1,0.8f)))
            {
                Browser.Open("https://discord.gg/GDyGncD");
            }
            ImGui.SameLine();
            if (ImGui.ImageButton("Patreon", new ImTextureRef(null, GetTexture("patreon").Id), smallButtonSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, new Num.Vector4(1,1,1,0.8f)))
            {
                Browser.Open("https://www.patreon.com/nelderim");
            }
            ImGui.PopStyleColor(2);
            
            //RightButtons
            var buttonSize = new Num.Vector2(140, 55);
            var buttonsStartPos = new Num.Vector2(maxPos.X - buttonSize.X, 140);
            ImGui.SetCursorPos(buttonsStartPos);
            ImGui.BeginGroup();
            ImGui.PushStyleColor(ImGuiCol.Button, new Num.Vector4(0.1f, 0.1f, 0.1f, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Num.Vector4(0.2f, 0.2f, 0.2f, 0.7f));
            if(ImGui.Button("Opcje", buttonSize))
            {
                _ShowOptions = true;
                _ShowLogs = false;
            }
            if (ImGui.Button("Logi", buttonSize))
            {
                _ShowOptions = false;
                _ShowLogs = true;
            }
            if (ImGui.Button("Rejestracja", buttonSize))
            {
                Browser.Open("https://www.nelderim.pl/rejestracja");
            }
            if (ImGui.Button("Regulamin", buttonSize))
            {
                Browser.Open("https://www.nelderim.pl/regulamin");
            }
            ImGui.PopStyleColor(2);
            ImGui.EndGroup();
            
            //Run button
            ImGui.PushClipRect(Num.Vector2.Zero, viewport.WorkSize, false);
            var wScale = _ImGuiRenderer.WindowWidth / (float)GetTexture("background").Width;
            var hScale = _ImGuiRenderer.WindowHeight / (float)GetTexture("background").Height;
            var launchSize = new Num.Vector2(GetTexture("launch").Width * wScale, GetTexture("launch").Height * hScale);
            var launchPos = new Num.Vector2((int)(viewport.WorkSize.X  - launchSize.X - 1), (int)(viewport.WorkSize.Y - launchSize.Y - 1));
            ImGui.SetCursorPos(launchPos);
            ImGui.Dummy(launchSize);
            ImGui.SetCursorPos(launchPos);
            ImGui.PushStyleColor(ImGuiCol.Button, Num.Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Num.Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, Num.Vector4.Zero);
            var entryPoint = _LocalManifest.EntryPointFor(CurrentPlatform);
            var canRun = !_Updating && !string.IsNullOrEmpty(entryPoint) && File.Exists(entryPoint);
            var launchTint = canRun && ImGui.IsItemHovered() ? Num.Vector4.One : new Num.Vector4(0.6f, 0.6f, 0.6f, 1);
            ImGui.BeginDisabled(!canRun);
            if (ImGui.ImageButton("Uruchom", new ImTextureRef(null, GetTexture("launch").Id), launchSize, Num.Vector2.Zero, Num.Vector2.One, Num.Vector4.Zero, launchTint))
            {
                SetExecutable(entryPoint);
                StartDetached(entryPoint, Path.GetDirectoryName(entryPoint));
                done = true;
            }
            ImGui.EndDisabled();
            ImGui.PopStyleColor(3);
            ImGui.PopClipRect();

            
            //Status text
            ImGui.SetCursorPosY(maxPos.Y * 0.85f);
            ImGui.SetCursorPosX(maxPos.X * 0.04f);
            ImGui.BeginGroup();
            
            var bottomAvailSize = ImGui.GetContentRegionAvail();
            bottomAvailSize.X *= 0.75f;
            var statusTextSize = ImGui.CalcTextSize(_LastLogMessage);
            var statusTextPos = new Num.Vector2(ImGui.GetCursorPosX() + (bottomAvailSize.X - statusTextSize.X) * 0.5f, ImGui.GetCursorPosY());
            ImGui.SetCursorPos(statusTextPos);
            if(_LastLogMessage != "")
            {
                ImGui.GetWindowDrawList().AddRectFilled(statusTextPos - ImGui.GetStyle().FramePadding,
                    statusTextPos + statusTextSize + ImGui.GetStyle().FramePadding,
                    ImGui.GetColorU32(new Num.Vector4(0f, 0f, 0f, 0.8f)));
            }
            ImGui.SetCursorPos(statusTextPos);
            ImGui.TextUnformatted(_LastLogMessage);
            
            //Progress bar
            var progressBarStart = ImGui.GetCursorPos();
            var progressBarSize = new Num.Vector2(bottomAvailSize.X, bottomAvailSize.Y * 0.6f);
            ImGui.SetCursorPos(progressBarStart);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
            ImGui.ProgressBar(_DownloadProgressValue, progressBarSize, "");
            ImGui.PopStyleVar(2);
            
            //Progress bar text
            var text = $"{_DownloadFileName} {_DownloadProgressValue * 100f:F0}%";
            var progressBarTextSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(progressBarStart.X + progressBarSize.X * 0.5f - progressBarTextSize.X * 0.5f);
            ImGui.SetCursorPosY(progressBarStart.Y + progressBarSize.Y * 0.5f - progressBarTextSize.Y * 0.5f);
            ImGui.TextUnformatted(text);
            ImGui.EndGroup();
            
            var updateButtonText = _Updating ? "Anuluj" : "Aktualizuj";
            var updateButtonTextSize = ImGui.CalcTextSize(updateButtonText);
            var updateButtonPosX = progressBarStart.X + progressBarSize.X * 0.5f - updateButtonTextSize.X * 0.5f;
            ImGui.SetCursorPosY(maxPos.Y * 0.8f);
            ImGui.SetCursorPosX(updateButtonPosX);
            if (_Updating)
            {
                if (ImGui.Button("Anuluj"))
                {
                    _UpdateCancellation?.Cancel();
                }
            }
            else if (ImGui.Button("Aktualizuj"))
            {
                StartUpdateTask(Update);
            }
            
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
            ImGui.NewLine();
            var spaceAvail = ImGui.GetContentRegionAvail();
            var updateText = _Updating ? "Anuluj" : "Aktualizuj";
            var updateTextSize = ImGui.CalcTextSize(updateText);
            var updateButtonSize = new Num.Vector2(updateTextSize.X + ImGui.GetStyle().WindowPadding.X, 36);
            ImGui.SetCursorPosX(spaceAvail.X - updateButtonSize.X);
            if (ImGui.Button(updateText, new Num.Vector2(0, 36)))
            {
                _ShowOptions = false;
                if (_Updating)
                    _UpdateCancellation?.Cancel();
                else
                    StartUpdateTask(Update);
            }
            ImGui.Spacing();
            ImGui.BeginDisabled(_Updating);
            var verifyText = "Weryfikuj instalacje";
            var verifyTextSize = ImGui.CalcTextSize(verifyText);
            var verifyButtonSize = new Num.Vector2(verifyTextSize.X + ImGui.GetStyle().WindowPadding.X, 36);
            ImGui.SetCursorPosX(spaceAvail.X - verifyButtonSize.X);
            if (ImGui.Button(verifyText, new Num.Vector2(0, 36)))
            {
                _ShowOptions = false;
                StartUpdateTask(Verify);
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
            ImGui.InputTextMultiline("Log", ref _LogText, 10000000, ImGui.GetContentRegionAvail(), ImGuiInputTextFlags.ReadOnly);
        }
        
        private void BackButton()
        {
            var availSpace = ImGui.GetContentRegionAvail();
            var backButtonSize = new Num.Vector2(availSpace.X * 0.04f, availSpace.X * 0.04f);
            ImGui.SetCursorPosX(availSpace.X - backButtonSize.X);
            if (ImGui.Button("<", backButtonSize))
            {
                _ShowOptions = false;
                _ShowLogs = false;
            }
        }

        private void DrawSelfUpdateUI()
        {
            switch (_SelfUpdateState)
            {
                case SelfUpdateState.Checking:
                    ImGui.Text("Sprawdzam aktualizacje...");
                    break;
                case SelfUpdateState.Updating:
                    ImGui.Text("Aktualizuje Nelderim Launcher");
                    ImGui.ProgressBar(_DownloadProgressValue, new Num.Vector2(ImGui.GetContentRegionAvail().X, 0), $"{_DownloadProgressValue * 100f:F0}%");
                    break;
                case SelfUpdateState.Failed:
                    ImGui.Text("Aktualizacja Nelderim Launcher nie powiodla sie");
                    ImGui.TextWrapped(_SelfUpdateError);
                    if (ImGui.Button("Sprobuj ponownie"))
                    {
                        Task.Run(SelfUpdate);
                    }
                    break;
            }
        }

        private async Task<Manifest> FetchManifest(CancellationToken cancellationToken = default)
        {
            var response = await _HttpClient.GetAsync($"{PatchUrl}/Nelderim.manifest.json", cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize(responseBody, ManifestJsonContext.Default.Manifest);
        }

        // Called from UI, _Updating is set before the task starts so it can't be started twice
        private void StartUpdateTask(Func<CancellationToken, Task> task)
        {
            var cancellation = new CancellationTokenSource();
            _UpdateCancellation = cancellation;
            _Updating = true;
            Task.Run(async () =>
            {
                try
                {
                    await task(cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    Log("Aktualizacja anulowana");
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                }
                finally
                {
                    _Updating = false;
                }
            });
        }

        private async Task Update(CancellationToken cancellationToken)
        {
            var serverManifest = await FetchManifest(cancellationToken);
            _ChangedFiles = _LocalManifest.ChangesBetween(serverManifest, CurrentPlatform);
            if (await UpdateFiles(_ChangedFiles, cancellationToken))
                SaveManifest(serverManifest);
        }
        
        private async Task Verify(CancellationToken cancellationToken)
        {
            var serverManifest = await FetchManifest(cancellationToken);
            if (await UpdateFiles(serverManifest.FilesFor(CurrentPlatform), cancellationToken))
                SaveManifest(serverManifest);
        }
        
        private async Task<bool> UpdateFiles(List<FileInfo> files, CancellationToken cancellationToken)
        {
            try
            {
                if(files.Count > 0)
                {
                    foreach (var fileInfo in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (fileInfo.Version != -1)
                        {
                            if (fileInfo.MergeKeys != null && File.Exists(fileInfo.File))
                            {
                                Log($"Aktualizuje {fileInfo.File}");
                                await MergeJsonFile(fileInfo, cancellationToken);
                                continue;
                            }
                            if (File.Exists(fileInfo.File))
                            {
                                Log($"Weryfikuje {fileInfo.File}");
                                if (Utils.Sha1Hash(fileInfo.File) == fileInfo.Sha1)
                                {
                                    continue;
                                }
                                else
                                    File.Delete(fileInfo.File);
                            }
                            
                            var directory = Path.GetDirectoryName(fileInfo.File);
                            if (!string.IsNullOrEmpty(directory))
                                Directory.CreateDirectory(directory);
                            
                            Log($"Pobieram {fileInfo.File}");
                            _DownloadFileName = fileInfo.File;
                            try
                            {
                                await using var file = new FileStream(Path.GetFullPath(fileInfo.File), FileMode.Create);
                                await _HttpClient.DownloadDataAsync(
                                    $"{PatchUrl}/{fileInfo.Source}/{fileInfo.File}",
                                    file,
                                    _DownloadProgressHandler,
                                    cancellationToken);
                            }
                            catch
                            {
                                File.Delete(fileInfo.File); //Don't leave a partial file behind
                                throw;
                            }
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
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Log(e.ToString());
                return false;
            }
            finally
            {
                _DownloadProgressValue = 0f;
                _DownloadFileName = "";
            }
            return true;
        }

        private async Task MergeJsonFile(FileInfo fileInfo, CancellationToken cancellationToken)
        {
            var serverText = await _HttpClient.GetStringAsync($"{PatchUrl}/{fileInfo.Source}/{fileInfo.File}", cancellationToken);
            var server = JsonNode.Parse(serverText)!.AsObject();

            JsonObject local;
            try
            {
                local = JsonNode.Parse(await File.ReadAllTextAsync(fileInfo.File))!.AsObject();
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or NullReferenceException)
            {
                await File.WriteAllTextAsync(fileInfo.File, serverText);
                return;
            }

            var changed = false;
            foreach (var key in fileInfo.MergeKeys!)
            {
                var serverValue = server[key];
                if (JsonNode.DeepEquals(local[key], serverValue) && local.ContainsKey(key) == server.ContainsKey(key))
                    continue;
                if (server.ContainsKey(key))
                    local[key] = serverValue?.DeepClone();
                else
                    local.Remove(key);
                changed = true;
            }
            if (changed)
                await File.WriteAllTextAsync(fileInfo.File, local.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void SetExecutable(string file)
        {
            if (OperatingSystem.IsWindows())
                return;
            const UnixFileMode exec = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(file, File.GetUnixFileMode(file) | exec);
        }

        private async void SaveManifest(Manifest manifest)
        {
            await File.WriteAllTextAsync(MANIFEST_FILE_NAME, JsonSerializer.Serialize(manifest, ManifestJsonContext.Default.Manifest));
            _LocalManifest = manifest;
        }
        
        private static void StartDetached(string file, string? workingDirectory)
        {
            var startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = workingDirectory;
            if (OperatingSystem.IsWindows())
            {
                startInfo.FileName = file;
                Process.Start(startInfo);
            }
            else
            {
                // Child inherits our stdout/stderr; once we exit, writes to them kill it (SIGPIPE).
                // Detach stdio, ignore SIGHUP and background it so it outlives the launcher.
                startInfo.FileName = "/bin/sh";
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add("nohup \"$0\" </dev/null >/dev/null 2>&1 &");
                startInfo.ArgumentList.Add(Path.GetFullPath(file));
                Process.Start(startInfo)?.WaitForExit(5000); //Wait for shell exit, child stays open
            }
        }

        private static void CleanupOldLauncher()
        {
            var currentPath = Environment.ProcessPath;
            if (currentPath == null)
                return;
            try
            {
                File.Delete(currentPath + ".old");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                //Previous launcher still exiting, next start will remove it
            }
        }

        private async Task SelfUpdate()
        {
            _SelfUpdateState = SelfUpdateState.Checking;
            var currentPath = Environment.ProcessPath;
            FileInfo? launcherInfo;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var serverManifest = await FetchManifest(timeout.Token);
                launcherInfo = serverManifest.LauncherFor(CurrentPlatform);
                if (launcherInfo == null || currentPath == null || SameHash(Utils.Sha1Hash(currentPath), launcherInfo.Sha1))
                {
                    _SelfUpdateState = SelfUpdateState.Done;
                    return;
                }
            }
            catch (Exception e)
            {
                //Server unreachable, no update known
                Log(e.ToString());
                _SelfUpdateState = SelfUpdateState.Done;
                return;
            }

            _SelfUpdateState = SelfUpdateState.Updating;
            var newPath = currentPath + ".new";
            var oldPath = currentPath + ".old";
            try
            {
                Log("Aktualizuje Nelderim Launcher");
                await using (var file = new FileStream(newPath, FileMode.Create))
                {
                    await _HttpClient.DownloadDataAsync($"{PatchUrl}/{launcherInfo.File}",
                        file,
                        _DownloadProgressHandler);
                }
                if (!SameHash(Utils.Sha1Hash(newPath), launcherInfo.Sha1))
                    throw new InvalidDataException("Suma kontrolna pobranego pliku nie zgadza sie");
                SetExecutable(newPath);

                // Running binary can be renamed on every platform, but not overwritten or deleted on Windows.
                // Rename also gives a new inode, overwriting a signed binary in place gets it killed on macOS.
                File.Delete(oldPath);
                File.Move(currentPath, oldPath);
                try
                {
                    File.Move(newPath, currentPath);
                }
                catch
                {
                    File.Move(oldPath, currentPath);
                    throw;
                }

                StartDetached(currentPath, Environment.CurrentDirectory);
                _Exit = true;
            }
            catch (Exception e)
            {
                try
                {
                    File.Delete(newPath);
                }
                catch (Exception deleteException) when (deleteException is IOException or UnauthorizedAccessException)
                {
                }
                Log(e.ToString());
                _SelfUpdateError = e.Message;
                _SelfUpdateState = SelfUpdateState.Failed;
            }
            finally
            {
                _DownloadProgressValue = 0f;
            }
        }

        private static bool SameHash(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        
        private void Log(string text)
        {
            _LastLogMessage = text;
            _LogText += $"{DateTime.UtcNow}: {text}\n";
        }

        public void Dispose()
        {
            _HttpClient.Dispose();
            _ImGuiRenderer.Dispose();
        }
    }
}