using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using System.Numerics;

struct VertexPositionColor
{
    public Vector3 Position;
    public Vector4 Color;
};

public unsafe class TriangleComponent : Component
{
    const uint VertexCount = 3;

    private readonly ILogger<TriangleComponent> logger;
    private ComPtr<ID3D11VertexShader> vertexShader = default;
    private ComPtr<ID3D11PixelShader> pixelShader = default;
    private ComPtr<ID3D11InputLayout> inputLayout = default;
    private ComPtr<ID3D11Buffer> vertexBuffer = default;

    public TriangleComponent(ILogger<TriangleComponent> logger)
    {
        this.logger = logger;
    }

    public override void Initialize(IApp app)
    {
        var device = app.GraphicsContext.device.GetPinnableReference();
        var compilerApi = D3DCompiler.GetApi();

        // Create vertex shader
        var compileFlags = 0u;
#if DEBUG
        compileFlags |= (1 << 0) | (1 << 2);
#endif

        logger.LogInformation("CreateVertexShader");
        ID3D10Blob* vertexShaderBlob;
        ID3D10Blob* errorMsgs;
        fixed (char* fileName = GetAssetFullPath(@"SimpleShader.hlsl"))
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
        fixed (char* fileName = GetAssetFullPath(@"SimpleShaderPS.hlsl"))
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
        var lpCOLOR = (byte*)SilkMarshal.StringToPtr("COLOR", NativeStringEncoding.LPStr);

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
                SemanticName = lpCOLOR,
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32A32Float,
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
        SilkMarshal.Free((nint)lpCOLOR);

        vertexShaderBlob->Release();

        // Vertex buffer
        var bufferDesc = new BufferDesc();
        bufferDesc.Usage = Usage.UsageDefault;
        bufferDesc.ByteWidth = (uint)sizeof(VertexPositionColor) * VertexCount;
        bufferDesc.BindFlags = (uint)BindFlag.BindVertexBuffer;
        bufferDesc.CPUAccessFlags = 0;

        var vertices = stackalloc VertexPositionColor[]
        {
            new VertexPositionColor { Position = new Vector3(0.0f, 1, 0.0f), Color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f) },
            new VertexPositionColor { Position = new Vector3(1, -1, 0.0f), Color = new Vector4(0.0f, 1.0f, 0.0f, 1.0f) },
            new VertexPositionColor { Position = new Vector3(-1, -1, 0.0f), Color = new Vector4(0.0f, 0.0f, 1.0f, 1.0f) },
        };

        var subresourceData = new SubresourceData();
        subresourceData.PSysMem = vertices;

        logger.LogInformation("CreateBuffer (Vertex buffer)");
        device
            ->CreateBuffer(ref bufferDesc, ref subresourceData, vertexBuffer.GetAddressOf())
            .ThrowHResult();
    }

    public void Draw(IApp app, double time)
    {
        var deviceContext = app.GraphicsContext.deviceContext.GetPinnableReference();

        // Set resources
        deviceContext->VSSetShader(vertexShader, null, 0);
        deviceContext->PSSetShader(pixelShader, null, 0);
        deviceContext->IASetInputLayout(inputLayout);
        deviceContext->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);

        uint stride = (uint)sizeof(VertexPositionColor);
        uint offset = 0;
        deviceContext->IASetVertexBuffers(0, 1, vertexBuffer.GetAddressOf(), ref stride, ref offset);
        deviceContext->Draw(VertexCount, 0);
    }

    private string GetAssetFullPath(string assetName) => Path.Combine(AppContext.BaseDirectory, assetName);
}