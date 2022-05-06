using Silk.NET.Windowing;

public interface IGraphicsService
{
    void InitializeWindow(IWindow window, ref GraphicsContext graphicsContext);
}