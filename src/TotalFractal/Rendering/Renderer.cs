using OpenTK.Graphics.OpenGL4;
using TotalFractal.Infrastructure;

namespace TotalFractal.Rendering;

/// <summary>
/// Owns the OpenGL rendering pipeline. Deliberately host-agnostic: it knows nothing about
/// WPF, WinForms or GLControl. The caller must make a GL context current before invoking
/// any method, and supply the surface size via <see cref="Resize"/>.
///
/// Pipeline: solve.comp writes a texture (one invocation per pixel) -> display.vert/frag
/// samples that texture onto an offscreen FBO. The FBO is then either blitted to the
/// window (<see cref="BlitToDefault"/>) or read back to a PNG (<see cref="ReadPixels"/>).
/// </summary>
public sealed class Renderer : IDisposable
{
    private ComputeProgram _solve = null!;
    private ShaderProgram _display = null!;
    private Texture _fractalTex = null!;
    private Framebuffer _fbo = null!;
    private FullscreenQuad _quad = null!;

    private int _width = 1;
    private int _height = 1;
    private bool _initialized;

    public int Width => _width;
    public int Height => _height;

    public void Initialize()
    {
        if (_initialized)
            return;

        _solve = new ComputeProgram(EmbeddedShaderSource.Load("solve.comp"), "solve");
        _display = new ShaderProgram(
            EmbeddedShaderSource.Load("display.vert"),
            EmbeddedShaderSource.Load("display.frag"),
            "display");

        _fractalTex = new Texture(SizedInternalFormat.Rgba32f);
        _fbo = new Framebuffer(SizedInternalFormat.Rgba8);
        _quad = new FullscreenQuad();

        _initialized = true;
    }

    public void Resize(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _fractalTex.Allocate(_width, _height);
        _fbo.Resize(_width, _height);
    }

    public void RenderFrame()
    {
        // 1) Compute pass - fill the fractal texture, one invocation per pixel.
        _solve.Use();
        _fractalTex.BindImage(0, TextureAccess.WriteOnly);
        int groupsX = (_width + 7) / 8;
        int groupsY = (_height + 7) / 8;
        _solve.Dispatch(groupsX, groupsY);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit |
                         MemoryBarrierFlags.TextureFetchBarrierBit);

        // 2) Display pass - sample the texture onto the offscreen FBO.
        _fbo.Bind();
        GL.Viewport(0, 0, _width, _height);
        GL.ClearColor(0f, 0f, 0f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _display.Use();
        _fractalTex.Bind(0);
        _display.SetInt("srcTex", 0);
        _quad.Draw();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>Copy the offscreen FBO to the default framebuffer (the window). Call before SwapBuffers.</summary>
    public void BlitToDefault(int targetWidth, int targetHeight)
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo.Handle);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        GL.BlitFramebuffer(
            0, 0, _width, _height,
            0, 0, targetWidth, targetHeight,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>
    /// Read the offscreen FBO color attachment back as tightly packed BGRA bytes, bottom-up
    /// (the natural GL.ReadPixels layout). Pair with <see cref="ScreenshotWriter.Save"/>.
    /// </summary>
    public byte[] ReadPixels()
    {
        var pixels = new byte[_width * _height * 4];
        _fbo.Bind();
        GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
        GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
        GL.ReadPixels(0, 0, _width, _height, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        return pixels;
    }

    public void Dispose()
    {
        _quad?.Dispose();
        _fbo?.Dispose();
        _fractalTex?.Dispose();
        _display?.Dispose();
        _solve?.Dispose();
    }
}
