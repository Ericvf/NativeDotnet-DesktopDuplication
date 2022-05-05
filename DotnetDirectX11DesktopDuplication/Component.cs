public abstract class Component : IComponent
{
    public abstract void Initialize(IApp app);

    public abstract void Draw(IApp app, ICamera camera, double time);
}

public interface IComponent
{
    void Initialize(IApp app);

    void Draw(IApp app, ICamera camera, double time);
}

