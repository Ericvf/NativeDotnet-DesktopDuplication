using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Numerics;

public unsafe class DesktopDuplicationComponent : Component
{
    private readonly ILogger<DesktopDuplicationComponent> logger;
    private readonly D3DCompiler compilerApi;

    private ComPtr<IDXGIOutputDuplication> outputDuplication = default;
    private ComPtr<ID3D11Buffer> vertexBuffer = default;
    private ComPtr<ID3D11InputLayout> inputLayout = default;
    private ComPtr<ID3D11PixelShader> pixelShader = default;
    private ComPtr<ID3D11VertexShader> vertexShader = default;

    private RenderTargetComponent renderTargetComponent;
    private const int VertexCount = 6;

    public DesktopDuplicationComponent(ILogger<DesktopDuplicationComponent> logger)
    {
        this.logger = logger;
        this.compilerApi = D3DCompiler.GetApi();
    }

    public ref ComPtr<ID3D11ShaderResourceView> RenderTarget => ref renderTargetComponent.renderTargetResourceView;

    internal void Resize(IApp app, Vector2D<int> windowSize)
    {
        renderTargetComponent.Resize(app, windowSize);
    }

    public override void Draw(IApp app, ICamera camera, double time)
    {
        using ComPtr<IDXGIResource> desktopResource = default;
        OutduplFrameInfo outputFrameInfo = default;

        var hr = (uint)outputDuplication.GetPinnableReference()->AcquireNextFrame(16, ref outputFrameInfo, desktopResource.GetAddressOf());

        if (hr == 0x887a0027)
            return;

        SilkMarshal.ThrowHResult((int)hr);

        using ComPtr<ID3D11Texture2D> acquiredDesktopImage = default;
        desktopResource.GetPinnableReference()->QueryInterface(ID3D11Texture2D.Guid.Pointer(), (void**)acquiredDesktopImage.GetAddressOf())
            .ThrowHResult();

        Texture2DDesc acquiredTextureDescription;
        acquiredDesktopImage.GetPinnableReference()->GetDesc(&acquiredTextureDescription);

        // Create acquired image resource view
        ShaderResourceViewDesc shaderResourceViewDesc = default;
        shaderResourceViewDesc.Format = acquiredTextureDescription.Format;
        shaderResourceViewDesc.Texture2D.MipLevels = acquiredTextureDescription.MipLevels;
        shaderResourceViewDesc.Texture2D.MostDetailedMip = acquiredTextureDescription.MipLevels - 1;
        shaderResourceViewDesc.ViewDimension = D3DSrvDimension.D3D11SrvDimensionTexture2D;

        using ComPtr<ID3D11ShaderResourceView> acquiredImageShaderResourceView = default;
        app.GraphicsContext.device.GetPinnableReference()
            ->CreateShaderResourceView((ID3D11Resource*)acquiredDesktopImage.GetPinnableReference(), ref shaderResourceViewDesc, acquiredImageShaderResourceView.GetAddressOf())
            .ThrowHResult();

        renderTargetComponent.PrepareDraw(app);

        var deviceContext = app.GraphicsContext.deviceContext.GetPinnableReference();

        // Set resources
        deviceContext->VSSetShader(vertexShader, null, 0);
        deviceContext->PSSetShader(pixelShader, null, 0);
        deviceContext->IASetInputLayout(inputLayout.GetPinnableReference());
        deviceContext->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
        deviceContext->PSSetShaderResources(0, 1, acquiredImageShaderResourceView.GetAddressOf());

        uint stride = (uint)sizeof(VertexPositionTexCoord);
        uint offset = 0;
        deviceContext->IASetVertexBuffers(0, 1, vertexBuffer.GetAddressOf(), ref stride, ref offset);
        deviceContext->Draw(VertexCount, 0);

        outputDuplication.GetPinnableReference()->ReleaseFrame();
    }

    public override void Initialize(IApp app)
    {
        renderTargetComponent = app.Create<RenderTargetComponent>();
        renderTargetComponent.Initialize(app);

        var device = app.GraphicsContext.device.GetPinnableReference();
        var swapChain = app.GraphicsContext.swapChain.GetPinnableReference();
        var deviceContext = app.GraphicsContext.deviceContext.GetPinnableReference();

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

        logger.LogInformation("EnumOutputs");
        uint outputNum = 0;
        IDXGIOutput* dxgiOutput = default;
        dxgiAdapter
            ->EnumOutputs(outputNum, ref dxgiOutput)
            .ThrowHResult();

        dxgiAdapter->Release();

        IDXGIOutput6* dxgiOutput6 = default;
        dxgiOutput
            ->QueryInterface(IDXGIOutput6.Guid.Pointer(), (void**)&dxgiOutput6)
            .ThrowHResult();

        dxgiOutput->Release();

        logger.LogInformation("DuplicateOutput");


        var formats = stackalloc[] { GraphicsService.GraphicsFormat }; //FormatR8G8B8A8Unorm
        dxgiOutput6
            ->DuplicateOutput1((IUnknown*)device, 0, 1, formats, outputDuplication.GetAddressOf())
            .ThrowHResult();

        dxgiOutput6->Release();

        // Create vertex shader
        var compileFlags = 0u;
#if DEBUG
        compileFlags |= (1 << 0) | (1 << 2);
#endif

        logger.LogInformation("CreateVertexShader");
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

        if (errorMsgs != null)
            errorMsgs->Release();

        // Pixel shader
        logger.LogInformation("CreatePixelShader");
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

        // Create input layout

        var lpPOSITION = (byte*)SilkMarshal.StringToPtr("POSITION", NativeStringEncoding.LPStr);
        var lpTEXCOORD = (byte*)SilkMarshal.StringToPtr("TEXCOORD", NativeStringEncoding.LPStr);

        var inputLayouts = stackalloc InputElementDesc[]
        {
            new InputElementDesc
            {
                SemanticName = lpPOSITION,
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32Float,
                InputSlot = 0,
                AlignedByteOffset = 0,
                InputSlotClass = InputClassification.InputPerVertexData,
                InstanceDataStepRate = 0
            },
            new InputElementDesc
            {
                SemanticName = lpTEXCOORD,
                SemanticIndex = 0,
                Format = Format.FormatR32G32Float,
                InputSlot = 0,
                AlignedByteOffset = 12,
                InputSlotClass = InputClassification.InputPerVertexData,
                InstanceDataStepRate = 0
            },
        };

        logger.LogInformation("CreateInputLayout");
        device
            ->CreateInputLayout(
                inputLayouts
                , 2
                , vertexShaderBlob->GetBufferPointer()
                , vertexShaderBlob->GetBufferSize()
                , inputLayout.GetAddressOf())
            .ThrowHResult();

        SilkMarshal.Free((nint)lpPOSITION);
        SilkMarshal.Free((nint)lpTEXCOORD);

        vertexShaderBlob->Release();

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

        logger.LogInformation("CreateBuffer (Vertex buffer)");
        device
            ->CreateBuffer(ref bufferDesc, ref subresourceData, vertexBuffer.GetAddressOf())
            .ThrowHResult();
    }

    private string GetAssetFullPath(string assetName) => Path.Combine(AppContext.BaseDirectory, assetName);
}
