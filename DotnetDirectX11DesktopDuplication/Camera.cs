using Silk.NET.Maths;
using System.Numerics;
using Matrix = System.Numerics.Matrix4x4;

public class Camera : ICamera
{
    private float aspectRatio;
    private float rdx, rdy, tdx, tdy, rx, ry, tx, ty;
    private float md;

    private Vector3 position = new Vector3(0, 0, 2);

    public Matrix GetProjection()
    {
        float fovAngleY = 70 * MathF.PI / 180.0f;
        return Matrix.CreatePerspectiveFieldOfView(fovAngleY, aspectRatio, 0.01f, 1000f);
    }

    public Matrix GetRotation()
    {
        float radiansX = (ry + rdy) / 250;
        float radiansY = (rx + rdx) / 250;
        return Matrix.CreateRotationY(ryy) * Matrix.CreateRotationY(radiansY) * Matrix.CreateRotationX(radiansX);
    }

    private float ryy = 0;

    public Matrix GetView()
    {
        var tdx = (this.tdx + tx) / 500;
        var tdy = (this.tdy + ty) / 500;

        var pos = position + new Vector3(-tdx, tdy, 0) + new Vector3(0, 0, md);
        var at = new Vector3() + new Vector3(-tdx, tdy, 0);

        return Matrix.CreateLookAt(pos, at, new Vector3(0, 1, 0));
    }

    public void Resize(Vector2D<int> windowSize)
    {
        aspectRatio = windowSize.X / (float)windowSize.Y;
    }

    public void Update(float rdx, float rdy, float tdx, float tdy, float md, double time)
    {
        int f = 5;
        this.rdx += (rdx - this.rdx) / f;
        this.rdy += (rdy - this.rdy) / f;
        this.tdx += (tdx - this.tdx) / f;
        this.tdy += (tdy - this.tdy) / f;
        this.md += (md - this.md) / f;

        //this.ryy += (float)time;
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
