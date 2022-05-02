public interface IApp
{
    GraphicsContext GraphicsContext { get; }

    T Create<T>() where T : IComponent;
}
