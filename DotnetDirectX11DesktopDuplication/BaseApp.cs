using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.Windowing;

public unsafe class BaseApp : IApp
{
    private ComPtr<ID3D11RenderTargetView> backbuffer = default;
    private ComPtr<ID3D11Resource> backBufferTexture = default;

    private readonly IServiceProvider serviceProvider;
    private readonly IGraphicsService graphicsService;
    private readonly ILogger<BaseApp> logger;

    private GraphicsContext graphicsContext = default;
    private bool resetDevice = false;

    public GraphicsContext GraphicsContext => graphicsContext;

    public Viewport windowViewport;

    public BaseApp(IServiceProvider serviceProvider, IGraphicsService graphicsService)
    {
        this.serviceProvider = serviceProvider;
        this.graphicsService = graphicsService;
        this.logger = serviceProvider.GetRequiredService<ILogger<BaseApp>>();
    }

    public virtual void Resize(Vector2D<int> windowSize)
    {
        windowViewport.Width = windowSize.X;
        windowViewport.Height = windowSize.Y;
        resetDevice = true;
    }

    public virtual void Initialize(IWindow window)
    {
        graphicsService.InitializeWindow(window, ref graphicsContext);
        Resize(window.Size);
        ResetBuffers();
        resetDevice = false;
    }

    public virtual void PrepareDraw()
    {
        if (resetDevice)
        {
            ResetBuffers();
            resetDevice = false;
        }

        var deviceContext = graphicsContext.deviceContext.GetPinnableReference();

        deviceContext->RSSetViewports(1, ref windowViewport);

        deviceContext->OMSetRenderTargets(1, backbuffer.GetAddressOf(), null);

        var backgroundColor = stackalloc[] { 0f, 0f, 1.0f, 1.0f };
        deviceContext->ClearRenderTargetView(backbuffer.GetPinnableReference(), backgroundColor);
    }

    public virtual void Draw()
    {
        GraphicsContext.swapChain.GetPinnableReference()
            ->Present(0, 0)
            .ThrowHResult();
    }

    private void ResetBuffers()
    {
        if (backbuffer.Handle != null)
        {
            backbuffer.Release();
            backBufferTexture.Release();
            graphicsContext.swapChain.GetPinnableReference()->ResizeBuffers(2, (uint)windowViewport.Width, (uint)windowViewport.Height, Silk.NET.DXGI.Format.FormatR8G8B8A8Unorm, 0);
        }

        graphicsContext.swapChain.GetPinnableReference()
            ->GetBuffer(0, ID3D11Texture2D.Guid.Pointer(), (void**)backBufferTexture.GetAddressOf())
            .ThrowHResult();

        logger.LogInformation("CreateRenderTargetView (back buffer)");
        graphicsContext.device.GetPinnableReference()
            ->CreateRenderTargetView(backBufferTexture, null, backbuffer.GetAddressOf())
            .ThrowHResult();
    }

    public T Create<T>()
        where T : IComponent
    {
        var component = serviceProvider.GetRequiredService<T>();
        return component;
    }
}
