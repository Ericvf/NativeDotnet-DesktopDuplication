using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Silk.NET.Maths;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default;
windowOptions.API = GraphicsAPI.None;
windowOptions.FramesPerSecond = 240;
windowOptions.UpdatesPerSecond = 60;
windowOptions.VSync = false;
windowOptions.Size = new Vector2D<int>(1200, 800);

var serviceProvider = BuildServiceProvider();
var app = serviceProvider.GetRequiredService<DesktopDuplicationApp>();

//Window.PrioritizeSdl();

using var window = Window.Create(windowOptions);
window.Load += async () => await app.Initialize(window, args);
window.Resize += s => app.Resize(s);
window.Update += t => app.Update(window, t);
window.Render += t => app.Draw(window, t);
window.Run();

static ServiceProvider BuildServiceProvider()
{
    var services = new ServiceCollection();

    services.AddLogging(builder =>
        builder.AddSimpleConsole(options =>
        {
            options.ColorBehavior = LoggerColorBehavior.Enabled;
            options.TimestampFormat = "[hh:mm:ss.FFF] ";
            options.SingleLine = true;
        }));

    services
        .AddSingleton<DesktopDuplicationApp>()
        .AddSingleton<IGraphicsService, GraphicsService>()
        .AddTransient<DesktopDuplicationComponent>()
        .AddTransient<TriangleComponent>()
        .AddTransient<RenderTargetComponent>()
        .AddTransient<GridComponent>()
        .AddTransient<StlMeshComponent>();

    return services.BuildServiceProvider();
}