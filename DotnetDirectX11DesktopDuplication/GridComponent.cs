using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using System.Numerics;

public unsafe class GridComponent : Component
{
    private readonly ILogger<GridComponent> logger;
    private ComPtr<ID3D11VertexShader> vertexShader = default;
    private ComPtr<ID3D11PixelShader> pixelShader = default;
    private ComPtr<ID3D11InputLayout> inputLayout = default;
    private ComPtr<ID3D11Buffer> vertexBuffer = default;
    private ComPtr<ID3D11Buffer> constantBuffer = default;
    ModelViewProjectionConstantBuffer constantBufferData;

    private int gridVertexCount;
    private const int gridSize = 12;

    public GridComponent(ILogger<GridComponent> logger)
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
        fixed (char* fileName = GetAssetFullPath(@"GridShader.hlsl"))
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
        fixed (char* fileName = GetAssetFullPath(@"GridShaderPS.hlsl"))
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

        device->CreatePixelShader(
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

        // Create constantBuffer
        var cbufferDesc = new BufferDesc();
        cbufferDesc.Usage = Usage.UsageDefault;
        cbufferDesc.ByteWidth = (uint)Helpers.RoundUp(sizeof(ModelViewProjectionConstantBuffer), 16);
        cbufferDesc.BindFlags = (uint)BindFlag.BindConstantBuffer;
        cbufferDesc.CPUAccessFlags = 0;

        device->CreateBuffer(ref cbufferDesc, null, constantBuffer.GetAddressOf())
            .ThrowHResult();

        // Create Grid
        gridVertexCount = (gridSize + 1) * 4;
        var grid = CreateGrid(gridSize, 0.25f);

        // Vertex buffer
        var bufferDesc = new BufferDesc();
        bufferDesc.Usage = Usage.UsageDefault;
        bufferDesc.ByteWidth = (uint)sizeof(VertexPositionColor) * (uint)gridVertexCount;
        bufferDesc.BindFlags = (uint)BindFlag.BindVertexBuffer;
        bufferDesc.CPUAccessFlags = 0;

        var subresourceData = new SubresourceData();
        fixed (VertexPositionColor* data = grid)
        {
            subresourceData.PSysMem = data;
        }

        logger.LogInformation("CreateBuffer (Vertex buffer)");
        device
            ->CreateBuffer(ref bufferDesc, ref subresourceData, vertexBuffer.GetAddressOf())
            .ThrowHResult();
    }

    private VertexPositionColor[] CreateGrid(int size, float m = 4f)
    {
        var vertexList = new VertexPositionColor[(size + 1) * 4];
        float QuadDistance = (float)size / 2;
        var cc = Vector4.UnitZ;
        for (int i = 0; i < size + 1; i++)
        {
            float position = i - QuadDistance;
            vertexList[4 * i/**/] = new VertexPositionColor { Position = new Vector3(position * m, 0, QuadDistance * m), Color = cc };
            vertexList[4 * i + 1] = new VertexPositionColor { Position = new Vector3(position * m, 0, -QuadDistance * m), Color = cc };
            vertexList[4 * i + 2] = new VertexPositionColor { Position = new Vector3(QuadDistance * m, 0, position * m), Color = cc };
            vertexList[4 * i + 3] = new VertexPositionColor { Position = new Vector3(-QuadDistance * m, 0, position * m), Color = cc };
        }

        return vertexList;
    }

    public override void Draw(IApp app, ICamera camera, double time)
    {
        var deviceContext = app.GraphicsContext.deviceContext.GetPinnableReference();

        deviceContext->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyLinelist);
        deviceContext->IASetInputLayout(inputLayout);
        deviceContext->VSSetShader(vertexShader, null, 0);
        deviceContext->PSSetShader(pixelShader, null, 0);

        var modelMatrix = camera.GetRotation();
        var viewMatrix = camera.GetView();
        var projectionMatrix = camera.GetProjection();

        constantBufferData.model = Matrix4x4.Transpose(modelMatrix);
        constantBufferData.view = Matrix4x4.Transpose(viewMatrix);
        constantBufferData.projection = Matrix4x4.Transpose(projectionMatrix);

        fixed (ModelViewProjectionConstantBuffer* data = &constantBufferData)
        {
            deviceContext->UpdateSubresource((ID3D11Resource*)constantBuffer.GetPinnableReference(), 0, null, data, 0, 0);
        }

        deviceContext->VSSetConstantBuffers(0, 1, constantBuffer.GetAddressOf());

        uint stride = (uint)sizeof(VertexPositionColor);
        uint offset = 0;
        deviceContext->IASetVertexBuffers(0, 1, vertexBuffer.GetAddressOf(), ref stride, ref offset);
        deviceContext->Draw((uint)gridVertexCount, 0);
    }

    private string GetAssetFullPath(string assetName) => Path.Combine(AppContext.BaseDirectory, assetName);
}
