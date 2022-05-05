using Silk.NET.Windowing;

public interface IApp
{
    GraphicsContext GraphicsContext { get; }

    Task Initialize(IWindow window, string[] args);

    T Create<T>() where T : IComponent;
}
