using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Windowing;

public unsafe class GraphicsService : IGraphicsService
{
    public const Format GraphicsFormat = Format.FormatR8G8B8A8Unorm; // FormatR16G16B16A16Float;

    private readonly ILogger<GraphicsService> logger;
    private readonly D3D11 dx11api;

    public GraphicsService(ILogger<GraphicsService> logger)
    {
        this.dx11api = D3D11.GetApi();
        this.logger = logger;
    }

    public void InitializeWindow(IWindow window, ref GraphicsContext graphicsContext)
    {
        // Init device and swapchain
        SwapChainDesc swapChainDesc;
        swapChainDesc.BufferCount = 2;
        swapChainDesc.BufferDesc.Format = GraphicsFormat; //Format.FormatR8G8B8A8Unorm;
        swapChainDesc.BufferUsage = DXGI.UsageRenderTargetOutput;
        swapChainDesc.OutputWindow = window.Native.Win32.Value.Hwnd;
        swapChainDesc.SampleDesc.Count = 1;
        swapChainDesc.SampleDesc.Quality = 0;
        swapChainDesc.Windowed = 1;

        var createDeviceFlags = CreateDeviceFlag.CreateDeviceBgraSupport | CreateDeviceFlag.CreateDeviceDebug;

        logger.LogInformation("CreateDeviceAndSwapChain");
        dx11api.CreateDeviceAndSwapChain(
            null
            , D3DDriverType.D3DDriverTypeHardware
            , 0
            , (uint)createDeviceFlags
            , null
            , 0
            , D3D11.SdkVersion
            , &swapChainDesc
            , graphicsContext.swapChain.GetAddressOf()
            , graphicsContext.device.GetAddressOf()
            , null
            , graphicsContext.deviceContext.GetAddressOf())
            .ThrowHResult();
    }
}

public interface IGraphicsService
{
    void InitializeWindow(IWindow window, ref GraphicsContext graphicsContext);
}