using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Numerics;

public class DesktopDuplicationApp : BaseApp
{
    private readonly ILogger<DesktopDuplicationApp> logger;
    private IInputContext input;

    private DesktopDuplicationComponent desktopDuplication;
    private TriangleComponent triangle;
    private GridComponent grid;
    private StlMeshComponent stlMesh;
    private Camera camera;

    bool isTrackingLeft, isTrackingRight = false;
    float px, py, sx, sy, rdx, rdy, tdx, tdy, md;

    public DesktopDuplicationApp(IServiceProvider serviceProvider, IGraphicsService graphicsService, ILogger<DesktopDuplicationApp> logger)
        : base(serviceProvider, graphicsService)
    {
        this.logger = logger;

        camera = new Camera();
    }

    private ComPtr<ID3D11DepthStencilState> depthStencilDefaultState = default;
    private ComPtr<ID3D11DepthStencilState> depthStencilDisabledState = default;


    public async override Task Initialize(IWindow window, string[] args)
    {
        await base.Initialize(window, args);

        //desktopDuplication = Create<DesktopDuplicationComponent>();
        //desktopDuplication.Initialize(this);

        input = window.CreateInput();
        input.Mice[0].MouseDown += DesktopDuplicationApp_MouseDown;
        input.Mice[0].MouseUp += DesktopDuplicationApp_MouseUp;
        input.Mice[0].MouseMove += DesktopDuplicationApp_MouseMove;
        input.Mice[0].Scroll += DesktopDuplicationApp_Scroll;

        triangle = Create<TriangleComponent>();
        triangle.Initialize(this);

        grid = Create<GridComponent>();
        grid.Initialize(this);
        InitializeDepthStencils();

        stlMesh = Create<StlMeshComponent>();
        if (args.Length > 0)
        {
            await loadFile(args[0]);
        }
    }

    private unsafe void InitializeDepthStencils()
    {
        ref var device = ref GraphicsContext.device.Get();

        DepthStencilDesc depthStencilDisabledDesc;
        depthStencilDisabledDesc.DepthEnable = 0;
        depthStencilDisabledDesc.DepthWriteMask = DepthWriteMask.DepthWriteMaskAll;
        depthStencilDisabledDesc.DepthFunc = ComparisonFunc.ComparisonLess;
        depthStencilDisabledDesc.StencilEnable = 1;
        depthStencilDisabledDesc.StencilReadMask = 0xFF;
        depthStencilDisabledDesc.StencilWriteMask = 0xFF;
        depthStencilDisabledDesc.FrontFace.StencilFailOp = StencilOp.StencilOpKeep;
        depthStencilDisabledDesc.FrontFace.StencilDepthFailOp = StencilOp.StencilOpIncr;
        depthStencilDisabledDesc.FrontFace.StencilPassOp = StencilOp.StencilOpKeep;
        depthStencilDisabledDesc.FrontFace.StencilFunc = ComparisonFunc.ComparisonAlways;
        depthStencilDisabledDesc.BackFace.StencilFailOp = StencilOp.StencilOpKeep;
        depthStencilDisabledDesc.BackFace.StencilDepthFailOp = StencilOp.StencilOpDecr;
        depthStencilDisabledDesc.BackFace.StencilPassOp = StencilOp.StencilOpKeep;
        depthStencilDisabledDesc.BackFace.StencilFunc = ComparisonFunc.ComparisonAlways;

        device.CreateDepthStencilState(ref depthStencilDisabledDesc, ref depthStencilDisabledState.GetPinnableReference())
            .ThrowHResult();

        DepthStencilDesc depthStencilDefaultDesc;
        depthStencilDefaultDesc.DepthEnable = 1;
        depthStencilDefaultDesc.DepthWriteMask = DepthWriteMask.DepthWriteMaskAll;
        depthStencilDefaultDesc.DepthFunc = ComparisonFunc.ComparisonLess;
        depthStencilDefaultDesc.StencilEnable = 1;
        depthStencilDefaultDesc.StencilReadMask = 0xFF;
        depthStencilDefaultDesc.StencilWriteMask = 0xFF;
        depthStencilDefaultDesc.FrontFace.StencilFailOp = StencilOp.StencilOpKeep;
        depthStencilDefaultDesc.FrontFace.StencilDepthFailOp = StencilOp.StencilOpIncr;
        depthStencilDefaultDesc.FrontFace.StencilPassOp = StencilOp.StencilOpKeep;
        depthStencilDefaultDesc.FrontFace.StencilFunc = ComparisonFunc.ComparisonAlways;
        depthStencilDefaultDesc.BackFace.StencilFailOp = StencilOp.StencilOpKeep;
        depthStencilDefaultDesc.BackFace.StencilDepthFailOp = StencilOp.StencilOpDecr;
        depthStencilDefaultDesc.BackFace.StencilPassOp = StencilOp.StencilOpKeep;
        depthStencilDefaultDesc.BackFace.StencilFunc = ComparisonFunc.ComparisonAlways;

        device.CreateDepthStencilState(ref depthStencilDefaultDesc, depthStencilDefaultState.GetAddressOf())
            .ThrowHResult();
    }

    private void DesktopDuplicationApp_Scroll(IMouse arg1, ScrollWheel arg2)
    {
        md -= (arg2.Y / 10);
    }

    private void DesktopDuplicationApp_MouseMove(IMouse arg1, Vector2 arg2)
    {
        px = arg2.X;
        py = arg2.Y;

        if (isTrackingLeft)
        {
            rdx = px - sx;
            rdy = py - sy;
        }
        else if (isTrackingRight)
        {
            tdx = px - sx;
            tdy = py - sy;
        }
    }

    private void DesktopDuplicationApp_MouseUp(IMouse arg1, MouseButton arg2)
    {
        if (isTrackingLeft)
        {
            isTrackingLeft = false;
            camera.SetRotation(rdx, rdy);
            rdx = 0;
            rdy = 0;
        }
        else if (isTrackingRight)
        {
            isTrackingRight = false;
            camera.SetTranslation(tdx, tdy);
            tdx = 0;
            tdy = 0;
        }
    }

    private void DesktopDuplicationApp_MouseDown(IMouse arg1, MouseButton arg2)
    {
        sx = px;
        sy = py;

        if (arg2 == MouseButton.Left)
        {
            isTrackingLeft = true;
        }
        else if (arg2 == MouseButton.Right)
        {
            isTrackingRight = true;
        }
    }

    public void Update(IWindow window, double time)
    {
        timeDelta += time;

        if (nexttime < timeDelta)
        {
            nexttime = timeDelta + (1f / window.UpdatesPerSecond);

            camera.Update(rdx, rdy, tdx, tdy, md, time);

            //HandleFPS(window, time);
        }
        else 
        Thread.Sleep(1);
    }

    private async Task loadFile(string fileName)
    {
        await Task.Delay(1);

        await stlMesh.LoadFile(this, fileName);
    }

    public override void Resize(Vector2D<int> windowSize)
    {
        if (camera != null)
        {
            camera.Resize(windowSize);
        }

        if (desktopDuplication != null)
        {
            desktopDuplication.Resize(this, windowSize);
        }

        base.Resize(windowSize);
    }

    double nexttime = 0;
    double nexttime2 = 0;

    public unsafe void Draw(IWindow window, double time)
    {
        timeDelta2 += time;

        PrepareDraw();

        if (nexttime2 < timeDelta2)
        {
            nexttime2 = timeDelta2 + (1f / window.FramesPerSecond);

            //var sw = new Stopwatch();
            //sw.Start();

            grid.Draw(this, camera, time);

            stlMesh.Draw(this, camera, time);

            base.Draw();

            HandleFPS2(window, time);
            //sw.Stop();
        }
        
        else
            Thread.Sleep(1);
        //desktopDuplication.Draw(this, camera, time);


        //var deviceContext = GraphicsContext.deviceContext.GetPinnableReference();
        //deviceContext->OMSetDepthStencilState(depthStencilDisabledState.GetPinnableReference(), 1);
        //deviceContext->OMSetDepthStencilState(depthStencilDefaultState.GetPinnableReference(), 1);
        //deviceContext->PSSetShaderResources(0, 1, desktopDuplication.RenderTarget.GetAddressOf());
        //deviceContext->Draw(6, 0);

        //triangle.Draw(this, camera, time);





    }

    private double timeDelta2 = 0;
    private int fpsIncrement2 = 0;

    private void HandleFPS2(IWindow window, double time)
    {
        if (timeDelta2 > 1)
        {
            window.Title = $"FPS2: {fpsIncrement2}";
            fpsIncrement2 = 0;
            timeDelta2 = 0;
            nexttime2 = 0;
        }

        fpsIncrement2++;
    }



    private double timeDelta = 0;
    private int fpsIncrement = 0;

    private void HandleFPS(IWindow window, double time)
    {
        if (timeDelta > 1)
        {
            window.Title = $"FPS: {fpsIncrement}";
            fpsIncrement = 0;
            timeDelta = 0;
            nexttime2 = 0;
        }

        fpsIncrement++;
    }


}
