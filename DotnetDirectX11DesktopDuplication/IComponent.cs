public interface IComponent
{
    void Initialize(IApp app);

    void Draw(IApp app, ICamera camera, double time);
}

