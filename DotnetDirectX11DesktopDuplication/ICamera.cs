using Silk.NET.Maths;
using System.Numerics;

public interface ICamera
{
    Matrix4x4 GetView();

    Vector4 GetEye();

    Matrix4x4 GetProjection();

    void Resize(Vector2D<int> windowSize);

    void Update(float dx, float dy, float tx, float ty, float md, double time);

    Matrix4x4 GetRotation();
}
