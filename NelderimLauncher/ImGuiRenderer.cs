using Hexa.NET.ImGui;
using Hexa.NET.SDL3;
using SDLBackend = Hexa.NET.ImGui.Backends.SDL3;

namespace Nelderim.Launcher;

public class ImGuiRenderer : IDisposable
{
    public const int WindowWidth = 1280;
    public const int WindowHeight = 720;
    
    private SDLWindowPtr _Window;
    private SDLGPUDevicePtr _GpuDevice;

    private Texture2D[] _LoadedTextures;

    public unsafe ImGuiRenderer(string version)
    {
        if (!SDL.Init((uint)(SDLInitFlags.Video | SDLInitFlags.Gamepad)))
        {
            Console.WriteLine($"Error: SDL_Init(): {SDL.GetErrorS()}");
            return;
        }

        float mainScale = SDL.GetDisplayContentScale(SDL.GetPrimaryDisplay());
        var windowFlags = (uint)(SDLWindowFlags.Hidden | SDLWindowFlags.HighPixelDensity);
        _Window = SDL.CreateWindow($"Nelderim Launcher {version}",
            (int)(WindowWidth * mainScale), (int)(WindowHeight * mainScale), windowFlags);
        if (_Window.IsNull)
        {
            Console.WriteLine($"Error: SDL_CreateWindow(): {SDL.GetErrorS()}");
            return;
        }

        SDL.SetWindowPosition(_Window, (int)SDL.SDL_WINDOWPOS_CENTERED_MASK, (int)SDL.SDL_WINDOWPOS_CENTERED_MASK);
        SDL.ShowWindow(_Window);

        _GpuDevice = SDL.CreateGPUDevice(
            (uint)(SDLGPUShaderFormat.Spirv | SDLGPUShaderFormat.Dxil | SDLGPUShaderFormat.Metallib),
            true, (byte*)null);
        if (_GpuDevice.IsNull)
        {
            Console.WriteLine($"Error: SDL_CreateGPUDevice(): {SDL.GetErrorS()}");
            return;
        }

        if (!SDL.ClaimWindowForGPUDevice(_GpuDevice, _Window))
        {
            Console.WriteLine($"Error: SDL_ClaimWindowForGPUDevice(): {SDL.GetErrorS()}");
            return;
        }

        SDL.SetGPUSwapchainParameters(_GpuDevice, _Window,
            SDLGPUSwapchainComposition.Sdr, SDLGPUPresentMode.Mailbox);

        var ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(ctx);
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard
                          | ImGuiConfigFlags.NavEnableGamepad
                          | ImGuiConfigFlags.DockingEnable
                          | ImGuiConfigFlags.ViewportsEnable;

        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.ScaleAllSizes(mainScale);
        style.FontScaleDpi = mainScale;
        io.ConfigDpiScaleFonts = true;
        io.ConfigDpiScaleViewports = true;

        if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            style.WindowRounding = 0.0f;
            style.Colors[(int)ImGuiCol.WindowBg].W = 1.0f;
        }

        SDLBackend.ImGuiImplSDL3.SetCurrentContext(ctx);
        SDLBackend.ImGuiImplSDL3.InitForSDLGPU((SDLBackend.SDLWindow*)_Window.Handle);

        SDLBackend.ImGuiImplSDLGPU3InitInfo initInfo = new()
        {
            Device = (SDLBackend.SDLGPUDevice*)_GpuDevice.Handle,
            ColorTargetFormat = (int)SDL.GetGPUSwapchainTextureFormat(_GpuDevice, _Window),
            MSAASamples = (int)SDLGPUSampleCount.Samplecount1
        };
        SDLBackend.ImGuiImplSDL3.SDLGPU3Init(&initInfo);
    }

    public unsafe void LoadFontResource(string fontFile, int fontSize)
    {
        var fontStream = GetType().Assembly.GetManifestResourceStream("NelderimLauncher.Resources." + fontFile);
        using var reader = new BinaryReader(fontStream);
        var fontData = reader.ReadBytes((int)fontStream.Length);
        ImFontPtr fontPtr;
        fixed (byte* ptr = fontData)
        {
            fontPtr = ImGui.GetIO().Fonts.AddFontFromMemoryTTF(ptr, fontData.Length, fontSize);
        }

        ImGui.PushFont(fontPtr, fontSize);
    }

    public virtual ImTextureID BindTexture(Texture2D texture)
    {
        var id = Array.IndexOf(_LoadedTextures, texture);
        if (id == -1)
        {
            //Zero index is null ImTextureID, so we just keep this one empty.
            for (var i = 1; i < _LoadedTextures.Length; i++)
            {
                if (_LoadedTextures[i] == null)
                {
                    _LoadedTextures[i] = texture;
                    id = i;
                    break;
                }
            }

            if (id == -1)
            {
                id = _LoadedTextures.Length;
                Array.Resize(ref _LoadedTextures, _LoadedTextures.Length * 2);
                _LoadedTextures[id] = texture;
            }
        }

        return new ImTextureID(id);
    }

    public virtual void UnbindTexture(ImTextureID textureId)
    {
        _LoadedTextures[(int)textureId.Handle] = null;
    }

    public unsafe bool BeforeDraw(out bool done)
    {
        done = false;
        SDLEvent e;
        while (SDL.PollEvent(&e))
        {
            SDLBackend.ImGuiImplSDL3.ProcessEvent((SDLBackend.SDLEvent*)&e);
            var type = (SDLEventType)e.Type;
            if (type == SDLEventType.Quit ||
                (type == SDLEventType.WindowCloseRequested &&
                 e.Window.WindowID == SDL.GetWindowID(_Window)))
            {
                done = true;
            }
        }

        if ((SDL.GetWindowFlags(_Window) & (ulong)SDLWindowFlags.Minimized) != 0)
        {
            SDL.Delay(10);
            return false;
        }

        SDLBackend.ImGuiImplSDL3.SDLGPU3NewFrame();
        SDLBackend.ImGuiImplSDL3.NewFrame();
        ImGui.NewFrame();

        return true;
    }

    public virtual unsafe void AfterDraw()
    {
        ImGui.Render();
        ImDrawData* drawData = ImGui.GetDrawData();
        bool isMinimized = drawData->DisplaySize.X <= 0 || drawData->DisplaySize.Y <= 0;

        SDLGPUCommandBuffer* commandBuffer = SDL.AcquireGPUCommandBuffer(_GpuDevice);
        SDLGPUTexture* swapTexture;
        SDL.AcquireGPUSwapchainTexture(commandBuffer, _Window, &swapTexture, null, null);

        if (swapTexture != null && !isMinimized)
        {
            SDLBackend.ImGuiImplSDL3.SDLGPU3PrepareDrawData(drawData, (SDLBackend.SDLGPUCommandBuffer*)commandBuffer);

            SDLGPUColorTargetInfo targetInfo = new()
            {
                Texture = swapTexture,
                ClearColor = new SDLFColor(),
                LoadOp = SDLGPULoadOp.Clear,
                StoreOp = SDLGPUStoreOp.Store,
                MipLevel = 0,
                LayerOrDepthPlane = 0,
                Cycle = 0
            };

            SDLGPURenderPass* renderPass = SDL.BeginGPURenderPass(commandBuffer, &targetInfo, 1, null);
            SDLBackend.ImGuiImplSDL3.SDLGPU3RenderDrawData(drawData, (SDLBackend.SDLGPUCommandBuffer*)commandBuffer,
                (SDLBackend.SDLGPURenderPass*)renderPass, null);
            SDL.EndGPURenderPass(renderPass);
        }

        //No need for viewports
        // if ((io.ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        // {
        //     ImGui.UpdatePlatformWindows();
        //     ImGui.RenderPlatformWindowsDefault();
        // }

        SDL.SubmitGPUCommandBuffer(commandBuffer);
    }

    public void Dispose()
    {
    }
}