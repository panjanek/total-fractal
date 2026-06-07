using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace TotalFractal.Rendering;

/// <summary>
/// A Shader Storage Buffer Object (SSBO). Used here to hold the CPU-generated seed points,
/// uploaded once and read by the compute shader.
/// </summary>
public sealed class ShaderStorageBuffer : IDisposable
{
    public int Handle { get; }
    public int Count { get; private set; }

    public ShaderStorageBuffer() => Handle = GL.GenBuffer();

    public void Upload<T>(T[] data) where T : struct
    {
        Count = data.Length;
        int stride = Marshal.SizeOf<T>();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, Handle);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, data.Length * stride, data, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public void Bind(int index) =>
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, index, Handle);

    public void Dispose() => GL.DeleteBuffer(Handle);
}
