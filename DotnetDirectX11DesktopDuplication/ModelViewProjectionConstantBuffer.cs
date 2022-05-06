using System.Numerics;
//[StructLayout(LayoutKind.Sequential, Pack = 16)]
struct ModelViewProjectionConstantBuffer
{
    public Matrix4x4 model;
    public Matrix4x4 view;
    public Matrix4x4 projection;
};
