using OpenTK.Graphics.OpenGL4;
using TotalFractal.Infrastructure;

namespace TotalFractal.Rendering;

/// <summary>
/// An offscreen framebuffer with a single color texture attachment. Rendering into this
/// (rather than the default framebuffer) makes screenshots deterministic and independent
/// of window visibility.
/// </summary>
public sealed class Framebuffer : IDisposable
{
    public int Handle { get; }
    public Texture Color { get; }

    public Framebuffer(SizedInternalFormat colorFormat = SizedInternalFormat.Rgba8)
    {
        Handle = GL.GenFramebuffer();
        Color = new Texture(colorFormat);
    }

    public void Resize(int width, int height)
    {
        Color.Allocate(width, height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);
        GL.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            Color.Handle,
            0);
        GlDebug.CheckFramebuffer("offscreen color");
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Bind() => GL.BindFramebuffer(FramebufferTarget.Framebuffer, Handle);

    public void Dispose()
    {
        Color.Dispose();
        GL.DeleteFramebuffer(Handle);
    }
}
