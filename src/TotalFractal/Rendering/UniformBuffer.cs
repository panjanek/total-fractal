using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace TotalFractal.Rendering;

/// <summary>
/// A Uniform Buffer Object (UBO) holding a single std140 struct, re-uploaded each frame.
/// Used here for the per-frame config (coefficients, view rect, dimensions).
/// </summary>
public sealed class UniformBuffer : IDisposable
{
    public int Handle { get; }

    public UniformBuffer() => Handle = GL.GenBuffer();

    public void Update<T>(ref T data) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        GL.BindBuffer(BufferTarget.UniformBuffer, Handle);
        GL.BufferData(BufferTarget.UniformBuffer, size, ref data, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.UniformBuffer, 0);
    }

    public void Bind(int index) =>
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, index, Handle);

    public void Dispose() => GL.DeleteBuffer(Handle);
}
