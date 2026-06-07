using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using TotalFractal.Rendering;
using TotalFractal.ViewModels;
using GLControl = OpenTK.GLControl.GLControl;
using GLControlSettings = OpenTK.GLControl.GLControlSettings;

namespace TotalFractal;

/// <summary>
/// Interaction logic for MainWindow.xaml. Kept intentionally thin: it hosts the GLControl,
/// forwards the context lifecycle (Load/Resize/Paint) to <see cref="Renderer"/>, and is the
/// GL-aware coordinator that pushes <see cref="ParametersViewModel"/> changes into the
/// renderer. All OpenGL work lives in the renderer.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Renderer _renderer = new();
    private readonly ParametersViewModel _vm;
    private GLControl? _glControl;
    private bool _ready;

    // Mouse-drag (pan) state.
    private bool _panning;
    private Vector2 _grabWorld;

    // Source domain of the seed grid (world space); fixed - only the counts change.
    private static readonly Vector2 SeedMin = new(-1f, -1f);
    private static readonly Vector2 SeedMax = new(1f, 1f);

    public MainWindow(ParametersViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.MapChanged += ApplyMap;       // coefficients / splat: cheap UBO-only update
        _vm.SeedsChanged += ApplySeeds;   // axis count / points per axis: seed buffer rebuild
        _vm.DisplayChanged += ApplyDisplay; // which panel is maximized: display-only
        _vm.CoeffPairChanged += ApplyCoeffPair; // coefficients-fractal pair selection
    }

    /// <summary>Push the selected coefficients-fractal pair (and any clamped display mode), then repaint.</summary>
    private void ApplyCoeffPair()
    {
        if (!_ready)
            return;

        _renderer.SetCoeffPair(_vm.CoeffI, _vm.CoeffJ);
        _renderer.SetDisplayMode(_vm.DisplayModeIndex); // mode may have been clamped on switch to "none"
        _glControl!.Invalidate();
    }

    /// <summary>Push the selected display mode (which panel is maximized) and repaint.</summary>
    private void ApplyDisplay()
    {
        if (!_ready)
            return;

        _renderer.SetDisplayMode(_vm.DisplayModeIndex);
        _glControl!.Invalidate();
    }

    /// <summary>
    /// Push the coefficients + splat into the renderer and request a repaint. Runs on the UI
    /// thread; only mutates CPU state - the GL upload happens in Paint.
    /// </summary>
    private void ApplyMap()
    {
        if (!_ready)
            return; // GL not initialized yet; GlControl_Load performs the initial sync.

        _renderer.SetMap(_vm.ToMap(), _vm.SplatRadius, _vm.IterationCount, _vm.MaxIterationsValue);
        _glControl!.Invalidate();
    }

    /// <summary>
    /// Request a seed-grid rebuild for the current axis count / points per axis, then repaint.
    /// SetSeeds is pure-CPU (deferred upload), so this is safe from a UI event handler.
    /// </summary>
    private void ApplySeeds()
    {
        if (!_ready)
            return;

        _renderer.SetSeeds(_vm.LinesPerAxis, _vm.SamplesPerLine, SeedMin, SeedMax);
        _glControl!.Invalidate();
    }

    private void parent_Loaded(object sender, RoutedEventArgs e)
    {
        if (_glControl != null)
            return; // Loaded can fire more than once; build the control only once.

        var settings = new GLControlSettings
        {
            API = ContextAPI.OpenGL,
            APIVersion = new Version(4, 5),     // 4.3+ for compute, 4.4+ for glClearTexImage
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible | ContextFlags.Debug,
        };

        _glControl = new GLControl(settings) { Dock = DockStyle.Fill };
        _glControl.Load += GlControl_Load;
        _glControl.Resize += GlControl_Resize;
        _glControl.Paint += GlControl_Paint;
        _glControl.MouseDown += GlControl_MouseDown;
        _glControl.MouseMove += GlControl_MouseMove;
        _glControl.MouseUp += GlControl_MouseUp;
        _glControl.MouseWheel += GlControl_MouseWheel;
        _glControl.MouseEnter += (_, _) => _glControl!.Focus(); // WinForms wheel needs focus

        placeholder.Children.Add(new WindowsFormsHost { Child = _glControl });
    }

    // --- Mouse navigation: pan (left-drag) and zoom (wheel). These only mutate the renderer's
    //     view state (pure CPU); the UBO upload happens in Paint. ---

    private void GlControl_MouseDown(object? sender, MouseEventArgs e)
    {
        if (!_ready || e.Button != MouseButtons.Left)
            return;

        _panning = true;
        _glControl!.Capture = true;
        _grabWorld = _renderer.ScreenToWorld(e.X, e.Y, _glControl.Width, _glControl.Height);
    }

    private void GlControl_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_ready || !_panning)
            return;

        _renderer.PanToKeepWorldAtPixel(_grabWorld, e.X, e.Y, _glControl!.Width, _glControl.Height);
        _glControl.Invalidate();
    }

    private void GlControl_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _panning = false;
        if (_glControl != null)
            _glControl.Capture = false;
    }

    private void GlControl_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_ready)
            return;

        float factor = MathF.Pow(1.15f, e.Delta / 120f); // wheel up -> zoom in
        _renderer.ZoomAt(e.X, e.Y, _glControl!.Width, _glControl.Height, factor);
        _glControl.Invalidate();
    }

    private void GlControl_Load(object? sender, EventArgs e)
    {
        _glControl!.MakeCurrent();
        _renderer.Initialize();
        _renderer.Resize(_glControl.Width, _glControl.Height);
        _ready = true;
        ApplySeeds(); // sync renderer to the view-model's initial values, then repaint
        ApplyMap();
        ApplyCoeffPair();
        ApplyDisplay();
    }

    private void GlControl_Resize(object? sender, EventArgs e)
    {
        if (!_ready)
            return; // Resize can fire before Load; the initial size is set in GlControl_Load.

        _glControl!.MakeCurrent();
        _renderer.Resize(_glControl.Width, _glControl.Height);
        _glControl.Invalidate();
    }

    private void GlControl_Paint(object? sender, PaintEventArgs e)
    {
        if (!_ready)
            return;

        _glControl!.MakeCurrent();
        _renderer.RenderFrame();
        _renderer.BlitToDefault(_glControl.Width, _glControl.Height);
        _glControl.SwapBuffers();
    }
}
