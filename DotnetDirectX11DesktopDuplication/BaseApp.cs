using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.Windowing;

public unsafe class BaseApp : IApp
{
    private ComPtr<ID3D11RenderTargetView> backBufferRenderTargetView = default;
    private ComPtr<ID3D11Resource> backBufferTexture = default;
    private ComPtr<ID3D11Texture2D> depthStencilTexture = default;
    private ComPtr<ID3D11DepthStencilView> depthStencilView = default;
    private ComPtr<ID3D11DepthStencilState> depthStencilState = default;

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
        windowViewport.MinDepth = 0f;
        windowViewport.MaxDepth = 1f;
        resetDevice = true;
    }

    public virtual Task Initialize(IWindow window, string[] args)
    {
        graphicsService.InitializeWindow(window, ref graphicsContext);
        Resize(window.Size);
        ResetBuffers();
        resetDevice = false;
        return Task.CompletedTask;
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

        deviceContext->OMSetDepthStencilState(depthStencilState.GetPinnableReference(), 1);

        deviceContext->OMSetRenderTargets(1, backBufferRenderTargetView.GetAddressOf(), depthStencilView.GetPinnableReference());

        var backgroundColor = stackalloc[] { 1f, 1f, 1.0f, 1.0f };


        deviceContext->ClearRenderTargetView(backBufferRenderTargetView.GetPinnableReference(), backgroundColor);

        deviceContext->ClearDepthStencilView(depthStencilView.GetPinnableReference(), (uint)(ClearFlag.ClearDepth | ClearFlag.ClearStencil), 1.0f, 0);
    }

    public virtual void Draw()
    {
        GraphicsContext.swapChain.GetPinnableReference()
            ->Present(0, 0)
            .ThrowHResult();
    }

    private void ResetBuffers()
    {
        var device = graphicsContext.device.GetPinnableReference();

        if (backBufferRenderTargetView.Handle != null)
        {
            backBufferRenderTargetView.Release();
            backBufferTexture.Release();

            graphicsContext.swapChain.GetPinnableReference()->ResizeBuffers(2, (uint)windowViewport.Width, (uint)windowViewport.Height, Silk.NET.DXGI.Format.FormatR8G8B8A8Unorm, 0);
        }

        graphicsContext.swapChain.GetPinnableReference()
            ->GetBuffer(0, ID3D11Texture2D.Guid.Pointer(), (void**)backBufferTexture.GetAddressOf())
            .ThrowHResult();

        logger.LogInformation("CreateRenderTargetView (back buffer)");
        device
            ->CreateRenderTargetView(backBufferTexture, null, backBufferRenderTargetView.GetAddressOf())
            .ThrowHResult();


        Texture2DDesc depthStencilTextureDesc;
        depthStencilTextureDesc.Width = (uint)windowViewport.Width;
        depthStencilTextureDesc.Height = (uint)windowViewport.Height;
        depthStencilTextureDesc.MipLevels = 1;
        depthStencilTextureDesc.ArraySize = 1;
        depthStencilTextureDesc.Format = Silk.NET.DXGI.Format.FormatR32Typeless;
        depthStencilTextureDesc.SampleDesc.Count = 1;
        depthStencilTextureDesc.SampleDesc.Quality = 0;
        depthStencilTextureDesc.Usage = Usage.UsageDefault;
        depthStencilTextureDesc.BindFlags = (uint)(BindFlag.BindDepthStencil);
        depthStencilTextureDesc.CPUAccessFlags = 0;
        depthStencilTextureDesc.MiscFlags = 0;


        var depthStencilViewDesc = new DepthStencilViewDesc(
            viewDimension: DsvDimension.DsvDimensionTexture2D,
            format: Silk.NET.DXGI.Format.FormatD32Float);

        depthStencilViewDesc.Texture2D.MipSlice = 0;

        device
            ->CreateTexture2D(ref depthStencilTextureDesc, null, depthStencilTexture.GetAddressOf())
            .ThrowHResult();

        device
            ->CreateDepthStencilView((ID3D11Resource*)depthStencilTexture.GetPinnableReference(), ref depthStencilViewDesc, depthStencilView.GetAddressOf())
            .ThrowHResult();

        //DepthStencilDesc depthStencilDesc;
        //depthStencilDesc.DepthEnable = 1;
        //depthStencilDesc.DepthWriteMask = DepthWriteMask.DepthWriteMaskAll;
        //depthStencilDesc.DepthFunc = ComparisonFunc.ComparisonLess;
        //depthStencilDesc.StencilEnable = 1;
        //depthStencilDesc.StencilReadMask = 0xFF;
        //depthStencilDesc.StencilWriteMask = 0xFF;
        //depthStencilDesc.FrontFace.StencilFailOp = StencilOp.StencilOpKeep;
        //depthStencilDesc.FrontFace.StencilDepthFailOp = StencilOp.StencilOpIncr;
        //depthStencilDesc.FrontFace.StencilPassOp = StencilOp.StencilOpKeep;
        //depthStencilDesc.FrontFace.StencilFunc = ComparisonFunc.ComparisonAlways;
        //depthStencilDesc.BackFace.StencilFailOp = StencilOp.StencilOpKeep;
        //depthStencilDesc.BackFace.StencilDepthFailOp = StencilOp.StencilOpDecr;
        //depthStencilDesc.BackFace.StencilPassOp = StencilOp.StencilOpKeep;
        //depthStencilDesc.BackFace.StencilFunc = ComparisonFunc.ComparisonAlways;

        //device
        //    ->CreateDepthStencilState(ref depthStencilDesc, depthStencilState.GetAddressOf())
        //    .ThrowHResult();

    }

    public T Create<T>()
        where T : IComponent
    {
        var component = serviceProvider.GetRequiredService<T>();
        return component;
    }
}
