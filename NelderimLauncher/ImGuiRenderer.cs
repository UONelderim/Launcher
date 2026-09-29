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

    private readonly Dictionary< string, TextureData> _Textures = [];
    private List<nint> _TextureHandles = [];

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
            SDLGPUSwapchainComposition.Sdr, SDLGPUPresentMode.Vsync);

        var ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(ctx);
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard
                          | ImGuiConfigFlags.NavEnableGamepad;

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
        //Glyphs are rasterized lazily, so atlas must own font data for its whole lifetime
        var nativeData = ImGui.MemAlloc((nuint)fontData.Length);
        fontData.CopyTo(new Span<byte>(nativeData, fontData.Length));
        var fontPtr = ImGui.GetIO().Fonts.AddFontFromMemoryTTF(nativeData, fontData.Length, fontSize);

        ImGui.GetIO().FontDefault = fontPtr;
        ImGui.GetStyle().FontSizeBase = fontSize;
    }

    public TextureData GetTexture(string name)
    {
        if (_Textures.TryGetValue(name, out var texData))
        {
            return texData;
        }
        
        var png = $"{name}.png";
        Stream fileStream;
        if (File.Exists(png))
        {
            fileStream = File.OpenRead(png);
        }
        else
        {
            fileStream = GetType().Assembly.GetManifestResourceStream($"NelderimLauncher.Resources.{png}");
        }

        if (fileStream == null)
        {
            Console.WriteLine($"Unable to find resource for {name}");
        }
            
        using (fileStream)
        {
            var texture = LoadTexture(fileStream);
            _Textures[name] = texture;
            return texture;
        }
    }

    public unsafe TextureData LoadTexture(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.GetBuffer();

        SDLSurface* loaded;
        fixed (byte* ptr = data)
        {
            var io = SDL.IOFromConstMem(ptr, (nuint)ms.Length);
            loaded = SDL.LoadPNGIO(io, true);
        }
        if (loaded == null)
            throw new InvalidOperationException($"SDL_LoadPNG_IO(): {SDL.GetErrorS()}");

        //RGBA byte order to match R8G8B8A8Unorm
        SDLSurface* surface = SDL.ConvertSurface(loaded, SDLPixelFormat.Abgr8888);
        SDL.DestroySurface(loaded);
        if (surface == null)
            throw new InvalidOperationException($"SDL_ConvertSurface(): {SDL.GetErrorS()}");

        var width = surface->W;
        var height = surface->H;
        var rowSize = width * 4;

        var textureInfo = new SDLGPUTextureCreateInfo
        {
            Type = SDLGPUTextureType.Texturetype2D,
            Format = SDLGPUTextureFormat.R8G8B8A8Unorm,
            Usage = (uint)SDLGPUTextureUsageFlags.Sampler,
            Width = (uint)width,
            Height = (uint)height,
            LayerCountOrDepth = 1,
            NumLevels = 1,
        };
        SDLGPUTexture* gpuTexture = SDL.CreateGPUTexture(_GpuDevice, &textureInfo);
        if (gpuTexture == null)
        {
            SDL.DestroySurface(surface);
            throw new InvalidOperationException($"SDL_CreateGPUTexture(): {SDL.GetErrorS()}");
        }

        var transferInfo = new SDLGPUTransferBufferCreateInfo
        {
            Usage = SDLGPUTransferBufferUsage.Upload,
            Size = (uint)(rowSize * height),
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(_GpuDevice, &transferInfo);
        var dst = (byte*)SDL.MapGPUTransferBuffer(_GpuDevice, transferBuffer, false);
        var src = (byte*)surface->Pixels;
        for (var y = 0; y < height; y++)
        {
            Buffer.MemoryCopy(src + y * surface->Pitch, dst + y * rowSize, rowSize, rowSize);
        }
        SDL.UnmapGPUTransferBuffer(_GpuDevice, transferBuffer);
        SDL.DestroySurface(surface);

        var commandBuffer = SDL.AcquireGPUCommandBuffer(_GpuDevice);
        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        var source = new SDLGPUTextureTransferInfo { TransferBuffer = transferBuffer };
        var destination = new SDLGPUTextureRegion
        {
            Texture = gpuTexture,
            W = (uint)width,
            H = (uint)height,
            D = 1,
        };
        SDL.UploadToGPUTexture(copyPass, &source, &destination, false);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(commandBuffer);
        SDL.ReleaseGPUTransferBuffer(_GpuDevice, transferBuffer);

        _TextureHandles.Add((nint)gpuTexture);
        //SDLGPU3 backend expects SDL_GPUTexture* as ImTextureID
        return new TextureData(new ImTextureID(gpuTexture), width, height);
    }

    public unsafe bool BeforeDraw(ref bool done)
    {
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
        
        SDL.SubmitGPUCommandBuffer(commandBuffer);
    }

    public unsafe void Dispose()
    {
        SDL.WaitForGPUIdle(_GpuDevice);
        foreach (var texture in _TextureHandles)
            SDL.ReleaseGPUTexture(_GpuDevice, (SDLGPUTexture*)texture);
        _Textures.Clear();
        
        SDL.WaitForGPUIdle(_GpuDevice);
        SDLBackend.ImGuiImplSDL3.Shutdown();
        SDLBackend.ImGuiImplSDL3.SDLGPU3Shutdown();
        ImGui.DestroyContext();

        SDL.ReleaseWindowFromGPUDevice(_GpuDevice, _Window);
        SDL.DestroyGPUDevice(_GpuDevice);
        SDL.DestroyWindow(_Window);
        SDL.Quit();
    }
}

public readonly record struct TextureData(ImTextureID Id, int Width, int Height);
