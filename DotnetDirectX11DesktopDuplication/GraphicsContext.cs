using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;

public struct GraphicsContext
{
    public ComPtr<IDXGISwapChain> swapChain;
    public ComPtr<ID3D11Device> device;
    public ComPtr<ID3D11DeviceContext> deviceContext;
}
