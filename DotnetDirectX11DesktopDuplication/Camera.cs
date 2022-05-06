using Silk.NET.Maths;
using System.Numerics;

public class Camera : ICamera
{
    private float aspectRatio;
    private float rdx, rdy, tdx, tdy, rx, ry, tx, ty;
    private float md;

    private Vector3 position = new Vector3(0, 0, 2);

    public Matrix4x4 GetProjection()
    {
        float fovAngleY = 70 * MathF.PI / 180.0f;
        return Matrix4x4.CreatePerspectiveFieldOfView(fovAngleY, aspectRatio, 0.01f, 1000f);
    }

    public Matrix4x4 GetRotation()
    {
        float radiansX = (ry + rdy) / 250;
        float radiansY = (rx + rdx) / 250;
        return Matrix4x4.CreateRotationY(radiansY) * Matrix4x4.CreateRotationX(radiansX);
    }

    public Matrix4x4 GetView()
    {
        var tdx = (this.tdx + tx) / 500;
        var tdy = (this.tdy + ty) / 500;

        var pos = position + new Vector3(-tdx, tdy, 0) + new Vector3(0, 0, md);
        var at = new Vector3() + new Vector3(-tdx, tdy, 0);

        return Matrix4x4.CreateLookAt(pos, at, new Vector3(0, 1, 0));
    }

    public void Resize(Vector2D<int> windowSize)
    {
        aspectRatio = (float)windowSize.X / (float)windowSize.Y;
    }

    public void Update(float rdx, float rdy, float tdx, float tdy, float md)
    {
        this.rdx += (rdx - this.rdx) / 10;
        this.rdy += (rdy - this.rdy) / 10;
        this.tdx += (tdx - this.tdx) / 10;
        this.tdy += (tdy - this.tdy) / 10;
        this.md += (md - this.md) / 10;
    }

    public void SetRotation(float rx, float ry)
    {
        this.rx += rx;
        this.ry += ry;
        this.rdx = 0;
        this.rdy = 0;
    }

    public void SetTranslation(float tx, float ty)
    {
        this.tx += tx;
        this.ty += ty;
        this.tdx = 0;
        this.tdy = 0;
    }

    public Vector4 GetEye()
    {
        return new Vector4(position,0) + new Vector4(0,0, md, 0);
    }
}
