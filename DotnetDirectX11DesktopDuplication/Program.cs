using Silk.NET.Maths;
using Silk.NET.Windowing;

var windowOptions = WindowOptions.Default;
windowOptions.API = GraphicsAPI.None;
windowOptions.Size = new Vector2D<int>(1280, 720);

var app = new DesktopDuplicationApp();

using var window = Window.Create(windowOptions);
window.Load += () => app.OnLoad(window);
window.Render += (t) => app.OnRender(t);

window.Run();