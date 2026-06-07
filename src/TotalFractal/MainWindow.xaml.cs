using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
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

    public MainWindow(ParametersViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        _vm.Changed += ApplyParamsToRenderer;
    }

    /// <summary>
    /// Push the current view-model parameters into the renderer and request a repaint.
    /// Runs on the UI thread; only mutates CPU state - the GL upload happens in Paint.
    /// </summary>
    private void ApplyParamsToRenderer()
    {
        if (!_ready)
            return; // GL not initialized yet; GlControl_Load performs the initial sync.

        _renderer.SetMap(_vm.ToMap(), _vm.View, _vm.SplatRadius);
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

        placeholder.Children.Add(new WindowsFormsHost { Child = _glControl });
    }

    private void GlControl_Load(object? sender, EventArgs e)
    {
        _glControl!.MakeCurrent();
        _renderer.Initialize();
        _renderer.Resize(_glControl.Width, _glControl.Height);
        _ready = true;
        ApplyParamsToRenderer(); // sync renderer to the view-model's initial values, then repaint
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
