using System.Numerics;

struct ModelViewProjectionWorldEyeConstantBuffer
{
    public Matrix4x4 model;
    public Matrix4x4 view;
    public Matrix4x4 projection;

    public Matrix4x4 WorldInverseTranspose;
    public Vector4 vecEye;
};
