using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Windowing;
using System.Numerics;

public unsafe class DesktopDuplicationApp
{
    private const int VertexCount = 6;

    public struct VertexPositionTexCoord
    {
        public Vector3 Position;

        public Vector2 TexCoord;
    }

    private ComPtr<IDXGISwapChain> swapChain;
    private ComPtr<ID3D11Device> device;
    private ComPtr<ID3D11DeviceContext> deviceContext;
    private ComPtr<ID3D11RenderTargetView> backbuffer;
    private ComPtr<IDXGIOutputDuplication> outputDuplication;
    private ComPtr<ID3D11SamplerState> pointSamplerState;
    private ComPtr<ID3D11VertexShader> vertexShader;
    private ComPtr<ID3D11PixelShader> pixelShader;
    private ComPtr<ID3D11InputLayout> inputLayout;
    public ComPtr<ID3D11Buffer> vertexBuffer;
    public ComPtr<ID3D11Texture2D> renderTargetTexture;
    public ComPtr<ID3D11RenderTargetView> renderTargetView;
    public ComPtr<ID3D11ShaderResourceView> renderTargetResourceView;
    public Viewport renderTargetViewport;
    public Viewport windowViewport;

    public void OnLoad(IWindow window)
    {
        var dx11api = D3D11.GetApi();
        var compilerApi = D3DCompiler.GetApi();

        // Init device and swapchain
        SwapChainDesc swapChainDesc;
        swapChainDesc.BufferCount = 1;
        swapChainDesc.BufferDesc.Format = Format.FormatR8G8B8A8Unorm;
        swapChainDesc.BufferUsage = DXGI.UsageRenderTargetOutput;
        swapChainDesc.OutputWindow = window.Native.Win32.Value.Hwnd;
        swapChainDesc.SampleDesc.Count = 4;
        swapChainDesc.Windowed = 1;

        var createDeviceFlags = CreateDeviceFlag.CreateDeviceBgraSupport | CreateDeviceFlag.CreateDeviceDebug;

        dx11api.CreateDeviceAndSwapChain(
            null
            , D3DDriverType.D3DDriverTypeHardware
            , 0
            , (uint)createDeviceFlags
            , null
            , 0
            , D3D11.SdkVersion
            , &swapChainDesc
            , this.swapChain.GetAddressOf()
            , this.device.GetAddressOf()
            , null
            , this.deviceContext.GetAddressOf())
            .ThrowHResult();

        var device = this.device.GetPinnableReference();
        var swapchain = this.swapChain.GetPinnableReference();
        var deviceContext = this.deviceContext.GetPinnableReference();

        // Init desktop duplication
        IDXGIDevice* dxgiDevice;
        device
            ->QueryInterface(IDXGIDevice.Guid.Pointer(), (void**)&dxgiDevice)
            .ThrowHResult();

        IDXGIAdapter* dxgiAdapter;
        dxgiDevice
            ->GetParent(IDXGIAdapter.Guid.Pointer(), (void**)&dxgiAdapter)
            .ThrowHResult();

        dxgiDevice->Release();

        uint outputNum = 0;
        IDXGIOutput* dxgiOutput = default;
        dxgiAdapter
            ->EnumOutputs(outputNum, ref dxgiOutput)
            .ThrowHResult();

        dxgiAdapter->Release();

        IDXGIOutput1* dxgiOutput1 = default;
        dxgiOutput
            ->QueryInterface(IDXGIOutput1.Guid.Pointer(), (void**)&dxgiOutput1)
            .ThrowHResult();

        dxgiOutput->Release();

        dxgiOutput1
            ->DuplicateOutput((IUnknown*)device, outputDuplication.GetAddressOf())
            .ThrowHResult();

        dxgiOutput1->Release();

        // Init rendertarget
        ID3D11Resource* backBufferTexture;

        swapChain.GetPinnableReference()
            ->GetBuffer(0, ID3D11Texture2D.Guid.Pointer(), (void**)&backBufferTexture)
            .ThrowHResult();

        device
            ->CreateRenderTargetView(backBufferTexture, null, backbuffer.GetAddressOf())
            .ThrowHResult();

        backBufferTexture->Release();

        // Init samplerstate
        SamplerDesc samplerDescription;
        samplerDescription.Filter = Filter.FilterMinMagMipPoint;
        samplerDescription.AddressU = TextureAddressMode.TextureAddressClamp;
        samplerDescription.AddressV = TextureAddressMode.TextureAddressClamp;
        samplerDescription.AddressW = TextureAddressMode.TextureAddressClamp;
        samplerDescription.ComparisonFunc = ComparisonFunc.ComparisonNever;
        samplerDescription.MinLOD = 0;
        samplerDescription.MaxLOD = float.MaxValue;

        device
            ->CreateSamplerState(&samplerDescription, pointSamplerState.GetAddressOf())
            .ThrowHResult();

        // Create vertex shader
        var compileFlags = 0u;
#if DEBUG
        compileFlags |= (1 << 0) | (1 << 2);
#endif

        ID3D10Blob* vertexShaderBlob;
        ID3D10Blob* errorMsgs;
        fixed (char* fileName = GetAssetFullPath(@"VertexShader.hlsl"))
        {
            compilerApi.CompileFromFile(fileName
            , null
            , null
            , "VS"
            , "vs_4_0"
            , compileFlags
            , 0
            , &vertexShaderBlob
            , &errorMsgs)
            .ThrowHResult();
        }

        device->CreateVertexShader(
            vertexShaderBlob->GetBufferPointer()
            , vertexShaderBlob->GetBufferSize()
            , null
            , vertexShader.GetAddressOf())
            .ThrowHResult();

        // Create input layout
        var inputLayouts = stackalloc InputElementDesc[]
        {
            new InputElementDesc
            {
                SemanticName = (byte*)SilkMarshal.StringToPtr("POSITION"),
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32Float,
                InputSlot = 0,
                AlignedByteOffset = 0,
                InputSlotClass = InputClassification.InputPerVertexData,
                InstanceDataStepRate = 0
            },
            new InputElementDesc
            {
                SemanticName = (byte*)SilkMarshal.StringToPtr("TEXCOORD"),
                SemanticIndex = 0,
                Format = Format.FormatR32G32Float,
                InputSlot = 0,
                AlignedByteOffset = 12,
                InputSlotClass = InputClassification.InputPerVertexData,
                InstanceDataStepRate = 0
            },
        };

        device
            ->CreateInputLayout(
                inputLayouts
                , 2
                , vertexShaderBlob->GetBufferPointer()
                , vertexShaderBlob->GetBufferSize()
                , inputLayout.GetAddressOf())
            .ThrowHResult();

        deviceContext->IASetInputLayout(inputLayout.GetPinnableReference());
        vertexShaderBlob->Release();

        // Pixel shader
        ID3D10Blob* pixelShaderBlob;
        fixed (char* fileName = GetAssetFullPath(@"PixelShader.hlsl"))
        {
            compilerApi.CompileFromFile(fileName
                , null
                , null
                , "PS"
                , "ps_4_0"
                , compileFlags
                , 0
                , &pixelShaderBlob
                , &errorMsgs)
            .ThrowHResult();
        }

        device
            ->CreatePixelShader(
                pixelShaderBlob->GetBufferPointer()
                , pixelShaderBlob->GetBufferSize()
                , null
                , pixelShader.GetAddressOf())
            .ThrowHResult();

        pixelShaderBlob->Release();

        // Vertex buffer
        var bufferDesc = new BufferDesc();
        bufferDesc.Usage = Usage.UsageDefault;
        bufferDesc.ByteWidth = (uint)sizeof(VertexPositionTexCoord) * VertexCount;
        bufferDesc.BindFlags = (uint)BindFlag.BindVertexBuffer;
        bufferDesc.CPUAccessFlags = 0;

        var vertices = stackalloc VertexPositionTexCoord[]
        {
            new VertexPositionTexCoord { Position = new Vector3(-1.0f, -1.0f, 0.0f), TexCoord = new Vector2(0.0f, 1.0f) },
            new VertexPositionTexCoord { Position = new Vector3(-1.0f, 1.0f, 0.0f), TexCoord = new Vector2(0.0f, 0.0f) },
            new VertexPositionTexCoord { Position = new Vector3(1.0f, -1.0f, 0.0f), TexCoord = new Vector2(1.0f, 1.0f) },
            new VertexPositionTexCoord { Position = new Vector3(1.0f, -1.0f, 0.0f), TexCoord = new Vector2(1.0f, 1.0f) },
            new VertexPositionTexCoord { Position = new Vector3(-1.0f, 1.0f, 0.0f), TexCoord = new Vector2(0.0f, 0.0f) },
            new VertexPositionTexCoord { Position = new Vector3(1.0f, 1.0f, 0.0f), TexCoord = new Vector2(1.0f, 0.0f) },
        };

        var subresourceData = new SubresourceData();
        subresourceData.PSysMem = vertices;

        device
            ->CreateBuffer(ref bufferDesc, ref subresourceData, vertexBuffer.GetAddressOf())
            .ThrowHResult();

        // Create viewports
       renderTargetViewport = new Viewport();
       renderTargetViewport.TopLeftX = 0;
       renderTargetViewport.TopLeftY = 0;
       renderTargetViewport.Width = 1280;
       renderTargetViewport.Height = 720;

       windowViewport = new Viewport();
       windowViewport.TopLeftX = 0;
       windowViewport.TopLeftY = 0;
       windowViewport.Width = window.Size.X;
       windowViewport.Height = window.Size.Y;
    }

    private string GetAssetFullPath(string assetName) => Path.Combine(AppContext.BaseDirectory, assetName);

    public void OnRender(double time)
    {
        using ComPtr<IDXGIResource> desktopResource = default;
        OutduplFrameInfo outputFrameInfo = default;

        var outputDuplication = this.outputDuplication.GetPinnableReference();
        var deviceContext = this.deviceContext.GetPinnableReference();
        var device = this.device.GetPinnableReference();

        var hr = (uint)outputDuplication->AcquireNextFrame(16, ref outputFrameInfo, desktopResource.GetAddressOf());

        if (hr == 0x887a0027)
            return;

        SilkMarshal.ThrowHResult((int)hr);

        using ComPtr<ID3D11Texture2D> acquiredDesktopImage = default;
        desktopResource.GetPinnableReference()->QueryInterface(ID3D11Texture2D.Guid.Pointer(), (void**)acquiredDesktopImage.GetAddressOf())
            .ThrowHResult();

        Texture2DDesc acquiredTextureDescription;
        acquiredDesktopImage.GetPinnableReference()->GetDesc(&acquiredTextureDescription);

        if (renderTargetTexture.Handle == null)
        {
            // Create render target texture
            Texture2DDesc renderTargetTextureDesc;
            renderTargetTextureDesc.Width = 1280;
            renderTargetTextureDesc.Height = 720;
            renderTargetTextureDesc.MipLevels = 1;
            renderTargetTextureDesc.ArraySize = 1;
            renderTargetTextureDesc.Format = Format.FormatB8G8R8A8Unorm;
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
            renderTargetResourceViewDesc.Format = acquiredTextureDescription.Format;
            renderTargetResourceViewDesc.Texture2D.MipLevels = acquiredTextureDescription.MipLevels;
            renderTargetResourceViewDesc.Texture2D.MostDetailedMip = acquiredTextureDescription.MipLevels - 1;
            renderTargetResourceViewDesc.ViewDimension = D3DSrvDimension.D3D11SrvDimensionTexture2D;

            device
                ->CreateShaderResourceView(renderTargetTextureResource, ref renderTargetResourceViewDesc, renderTargetResourceView.GetAddressOf())
                .ThrowHResult();
        }

        // Create acquired image resource view
        ShaderResourceViewDesc shaderResourceViewDesc = default;
        shaderResourceViewDesc.Format = acquiredTextureDescription.Format;
        shaderResourceViewDesc.Texture2D.MipLevels = acquiredTextureDescription.MipLevels;
        shaderResourceViewDesc.Texture2D.MostDetailedMip = acquiredTextureDescription.MipLevels - 1;
        shaderResourceViewDesc.ViewDimension = D3DSrvDimension.D3D11SrvDimensionTexture2D;

        using ComPtr<ID3D11ShaderResourceView> shaderResourceView = default;
        device
            ->CreateShaderResourceView((ID3D11Resource*)acquiredDesktopImage.GetPinnableReference(), ref shaderResourceViewDesc, shaderResourceView.GetAddressOf())
            .ThrowHResult();

        // Set render target
        deviceContext->RSSetViewports(1, ref renderTargetViewport);
        deviceContext->OMSetRenderTargets(1, renderTargetView.GetAddressOf(), null);

        // Set resources
        deviceContext->VSSetShader(vertexShader, null, 0);
        deviceContext->PSSetShader(pixelShader, null, 0);
        deviceContext->PSSetShaderResources(0, 1, shaderResourceView.GetAddressOf());
        deviceContext->PSSetSamplers(0, 1, pointSamplerState.GetAddressOf());

        deviceContext->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);

        uint stride = (uint)sizeof(VertexPositionTexCoord);
        uint offset = 0;
        deviceContext->IASetVertexBuffers(0, 1, vertexBuffer.GetAddressOf(), ref stride, ref offset);

        deviceContext->Draw(VertexCount, 0);

        // Set window
        deviceContext->RSSetViewports(1, ref windowViewport);
        deviceContext->OMSetRenderTargets(1, backbuffer.GetAddressOf(), null);

        var backgroundColor = stackalloc[] { 0f, 0f, 0f, 1.0f };
        deviceContext->ClearRenderTargetView(backbuffer.GetPinnableReference(), backgroundColor);

        // Set shader resource to rendertarget
        deviceContext->PSSetShaderResources(0, 1, renderTargetResourceView.GetAddressOf());

        // Draw window
        deviceContext->Draw(VertexCount, 0);

        swapChain.GetPinnableReference()->Present(0, 0).ThrowHResult();

        outputDuplication->ReleaseFrame();
    }
}

public static unsafe class HResultExtensions
{
    public static Guid* Pointer(this Guid guid) => &guid;

    public static void ThrowHResult(this int hResult)
    {
        SilkMarshal.ThrowHResult(hResult);
    }
}