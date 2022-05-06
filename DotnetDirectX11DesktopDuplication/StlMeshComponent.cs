using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Maths;
using System.Numerics;
using Matrix = System.Numerics.Matrix4x4;

public unsafe class StlMeshComponent : Component
{
    private readonly ILogger<StlMeshComponent> logger;
    private ComPtr<ID3D11VertexShader> vertexShader = default;
    private ComPtr<ID3D11PixelShader> pixelShader = default;
    private ComPtr<ID3D11InputLayout> inputLayout = default;
    private ComPtr<ID3D11Buffer> vertexBuffer = default;
    private ComPtr<ID3D11Buffer> indexBuffer = default;
    private ComPtr<ID3D11Buffer> constantBuffer = default;
    private ComPtr<ID3D11RasterizerState> pRSsolidFrame = default;
    private ComPtr<ID3D11RasterizerState> pRSwireFrame = default;

    ModelViewProjectionWorldEyeConstantBuffer constantBufferData;
    private int meshVertexCount;
    private int meshIndexCount;
    private bool isLoaded = false;

    private const int gridSize = 16;
    private StlFile stlFile;

    public StlMeshComponent(ILogger<StlMeshComponent> logger)
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
        fixed (char* fileName = Helpers.GetAssetFullPath(@"MeshShader.hlsl"))
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
        fixed (char* fileName = Helpers.GetAssetFullPath(@"MeshShaderPS.hlsl"))
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
        var lpNORMAL = (byte*)SilkMarshal.StringToPtr("NORMAL", NativeStringEncoding.LPStr);

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
            new InputElementDesc
            {
                SemanticName = lpNORMAL,
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32Float,
                InputSlot = 0,
                AlignedByteOffset = 28,
                InputSlotClass = InputClassification.InputPerVertexData,
                InstanceDataStepRate = 0
            },
        };

        logger.LogInformation("CreateInputLayout");
        device
            ->CreateInputLayout(
                inputLayouts
                , 3
                , vertexShaderBlob->GetBufferPointer()
                , vertexShaderBlob->GetBufferSize()
                , inputLayout.GetAddressOf())
            .ThrowHResult();

        SilkMarshal.Free((nint)lpPOSITION);
        SilkMarshal.Free((nint)lpCOLOR);
        SilkMarshal.Free((nint)lpNORMAL);

        // Create constantBuffer
        var cbufferDesc = new BufferDesc();
        cbufferDesc.Usage = Usage.UsageDefault;
        cbufferDesc.ByteWidth = (uint)Helpers.RoundUp(sizeof(ModelViewProjectionWorldEyeConstantBuffer), 16);
        cbufferDesc.BindFlags = (uint)BindFlag.BindConstantBuffer;
        cbufferDesc.CPUAccessFlags = 0;

        device->CreateBuffer(ref cbufferDesc, null, constantBuffer.GetAddressOf())
            .ThrowHResult();

        // Rasterizer
        RasterizerDesc RSSolidFrameDesc;
        RSSolidFrameDesc.FillMode = FillMode.FillSolid;
        RSSolidFrameDesc.CullMode = CullMode.CullBack;
        RSSolidFrameDesc.ScissorEnable = 0;
        RSSolidFrameDesc.DepthBias = 0;
        RSSolidFrameDesc.FrontCounterClockwise = 0;
        RSSolidFrameDesc.DepthBiasClamp = 0;
        RSSolidFrameDesc.SlopeScaledDepthBias = 0;
        RSSolidFrameDesc.DepthClipEnable = 1;
        RSSolidFrameDesc.MultisampleEnable = 0;
        RSSolidFrameDesc.AntialiasedLineEnable = 1;

        device->CreateRasterizerState(&RSSolidFrameDesc, pRSsolidFrame.GetAddressOf());

        // Rasterizer
        RasterizerDesc RSWireFrameDesc;
        RSWireFrameDesc.FillMode = FillMode.FillWireframe;
        RSWireFrameDesc.CullMode = CullMode.CullBack;
        RSWireFrameDesc.ScissorEnable = 0;
        RSWireFrameDesc.DepthBias = 0;
        RSWireFrameDesc.FrontCounterClockwise = 0;
        RSWireFrameDesc.DepthBiasClamp = 0;
        RSWireFrameDesc.SlopeScaledDepthBias = 0;
        RSWireFrameDesc.DepthClipEnable = 1;
        RSWireFrameDesc.MultisampleEnable = 0;
        RSWireFrameDesc.AntialiasedLineEnable = 1;

        device->CreateRasterizerState(&RSWireFrameDesc, pRSwireFrame.GetAddressOf());

    }

    public Task LoadFile(IApp app, string fileName)
    {
        isLoaded = false;

        var device = app.GraphicsContext.device.GetPinnableReference();

        Initialize(app);

        // Create Grid
        (var vertexData, var indexData, meshVertexCount, meshIndexCount) = CreateMesh(fileName);

        // Vertex buffer
        var bufferDesc = new BufferDesc();
        bufferDesc.Usage = Usage.UsageDefault;
        bufferDesc.ByteWidth = (uint)sizeof(VertexPositionColorNormal) * (uint)meshVertexCount;
        bufferDesc.BindFlags = (uint)BindFlag.BindVertexBuffer;
        bufferDesc.CPUAccessFlags = 0;

        var subresourceData = new SubresourceData();
        fixed (VertexPositionColorNormal* data = vertexData)
        {
            subresourceData.PSysMem = data;
        }

        logger.LogInformation("CreateBuffer (Vertex buffer)");
        device
            ->CreateBuffer(ref bufferDesc, ref subresourceData, vertexBuffer.GetAddressOf())
            .ThrowHResult();

        // Index buffer
        var indexBufferDesc = new BufferDesc();
        indexBufferDesc.Usage = Usage.UsageDefault;
        indexBufferDesc.ByteWidth = sizeof(short) * (uint)meshIndexCount;
        indexBufferDesc.BindFlags = (uint)BindFlag.BindIndexBuffer;
        indexBufferDesc.CPUAccessFlags = 0;

        var indexBufferData = new SubresourceData();
        fixed (short* data = indexData)
        {
            indexBufferData.PSysMem = data;
        }

        logger.LogInformation("CreateBuffer (Index buffer)");
        device
            ->CreateBuffer(ref indexBufferDesc, ref indexBufferData, indexBuffer.GetAddressOf())
            .ThrowHResult();

        isLoaded = true;

        return Task.CompletedTask;
    }

    private (VertexPositionColorNormal[] vertexData, short[] indexData, int vertexCount, int indexCount) CreateMesh(string fileName)
    {
        using var fileStream = File.Open(fileName, FileMode.Open, FileAccess.Read);
        stlFile = new StlFile(fileStream, fileName);

        // Stl
        var facetCount = stlFile.Facets.Length;
        var gridVertexCount = facetCount * 3;

        var vertexList = new VertexPositionColorNormal[gridVertexCount];
        for (int i = 0, d = 0; i < facetCount; i++)
        {
            var facet = stlFile.Facets[i];
            vertexList[d++] = new VertexPositionColorNormal { Position = new Vector3(facet.v1.X, facet.v1.Y, facet.v1.Z), Color = new Vector4(1, 0, 0, 1), Normal = new Vector3(facet.normal.X, facet.normal.Y, facet.normal.Z) };
            vertexList[d++] = new VertexPositionColorNormal { Position = new Vector3(facet.v2.X, facet.v2.Y, facet.v2.Z), Color = new Vector4(0, 1, 0, 1), Normal = new Vector3(facet.normal.X, facet.normal.Y, facet.normal.Z) };
            vertexList[d++] = new VertexPositionColorNormal { Position = new Vector3(facet.v3.X, facet.v3.Y, facet.v3.Z), Color = new Vector4(0, 0, 1, 1), Normal = new Vector3(facet.normal.X, facet.normal.Y, facet.normal.Z) };
        }

        int segmentRadius = 20;
        int tubeRadius = 8;
        int segments = 40;
        int tubes = 50;

        int totalVertices = segments * tubes;
        int totalPrimitives = totalVertices * 2;
        int totalIndices = totalPrimitives * 3;

        var vertexNormalData = new VertexPositionColorNormal[totalVertices * 2];
        var vertexData = new VertexPositionColorNormal[totalVertices];
        var indexData = new short[totalIndices];

        //int countNormalVertices = 0;
        int countVertices = 0;
        int countIndices = 0;

        // Calculate size of segment and tube
        float segmentSize = 2 * MathF.PI / segments;
        float tubeSize = 2 * MathF.PI / tubes;

        // Create doubles for our xyz coordinates
        float x = 0;
        float y = 0;
        float z = 0;

        for (int i = 0; i < segments; i++)
        {
            // Save segments
            int currentSegment = i * tubes;
            int nextSegment = ((i + 1) % segments) * tubes;

            // Loop through the number of tubes
            for (int j = 0; j < tubes; j++)
            {
                // Save next tube offset
                int m = (j + 1) % tubes;

                // Calculate X, Y, Z coordinates.. 
                x = (segmentRadius + tubeRadius * MathF.Cos(j * tubeSize)) * MathF.Cos(i * segmentSize);
                y = (segmentRadius + tubeRadius * MathF.Cos(j * tubeSize)) * MathF.Sin(i * segmentSize);
                z = tubeRadius * MathF.Sin(j * tubeSize);

                // Add the vertex to global vertex list
                vertexData[countVertices++] = new VertexPositionColorNormal
                {
                    Position = new Vector3(x, y, z),
                    Color = new Vector4(),
                    Normal = new Vector3()
                };

                short v1 = (short)(currentSegment + j);
                short v2 = (short)(currentSegment + m);
                short v3 = (short)(nextSegment + m);
                short v4 = (short)(nextSegment + j);

                // Draw the first triangle
                indexData[countIndices++] = v1;
                indexData[countIndices++] = v2;
                indexData[countIndices++] = v3;

                // Finish the quad
                indexData[countIndices++] = v3;
                indexData[countIndices++] = v4;
                indexData[countIndices++] = v1;
            }
        }
        for (int i = 0; i < totalIndices; i += 3)
        {
            // Find all 3 vertices
            var v0 = vertexData[indexData[i]];
            var v1 = vertexData[indexData[i + 1]];
            var v2 = vertexData[indexData[i + 2]];

            // Calculate the triangle normal
            var vt2 = new Vector3(
                v1.Position.X - v0.Position.X,
                v1.Position.Y - v0.Position.Y,
                v1.Position.Z - v0.Position.Z
            );
            var vt1 = new Vector3(
                v2.Position.X - v0.Position.X,
                v2.Position.Y - v0.Position.Y,
                v2.Position.Z - v0.Position.Z
            );

            var triangleCenter = new Vector3(
                (v0.Position.X + v1.Position.X + v2.Position.X) / 3,
                (v0.Position.Y + v1.Position.Y + v2.Position.Y) / 3,
                (v0.Position.Z + v1.Position.Z + v2.Position.Z) / 3
            );

            var triangleNormal = new Vector3(
                (vt1.Y * vt2.Z) - (vt1.Z * vt2.Y),
                (vt1.Z * vt2.X) - (vt1.X * vt2.Z),
                (vt1.X * vt2.Y) - (vt1.Y * vt2.X)
            );

            v0.Normal = triangleNormal;
            v1.Normal = triangleNormal;
            v2.Normal = triangleNormal;

            //flatVertexData[i] = v0;
            //flatVertexData[i + 1] = v1;
            //flatVertexData[i + 2] = v2;

            vertexData[indexData[i]].Normal = vertexData[indexData[i]].Normal + triangleNormal;
            vertexData[indexData[i + 1]].Normal = vertexData[indexData[i + 1]].Normal + triangleNormal;
            vertexData[indexData[i + 2]].Normal = vertexData[indexData[i + 2]].Normal + triangleNormal;
        }
        for (int i = 0, k = 0; i < totalVertices; i++)
        {
            var n = Vector3.Normalize(vertexData[i].Normal);
            var posn = new Vector3(
                vertexData[i].Position.X + (n.X * 10),
                vertexData[i].Position.Y + (n.Y * 10),
                vertexData[i].Position.Z + (n.Z * 10)
            );

            vertexNormalData[k++] = new VertexPositionColorNormal()
            {
                Position = new Vector3(vertexData[i].Position.X, vertexData[i].Position.Y, vertexData[i].Position.Z),
                Color = new Vector4(0, 0, 0, 0),
                Normal = new Vector3(0, 0, 0)
            };

            vertexNormalData[k++] = new VertexPositionColorNormal()
            {
                Position = new Vector3(posn.X, posn.Y, posn.Z),
                Color = new Vector4(0, 0, 0, 0),
                Normal = new Vector3(0, 0, 0)
            };
        }

        //gridVertexCount = vertexData.Length;
        return (vertexList, indexData, gridVertexCount, indexData.Length);
    }

    public override void Draw(IApp app, ICamera camera, double time)
    {
        if (isLoaded)
        {
            var deviceContext = app.GraphicsContext.deviceContext.GetPinnableReference();
            var device = app.GraphicsContext.device.GetPinnableReference();

            deviceContext->IASetPrimitiveTopology(D3DPrimitiveTopology.D3D11PrimitiveTopologyTrianglelist);
            deviceContext->IASetInputLayout(inputLayout);
            deviceContext->VSSetShader(vertexShader, null, 0);
            deviceContext->PSSetShader(pixelShader, null, 0);

            //var translationMatrix = Matrix.Identity;
            var translationMatrix = Matrix.CreateTranslation(stlFile.translate.X, stlFile.translate.Y + (stlFile.msize.Y / 2), stlFile.translate.Z);
            var scaleMatrix = Matrix.CreateScale(stlFile.scale);
            var rotationMatrix = camera.GetRotation();

            var correctedModelMatrix = (translationMatrix * scaleMatrix);
            var modelMatrix = correctedModelMatrix * rotationMatrix;
            var viewMatrix = camera.GetView();
            var projectionMatrix = camera.GetProjection();
            var eye = camera.GetEye();

            constantBufferData.model = Matrix.Transpose(modelMatrix);
            constantBufferData.view = Matrix.Transpose(viewMatrix);
            constantBufferData.projection = Matrix.Transpose(projectionMatrix);

            if (Matrix.Invert(rotationMatrix, out Matrix inverted))
                constantBufferData.WorldInverseTranspose = Matrix.Transpose(inverted);

            constantBufferData.vecEye = eye * -1;

            fixed (ModelViewProjectionWorldEyeConstantBuffer* data = &constantBufferData)
            {
                deviceContext->UpdateSubresource((ID3D11Resource*)constantBuffer.GetPinnableReference(), 0, null, data, 0, 0);
            }

            uint stride = (uint)sizeof(VertexPositionColorNormal);
            uint offset = 0;

            deviceContext->VSSetConstantBuffers(0, 1, constantBuffer.GetAddressOf());
            deviceContext->RSSetState(pRSsolidFrame);

            deviceContext->IASetVertexBuffers(0, 1, vertexBuffer.GetAddressOf(), ref stride, ref offset);

            //deviceContext->IASetIndexBuffer(indexBuffer, Format.FormatR16Uint, 0);
            //deviceContext->DrawIndexed((uint)meshIndexCount, 0, 0);

            deviceContext->Draw((uint)meshVertexCount, 0);
        }
    }
}
