using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
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

    bool isLoaded = false;
    double timeDelta = 0;
    int fpsIncrement = 0;
    int fps = 0;

    public DesktopDuplicationApp(IServiceProvider serviceProvider, IGraphicsService graphicsService, ILogger<DesktopDuplicationApp> logger)
        : base(serviceProvider, graphicsService)
    {
        this.logger = logger;

        camera = new Camera();
    }

    public async override Task Initialize(IWindow window, string[] args)
    {
        await base.Initialize(window, args);

        // desktopDuplication = Create<DesktopDuplicationComponent>();
        // desktopDuplication.Initialize(this);

        input = window.CreateInput();
        input.Mice[0].MouseDown += DesktopDuplicationApp_MouseDown;
        input.Mice[0].MouseUp += DesktopDuplicationApp_MouseUp;
        input.Mice[0].MouseMove += DesktopDuplicationApp_MouseMove;
        input.Mice[0].Scroll += DesktopDuplicationApp_Scroll;

        //triangle = Create<TriangleComponent>();
        //triangle.Initialize(this);

        grid = Create<GridComponent>();
        grid.Initialize(this);

        if (args.Length > 0)
        {
            await loadFile(args[0]);
            isLoaded = true;
        }
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
            rdx = arg2.X - sx;
            rdy = arg2.Y - sy;
        }
        else if (isTrackingRight)
        {
            tdx = arg2.X - sx;
            tdy = arg2.Y - sy;
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

    public void Update(double t)
    {
        camera.Update(rdx, rdy, tdx, tdy, md);
    }

    private async Task loadFile(string fileName)
    {
        stlMesh = Create<StlMeshComponent>();
        await stlMesh.LoadFile(this, fileName);

        isLoaded = true;
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

    public int Draw(double time)
    {
        timeDelta += time;
        if (timeDelta > 1)
        {
            timeDelta = 0;
            fps = fpsIncrement;
            fpsIncrement = 0;
        }
        fpsIncrement++;

        // desktopDuplication.Draw(this, time);

        PrepareDraw();

        //var deviceContext = GraphicsContext.deviceContext.GetPinnableReference();
        //deviceContext->PSSetShaderResources(0, 1, desktopDuplication.RenderTarget.GetAddressOf());
        //deviceContext->Draw(6, 0);

        //triangle.Draw(this, camera, time);

        grid.Draw(this, camera, time);

        if (isLoaded)
        {
            stlMesh.Draw(this, camera, time);
        }

        base.Draw();

        return fps;
    }
}
