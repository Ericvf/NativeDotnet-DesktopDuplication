public abstract class Component : IComponent
{
    public abstract void Initialize(IApp app);

}

public interface IComponent
{
    void Initialize(IApp app);
}

