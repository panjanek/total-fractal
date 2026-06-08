using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using TotalFractal.Rendering;
using TotalFractal.ViewModels;
using GLControl = OpenTK.GLControl.GLControl;
using GLControlSettings = OpenTK.GLControl.GLControlSettings;
using WindowState = System.Windows.WindowState; // disambiguate from OpenTK.Windowing.Common.WindowState

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

    // Coefficient-pick drag state: when a left-drag starts over the coefficients-fractal panel we
    // set the selected pair from the cursor instead of panning. The panel rectangle is captured at
    // mouse-down so the drag keeps mapping even if the cursor leaves the panel.
    private bool _pickingCoeff;
    private Renderer.CoeffPanelRect _coeffPanel;

    // Wheel-zoom easing state (CompositionTarget.Rendering tick).
    private bool _viewAnimating;
    private TimeSpan _lastRenderTime;

    // Fullscreen (borderless-maximized) toggle state; saved windowed values for restore.
    private bool _fullscreen;
    private WindowStyle _savedStyle;
    private WindowState _savedState;
    private ResizeMode _savedResize;

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
        _renderer.SetShowThumbnails(_vm.Thumbnails.Value);
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

        _renderer.SetMap(_vm.ToMap(), _vm.SplatRadius, _vm.IterationCount, _vm.MaxIterationsValue, _vm.PlotAll.Value, _vm.ColorMapId, _vm.IntensityValue);
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
        _glControl.KeyDown += GlControl_KeyDown;
        _glControl.PreviewKeyDown += GlControl_PreviewKeyDown;
        _glControl.MouseEnter += (_, _) => _glControl!.Focus(); // WinForms wheel + keys need focus

        placeholder.Children.Add(new WindowsFormsHost { Child = _glControl });
    }

    // --- Mouse navigation: pan (left-drag) and zoom (wheel). These only mutate the renderer's
    //     view state (pure CPU); the UBO upload happens in Paint. ---

    private void GlControl_MouseDown(object? sender, MouseEventArgs e)
    {
        if (!_ready || e.Button != MouseButtons.Left)
            return;

        // A left-drag over the coefficients-fractal panel sets the selected pair (driving the Julia
        // panel + sliders + crosshair) instead of panning.
        if (_renderer.TryPickCoeffValue(e.X, e.Y, out Vector2 picked, out _coeffPanel))
        {
            _pickingCoeff = true;
            _glControl!.Capture = true;
            ApplyCoeffPick(picked);
            return;
        }

        _panning = true;
        _glControl!.Capture = true;
        _grabWorld = _renderer.ScreenToWorld(e.X, e.Y, _glControl.Width, _glControl.Height);
    }

    private void GlControl_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_ready)
            return;

        if (_pickingCoeff)
        {
            ApplyCoeffPick(_renderer.CoeffValueFromPanel(_coeffPanel, e.X, e.Y));
            return;
        }

        if (_panning)
        {
            _renderer.PanToKeepWorldAtPixel(_grabWorld, e.X, e.Y, _glControl!.Width, _glControl.Height);
            _glControl.Invalidate();
        }
    }

    private void GlControl_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        _panning = false;
        _pickingCoeff = false;
        if (_glControl != null)
            _glControl.Capture = false;
    }

    private void GlControl_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_ready || _pickingCoeff)
            return; // ignore zoom mid-pick: it would shift the captured panel mapping under the drag

        float factor = MathF.Pow(1.15f, e.Delta / 120f); // wheel up -> zoom in
        _renderer.ZoomAt(e.X, e.Y, _glControl!.Width, _glControl.Height, factor); // updates the target
        StartViewAnimation();
    }

    private void GlControl_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Space: // cycle the maximized panel (same as the "Toggle display" button)
                _vm.ToggleDisplayCommand.Execute(null);
                e.Handled = true;
                break;
            case Keys.F: // toggle fullscreen
                ToggleFullscreen();
                e.Handled = true;
                break;
            case Keys.Escape: // exit fullscreen only
                if (_fullscreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                }
                break;
        }
    }

    // Escape is a WinForms "command key" and won't raise KeyDown on the control unless it is marked
    // as an input key here. (Space and F are ordinary input keys and arrive without this.)
    private void GlControl_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            e.IsInputKey = true;
    }

    /// <summary>Toggle borderless-maximized fullscreen for the main window (F to toggle, Esc to exit).</summary>
    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _savedStyle = WindowStyle;
            _savedState = WindowState;
            _savedResize = ResizeMode;
            WindowState = WindowState.Normal;    // so the re-maximize re-extends over the taskbar
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized; // borderless + maximized = true fullscreen
            _fullscreen = true;
        }
        else
        {
            WindowStyle = _savedStyle;
            ResizeMode = _savedResize;
            WindowState = _savedState;
            _fullscreen = false;
        }
        _glControl?.Focus(); // keep keyboard focus after the window-state change
    }

    /// <summary>
    /// Push a coefficient pick (a world point on the coefficients panel) into the view model: clamp
    /// to each slider's range and set the two selected coefficients. The TwoWay-bound sliders move
    /// and their Changed event drives the usual MapChanged -> ApplyMap repaint (updating the Julia
    /// panel and the crosshair). Always invalidates, since SetField suppresses Changed when a value
    /// is unchanged (e.g. dragging within a single pixel column).
    /// </summary>
    private void ApplyCoeffPick(Vector2 worldValue)
    {
        int i = _vm.CoeffI, j = _vm.CoeffJ;
        if (i < 0 || j < 0)
            return; // the pair may have changed to "none"

        SliderParam pi = _vm.Coefficients[i];
        SliderParam pj = _vm.Coefficients[j];
        pi.Value = Math.Clamp(worldValue.X, pi.Min, pi.Max); // X -> a[CoeffI]
        pj.Value = Math.Clamp(worldValue.Y, pj.Min, pj.Max); // Y -> a[CoeffJ]
        _glControl!.Invalidate();
    }

    // --- Zoom easing: a UI-thread per-frame tick eases the current view toward the target until
    //     it settles, then unsubscribes (zero idle cost). All CPU here; rendering stays in Paint. ---

    private void StartViewAnimation()
    {
        if (_viewAnimating)
            return;
        _viewAnimating = true;
        _lastRenderTime = TimeSpan.MinValue; // first tick seeds the timestamp (dt = 0)
        CompositionTarget.Rendering += OnViewTick;
    }

    private void StopViewAnimation()
    {
        if (!_viewAnimating)
            return;
        CompositionTarget.Rendering -= OnViewTick;
        _viewAnimating = false;
    }

    private void OnViewTick(object? sender, EventArgs e)
    {
        if (!_ready)
        {
            StopViewAnimation();
            return;
        }

        TimeSpan now = ((RenderingEventArgs)e).RenderingTime;
        double dt = _lastRenderTime == TimeSpan.MinValue ? 0.0 : (now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;
        dt = Math.Clamp(dt, 0.0, 0.1); // guard against stalls / first frame

        bool animating = _renderer.AdvanceView(dt);
        _glControl!.Invalidate();
        if (!animating)
            StopViewAnimation();
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
