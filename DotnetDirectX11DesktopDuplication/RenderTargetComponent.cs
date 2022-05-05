using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Maths;
using Silk.NET.Windowing;

public unsafe class RenderTargetComponent : Component
{
    public ComPtr<ID3D11Texture2D> renderTargetTexture;
    public ComPtr<ID3D11RenderTargetView> renderTargetView;
    public ComPtr<ID3D11ShaderResourceView> renderTargetResourceView;
    public Viewport renderTargetViewport;

    public override void Initialize(IApp app)
    {
        Resize(app, new Vector2D<int>(640, 360));
    }
    
    public void Resize(IApp app, Vector2D<int> windowSize)
    {
        var device = app.GraphicsContext.device.GetPinnableReference();
        if (renderTargetTexture.Handle != null)
        {
            renderTargetTexture.Release();
        }

        if (renderTargetView.Handle != null)
        {
            renderTargetView.Release();
        }

        if (renderTargetResourceView.Handle != null)
        {
            renderTargetResourceView.Release();
        }

        // Create viewports
        renderTargetViewport = new Viewport();
        renderTargetViewport.TopLeftX = 0;
        renderTargetViewport.TopLeftY = 0;
        renderTargetViewport.Width = windowSize.X;
        renderTargetViewport.Height = windowSize.Y;

        // Create render target texture
        Texture2DDesc renderTargetTextureDesc;
        renderTargetTextureDesc.Width = (uint)windowSize.X;
        renderTargetTextureDesc.Height = (uint)windowSize.Y;
        renderTargetTextureDesc.MipLevels = 1;
        renderTargetTextureDesc.ArraySize = 1;
        renderTargetTextureDesc.Format = GraphicsService.GraphicsFormat; //Format.FormatR8G8B8A8Unorm;
        renderTargetTextureDesc.SampleDesc.Count = 1;
        renderTargetTextureDesc.SampleDesc.Quality = 0;
        renderTargetTextureDesc.Usage = Usage.UsageDefault;
        renderTargetTextureDesc.BindFlags = (uint)(BindFlag.BindRenderTarget | BindFlag.BindShaderResource);
        renderTargetTextureDesc.CPUAccessFlags = 0;
        renderTargetTextureDesc.MiscFlags = 0;

        device
            ->CreateTexture2D(&renderTargetTextureDesc, null, renderTargetTexture.GetAddressOf())
            .ThrowHResult();

        // Create render target view
        RenderTargetViewDesc renderTargetViewDesc = default;
        renderTargetViewDesc.Format = renderTargetTextureDesc.Format;
        renderTargetViewDesc.ViewDimension = RtvDimension.RtvDimensionTexture2D;
        renderTargetViewDesc.Texture2D.MipSlice = 0;

        var renderTargetTextureResource = (ID3D11Resource*)renderTargetTexture.GetPinnableReference();
        device
            ->CreateRenderTargetView(renderTargetTextureResource, ref renderTargetViewDesc, renderTargetView.GetAddressOf())
            .ThrowHResult();

        // Create render target resource view
        ShaderResourceViewDesc renderTargetResourceViewDesc = default;
        renderTargetResourceViewDesc.Format = renderTargetTextureDesc.Format;
        renderTargetResourceViewDesc.Texture2D.MipLevels = 1;
        renderTargetResourceViewDesc.Texture2D.MostDetailedMip = 0;
        renderTargetResourceViewDesc.ViewDimension = D3DSrvDimension.D3D11SrvDimensionTexture2D;

        device
            ->CreateShaderResourceView(renderTargetTextureResource, ref renderTargetResourceViewDesc, renderTargetResourceView.GetAddressOf())
            .ThrowHResult();
    }

    public void PrepareDraw(IApp app)
    {
        var deviceContext = app.GraphicsContext.deviceContext.GetPinnableReference();
        // Set render target
        deviceContext->RSSetViewports(1, ref renderTargetViewport);
        deviceContext->OMSetRenderTargets(1, renderTargetView.GetAddressOf(), null);

        var backgroundColor = stackalloc[] { 1f, 0.5f, 0f, 1.0f };
        deviceContext->ClearRenderTargetView(renderTargetView.GetPinnableReference(), backgroundColor);
    }

    public override void Draw(IApp app, ICamera camera, double time)
    {
    }
}
