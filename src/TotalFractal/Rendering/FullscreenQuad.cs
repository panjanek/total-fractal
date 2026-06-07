using OpenTK.Graphics.OpenGL4;

namespace TotalFractal.Rendering;

/// <summary>
/// Draws a single screen-covering triangle. No vertex data is supplied - the vertex
/// shader derives positions from gl_VertexID - but a non-zero VAO must still be bound in
/// the core profile, which is the only reason this object exists.
/// </summary>
public sealed class FullscreenQuad : IDisposable
{
    private readonly int _vao;

    public FullscreenQuad() => _vao = GL.GenVertexArray();

    public void Draw()
    {
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
    }

    public void Dispose() => GL.DeleteVertexArray(_vao);
}
