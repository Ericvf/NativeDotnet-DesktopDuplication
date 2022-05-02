using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Silk.NET.Maths;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default;
windowOptions.API = GraphicsAPI.None;
windowOptions.Size = new Vector2D<int>(1250, 500);

var serviceProvider = BuildServiceProvider();
var app = serviceProvider.GetRequiredService<DesktopDuplicationApp>();

using var window = Window.Create(windowOptions);
window.Load += () => app.Initialize(window);
window.Resize += (windowSize) => app.Resize(windowSize);
window.Render += (t) => app.Draw(t);
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
        .AddTransient<RenderTargetComponent>();

    return services.BuildServiceProvider();
}