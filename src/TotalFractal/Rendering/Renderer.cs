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
    private ComputeProgram _escape = null!;
    private ComputeProgram _coeffProgram = null!;
    private ShaderProgram _display = null!;
    private Texture _scatterTex = null!;
    private Texture _fractalTex = null!;
    private Texture _coeffTex = null!;
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
    private int _splatRadius = 1;
    private int _iterations = 1;
    private int _pointCount;

    // Shared world-space view, navigated by the mouse and applied to BOTH passes. Stored as
    // center + half-height; the rectangle is derived per-frame with the panel aspect so pixels
    // stay square. Zooming shrinks the half-height (smaller plane section at constant resolution).
    private Vector2 _viewCenter = new(0f, 0f);
    private float _viewHalfHeight = 2.5f;
    private const float MinHalfHeight = 1e-4f;
    private const float MaxHalfHeight = 1e4f;

    // Escape-time iteration cap for the fractal panels (driven by the "Max iterations" dropdown).
    private int _maxIterations = 200;

    // Which panel is maximized (index into the active texture list). The others become insets.
    private int _displayMode;

    // Coefficients-fractal pair (0-based indexes into a1..a12); -1 = none (panel disabled).
    private int _coeffI = -1;
    private int _coeffJ = -1;

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
        _escape = new ComputeProgram(EmbeddedShaderSource.Load("escape.comp"), "escape");
        _coeffProgram = new ComputeProgram(EmbeddedShaderSource.Load("coeffs.comp"), "coeffs");
        _display = new ShaderProgram(
            EmbeddedShaderSource.Load("display.vert"),
            EmbeddedShaderSource.Load("display.frag"),
            "display");

        _scatterTex = new Texture(SizedInternalFormat.Rgba8);
        _fractalTex = new Texture(SizedInternalFormat.Rgba8);
        _coeffTex = new Texture(SizedInternalFormat.Rgba8);
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

    /// <summary>Select which panel is maximized (index into the active texture list).</summary>
    public void SetDisplayMode(int mode) => _displayMode = Math.Clamp(mode, 0, ActivePanelCount - 1);

    /// <summary>Set the coefficients-fractal pair (0-based indexes); pass (-1, -1) to disable it.</summary>
    public void SetCoeffPair(int i, int j)
    {
        _coeffI = i;
        _coeffJ = j;
        _displayMode = Math.Clamp(_displayMode, 0, ActivePanelCount - 1);
    }

    // grid + escape are always active; the coefficients fractal adds a third when a pair is set.
    private int ActivePanelCount => _coeffI >= 0 ? 3 : 2;

    /// <summary>Set the map coefficients, splat radius, grid iterations, and fractal max iterations.</summary>
    public void SetMap(QuadraticMap map, int splatRadius, int iterations, int maxIterations)
    {
        _map = map;
        _splatRadius = Math.Max(0, splatRadius);
        _iterations = Math.Max(1, iterations);
        _maxIterations = Math.Max(1, maxIterations);
    }

    /// <summary>Set the shared view directly (world center + half-height). Pure CPU.</summary>
    public void SetView(Vector2 center, float halfHeight)
    {
        _viewCenter = center;
        _viewHalfHeight = Math.Clamp(halfHeight, MinHalfHeight, MaxHalfHeight);
    }

    /// <summary>Map a client pixel (top-left origin) to a world point under the current view.</summary>
    public Vector2 ScreenToWorld(double px, double py, int w, int h)
        => _viewCenter + OffsetFromCenter(px, py, w, h);

    /// <summary>Pan so the given world point stays under the cursor pixel (drag gesture).</summary>
    public void PanToKeepWorldAtPixel(Vector2 grabWorld, double px, double py, int w, int h)
        => _viewCenter = grabWorld - OffsetFromCenter(px, py, w, h);

    /// <summary>Zoom about the cursor: factor &gt; 1 zooms in (smaller plane section).</summary>
    public void ZoomAt(double px, double py, int w, int h, float factor)
    {
        Vector2 wc = ScreenToWorld(px, py, w, h);
        _viewHalfHeight = Math.Clamp(_viewHalfHeight / factor, MinHalfHeight, MaxHalfHeight);
        _viewCenter = wc - OffsetFromCenter(px, py, w, h); // keep the cursor's world point fixed
    }

    // World offset from the view center for a client pixel. Y is flipped (py=0 top -> +halfHeight).
    private Vector2 OffsetFromCenter(double px, double py, int w, int h)
    {
        float halfW = _viewHalfHeight * ((float)w / h);
        float fx = (float)(2.0 * px / w - 1.0);
        float fy = (float)(1.0 - 2.0 * py / h);
        return new Vector2(fx * halfW, fy * _viewHalfHeight);
    }

    public void Resize(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
        _scatterTex.Allocate(_width, _height);
        // Both textures are full-resolution; either can be maximized or shown as the inset
        // (the inset viewport downscales via the sampler), so the toggle needs no reallocation.
        _fractalTex.Allocate(_width, _height);
        _coeffTex.Allocate(_width, _height);
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

        // 1) Push current state into the config UBO. The shared view rectangle is derived from
        //    center + half-height + panel aspect so pixels stay square; both passes read it.
        float halfW = _viewHalfHeight * ((float)_width / _height);
        var viewRect = new Vector4(
            _viewCenter.X - halfW, _viewCenter.Y - _viewHalfHeight,
            _viewCenter.X + halfW, _viewCenter.Y + _viewHalfHeight);

        var (cA, cB, cC) = _map.ToVec4();
        var cfg = new SolveConfig
        {
            CA = cA,
            CB = cB,
            CC = cC,
            View = viewRect,
            Dims = new Vector4i(_pointCount, _width, _height, _splatRadius),
            Iter = new Vector4i(_iterations, _maxIterations, _coeffI, _coeffJ),
        };
        _config.Update(ref cfg);
        _config.Bind(1);

        // 2) Grid scatter pass: clear the scatter target, then scatter the transformed points.
        GL.ClearTexImage(_scatterTex.Handle, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        _solve.Use();
        _scatterTex.BindImage(0, TextureAccess.WriteOnly);
        _seeds.Bind(0);
        _solve.Dispatch((_pointCount + 255) / 256, 1, 1);

        // 3) Escape-time fractal pass (filled Julia set), one invocation per pixel. No clear needed.
        _escape.Use();
        _fractalTex.BindImage(1, TextureAccess.WriteOnly);
        _escape.Dispatch((_fractalTex.Width + 7) / 8, (_fractalTex.Height + 7) / 8, 1);

        // 4) Coefficients (parameter-space) fractal pass - only when a coefficient pair is selected.
        if (_coeffI >= 0)
        {
            _coeffProgram.Use();
            _coeffTex.BindImage(2, TextureAccess.WriteOnly);
            _coeffProgram.Dispatch((_coeffTex.Width + 7) / 8, (_coeffTex.Height + 7) / 8, 1);
        }

        // One barrier covers all image writes before the display pass samples the textures.
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit |
                         MemoryBarrierFlags.TextureFetchBarrierBit);

        // 5) Display pass - composite the active panels onto the offscreen FBO.
        _fbo.Bind();
        GL.ClearColor(0f, 0f, 0f, 1f);
        GL.Viewport(0, 0, _width, _height);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // Active textures in fixed order; coefficients fractal only when a pair is selected.
        Texture[] active = _coeffI >= 0
            ? new[] { _scatterTex, _fractalTex, _coeffTex }
            : new[] { _scatterTex, _fractalTex };
        int mode = Math.Clamp(_displayMode, 0, active.Length - 1);

        // Maximized panel fills the window; the remaining ones go to the lower-left then lower-right
        // insets (GL origin = bottom-left). Mode 0 => grid full, escape lower-left, coeffs lower-right.
        DrawPanel(active[mode], 0, 0, _width, _height);
        int iw = _width / 3, ih = _height / 3, slot = 0;
        for (int k = 0; k < active.Length; k++)
        {
            if (k == mode)
                continue;
            int x = slot == 0 ? 0 : _width - iw; // slot 0 = lower-left, slot 1 = lower-right
            DrawPanel(active[k], x, 0, iw, ih);
            slot++;
        }

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    /// <summary>Draw a texture into a sub-rectangle of the bound framebuffer (one display panel).</summary>
    private void DrawPanel(Texture tex, int x, int y, int w, int h)
    {
        GL.Viewport(x, y, Math.Max(1, w), Math.Max(1, h));
        _display.Use();
        tex.Bind(0);
        _display.SetInt("srcTex", 0);
        _quad.Draw();
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
        _fractalTex?.Dispose();
        _coeffTex?.Dispose();
        _display?.Dispose();
        _escape?.Dispose();
        _coeffProgram?.Dispose();
        _solve?.Dispose();
    }
}
