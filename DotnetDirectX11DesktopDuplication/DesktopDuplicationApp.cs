using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.Windowing;

public unsafe class DesktopDuplicationApp : BaseApp
{
    private readonly ILogger<DesktopDuplicationApp> logger;

    private DesktopDuplicationComponent desktopDuplication;
    private TriangleComponent triangle;

    public DesktopDuplicationApp(IServiceProvider serviceProvider, IGraphicsService graphicsService, ILogger<DesktopDuplicationApp> logger)
        : base(serviceProvider, graphicsService)
    {
        this.logger = logger;
    }

    public override void Initialize(IWindow window)
    {
        base.Initialize(window);

        desktopDuplication = Create<DesktopDuplicationComponent>();
        desktopDuplication.Initialize(this);

        triangle = Create<TriangleComponent>();
        triangle.Initialize(this);
    }

    public override void Resize(Vector2D<int> windowSize)
    {
        if (desktopDuplication != null)
        {
            desktopDuplication.Resize(this, windowSize);
        }

        base.Resize(windowSize);
    }

    public void Draw(double time)
    {
        desktopDuplication.Draw(this);

        PrepareDraw();

        var deviceContext = GraphicsContext.deviceContext.GetPinnableReference();
        deviceContext->PSSetShaderResources(0, 1, desktopDuplication.RenderTarget.GetAddressOf());
        deviceContext->Draw(6, 0);

        triangle.Draw(this, time);

        base.Draw();
    }
}
