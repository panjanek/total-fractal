using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using TotalFractal.Infrastructure;
using TotalFractal.Model;

namespace TotalFractal.Rendering;

/// <summary>
/// Owns the OpenGL rendering pipeline. Deliberately host-agnostic: it knows nothing about
/// WPF, WinForms or GLControl. The caller must make a GL context current before invoking
/// any method, and supply the surface size via <see cref="Resize"/>.
///
/// Stage 1 pipeline (quadratic-map grid deformation):
///   clear scatter texture -> solve.comp transforms each seed point once and scatters a
///   white splat into the texture -> display pass samples the texture onto an offscreen FBO.
///   The FBO is then blitted to the window (<see cref="BlitToDefault"/>) or read back to a
///   PNG (<see cref="ReadPixels"/>).
/// </summary>
public sealed class Renderer : IDisposable
{
    private ComputeProgram _solve = null!;
    private ShaderProgram _display = null!;
    private Texture _scatterTex = null!;
    private Framebuffer _fbo = null!;
    private FullscreenQuad _quad = null!;
    private ShaderStorageBuffer _seeds = null!;
    private UniformBuffer _config = null!;

    private int _width = 1;
    private int _height = 1;
    private bool _initialized;

    // Current map / view state. Hardcoded defaults for now; the WPF sliders will drive these
    // later via SetMap(). Default is the Example map so a no-args run shows the transform.
    private QuadraticMap _map = QuadraticMap.Example;
    private Vector4 _view = new(-2.5f, -2.5f, 2.5f, 2.5f);
    private int _splatRadius = 1;
    private int _iterations = 1;
    private int _pointCount;

    // Pending seed grid. Set on the UI thread (pure CPU); the actual GL upload is deferred to
    // RenderFrame (which runs with the context current), keeping all GL calls inside Paint.
    private (int lines, int samples, Vector2 min, Vector2 max) _seedParams = (20, 1000, new(-1f, -1f), new(1f, 1f));
    private bool _seedsDirty = true;

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

        _scatterTex = new Texture(SizedInternalFormat.Rgba8);
        _fbo = new Framebuffer(SizedInternalFormat.Rgba8);
        _quad = new FullscreenQuad();

        _seeds = new ShaderStorageBuffer();
        _config = new UniformBuffer();

        // Seeds are built lazily on the first RenderFrame (see _seedsDirty default).
        _initialized = true;
    }

    /// <summary>
    /// Request a new seed grid. Pure CPU: stores the parameters and marks the buffer dirty;
    /// the GL upload happens in <see cref="RenderFrame"/>. Safe to call from a UI event handler.
    /// </summary>
    public void SetSeeds(int linesPerAxis, int samplesPerLine, Vector2 min, Vector2 max)
    {
        _seedParams = (linesPerAxis, samplesPerLine, min, max);
        _seedsDirty = true;
    }

    private void RebuildSeeds()
    {
        var (lines, samples, min, max) = _seedParams;
        Vector2[] points = GridSeeds.Generate(lines, samples, min, max);
        _seeds.Upload(points);
        _pointCount = points.Length;
    }

    /// <summary>Set the map coefficients, view rectangle, splat radius, and iteration count.</summary>
    public void SetMap(QuadraticMap map, Vector4 view, int splatRadius, int iterations)
    {
        _map = map;
        _view = view;
        _splatRadius = Math.Max(0, splatRadius);
        _iterations = Math.Max(1, iterations);
    }

    public void Resize(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _scatterTex.Allocate(_width, _height);
        _fbo.Resize(_width, _height);
    }

    public void RenderFrame()
    {
        // 0) Rebuild the seed buffer if axis count / points per axis changed (GL upload here,
        //    where the context is current).
        if (_seedsDirty)
        {
            RebuildSeeds();
            _seedsDirty = false;
        }

        // 1) Push current state into the config UBO.
        var (cA, cB, cC) = _map.ToVec4();
        var cfg = new SolveConfig
        {
            CA = cA,
            CB = cB,
            CC = cC,
            View = _view,
            Dims = new Vector4i(_pointCount, _width, _height, _splatRadius),
            Iter = new Vector4i(_iterations, 0, 0, 0),
        };
        _config.Update(ref cfg);

        // 2) Clear the scatter target to zero, then scatter the transformed points into it.
        GL.ClearTexImage(_scatterTex.Handle, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

        _solve.Use();
        _scatterTex.BindImage(0, TextureAccess.WriteOnly);
        _seeds.Bind(0);
        _config.Bind(1);
        _solve.Dispatch((_pointCount + 255) / 256, 1, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit |
                         MemoryBarrierFlags.TextureFetchBarrierBit);

        // 3) Display pass - sample the scatter texture onto the offscreen FBO.
        _fbo.Bind();
        GL.Viewport(0, 0, _width, _height);
        GL.ClearColor(0f, 0f, 0f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _display.Use();
        _scatterTex.Bind(0);
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
        _seeds?.Dispose();
        _config?.Dispose();
        _quad?.Dispose();
        _fbo?.Dispose();
        _scatterTex?.Dispose();
        _display?.Dispose();
        _solve?.Dispose();
    }
}
