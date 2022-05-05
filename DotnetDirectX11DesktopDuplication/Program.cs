using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Input;

var windowOptions = WindowOptions.Default;
windowOptions.API = GraphicsAPI.None;
//windowOptions.VSync = false;
//windowOptions.FramesPerSecond = 60;
//windowOptions.UpdatesPerSecond = 60;
windowOptions.Size = new Vector2D<int>(1200, 800);

var serviceProvider = BuildServiceProvider();
var app = serviceProvider.GetRequiredService<DesktopDuplicationApp>();

using var window = Window.Create(windowOptions);

window.Load += async () => await app.Initialize(window, args);
window.Resize += (windowSize) => app.Resize(windowSize);
window.Update += (t => app.Update(t));
window.Render += (t) => window.Title = $"FPS: {app.Draw(t)}";
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
        .AddTransient<StlMeshComponent>()
        ;

    return services.BuildServiceProvider();
}