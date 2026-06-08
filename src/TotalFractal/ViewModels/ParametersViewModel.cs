using System.Collections.ObjectModel;
using System.Windows.Input;
using TotalFractal.Model;

namespace TotalFractal.ViewModels;

/// <summary>
/// Single source of truth for the shader parameters exposed in the config window. Knows
/// nothing about OpenGL - the GL-aware <c>MainWindow</c> subscribes to the change events and
/// pushes the values into the renderer.
///
/// Changes are split by cost: <see cref="MapChanged"/> (coefficients + splat) is a cheap
/// UBO-only update, while <see cref="SeedsChanged"/> (axis count / points per axis) requires
/// rebuilding the seed buffer - so a coefficient drag never triggers a buffer rebuild.
/// </summary>
public sealed class ParametersViewModel
{
    /// <summary>The 12 quadratic-map coefficients a1..a12, each ranged [-5, 5].</summary>
    public ObservableCollection<SliderParam> Coefficients { get; }

    /// <summary>Splat size: 1 = single pixel, 2, 3. Maps to shader radius = size - 1.</summary>
    public ChoiceParam SplatSize { get; } =
        new() { Label = "Splat size", Options = new[] { 1, 2, 3 }, Value = 1 };

    /// <summary>Number of grid lines per axis.</summary>
    public ChoiceParam AxisCount { get; } =
        new() { Label = "Axis count", Options = new[] { 5, 10, 20, 30, 50, 100 }, Value = 20 };

    /// <summary>Sample points along each grid line.</summary>
    public ChoiceParam PointsPerAxis { get; } =
        new() { Label = "Points per axis", Options = new[] { 1, 10, 100, 1000, 10000, 50000 }, Value = 1000 };

    /// <summary>How many times the map is applied to each seed before its final point is drawn.</summary>
    public SliderParam Iterations { get; } =
        new() { Label = "Iterations", Min = 1, Max = 1000000, Value = 1 };

    /// <summary>When true, the grid pass plots every iteration step (the orbit), not just the final point.</summary>
    public BoolParam PlotAll { get; } =
        new() { Label = "Plot all iteration steps", Value = false };

    /// <summary>Per-visit grey level [0,1] for the accumulating grid plot; brighter where texels are revisited.</summary>
    public SliderParam Intensity { get; } =
        new() { Label = "Intensity", Min = 0, Max = 1, Value = 0.5 };

    /// <summary>Escape-time iteration cap for the escape and coefficients fractals.</summary>
    public ChoiceParam MaxIterations { get; } =
        new() { Label = "Max iterations", Options = new[] { 100, 200, 300, 500, 1000, 2000, 3000, 5000, 10000 }, Value = 200 };

    /// <summary>When checked, the lower-left/right inset thumbnail panels are shown (display-only).</summary>
    public BoolParam Thumbnails { get; } =
        new() { Label = "Thumbnails", Value = true };

    /// <summary>Which panel is maximized: 0 = grid, 1 = escape fractal, 2 = coefficients fractal.</summary>
    public int DisplayModeIndex { get; private set; }

    /// <summary>Cycles <see cref="DisplayModeIndex"/> through the active display modes.</summary>
    public ICommand ToggleDisplayCommand { get; }

    /// <summary>"none" + the 66 coefficient pairs (a1-a2 .. a11-a12) for the parameter-space fractal.</summary>
    public IReadOnlyList<CoeffPair> CoeffPairs { get; }

    private CoeffPair _selectedCoeffPair;
    /// <summary>Selected coefficient pair; "none" disables the coefficients fractal panel.</summary>
    public CoeffPair SelectedCoeffPair
    {
        get => _selectedCoeffPair;
        set
        {
            if (ReferenceEquals(_selectedCoeffPair, value) || value is null)
                return;
            _selectedCoeffPair = value;
            UpdateCoeffHighlights();
            // Switching to "none" may drop the active mode count; keep the index in range.
            if (DisplayModeIndex >= ActiveModeCount)
                DisplayModeIndex = ActiveModeCount - 1;
            CoeffPairChanged?.Invoke();
        }
    }

    /// <summary>Selected coefficient indexes (0-based); -1 when "none".</summary>
    public int CoeffI => SelectedCoeffPair.I;
    public int CoeffJ => SelectedCoeffPair.J;

    /// <summary>Number of active display modes: 2 (grid, escape) or 3 when a coeff pair is selected.</summary>
    public int ActiveModeCount => SelectedCoeffPair.IsNone ? 2 : 3;

    /// <summary>The fractal colour maps for the "Color map" dropdown (Id is passed to the shaders).</summary>
    public IReadOnlyList<ColorMap> ColorMaps { get; }

    private ColorMap _selectedColorMap;
    /// <summary>Selected fractal colour map; recolours both fractal panels (cheap UBO update).</summary>
    public ColorMap SelectedColorMap
    {
        get => _selectedColorMap;
        set
        {
            if (ReferenceEquals(_selectedColorMap, value) || value is null)
                return;
            _selectedColorMap = value;
            MapChanged?.Invoke();
        }
    }

    /// <summary>Selected colour map id (0 = gradient .. 4 = classic).</summary>
    public int ColorMapId => SelectedColorMap.Id;

    /// <summary>Cheap update: coefficients or splat changed (UBO only, no buffer rebuild).</summary>
    public event Action? MapChanged;

    /// <summary>Display-only update: which panel is maximized changed (no recompute).</summary>
    public event Action? DisplayChanged;

    /// <summary>The coefficients-fractal pair selection changed (toggles/redraws the third panel).</summary>
    public event Action? CoeffPairChanged;

    /// <summary>Expensive update: axis count or points per axis changed (rebuild seed buffer).</summary>
    public event Action? SeedsChanged;

    public ParametersViewModel()
    {
        QuadraticMap e = QuadraticMap.Example; // initial slider positions match the initial render
        float[] init =
        {
            e.A1, e.A2, e.A3, e.A4, e.A5, e.A6,
            e.A7, e.A8, e.A9, e.A10, e.A11, e.A12,
        };

        Coefficients = new ObservableCollection<SliderParam>();
        for (int i = 0; i < 12; i++)
        {
            var p = new SliderParam { Label = $"a{i + 1}", Min = -5.0, Max = 5.0, Value = init[i] };
            p.Changed += () => MapChanged?.Invoke();
            Coefficients.Add(p);
        }

        SplatSize.Changed += () => MapChanged?.Invoke();
        Iterations.Changed += () => MapChanged?.Invoke();
        PlotAll.Changed += () => MapChanged?.Invoke();
        Intensity.Changed += () => MapChanged?.Invoke();
        MaxIterations.Changed += () => MapChanged?.Invoke();
        AxisCount.Changed += () => SeedsChanged?.Invoke();
        PointsPerAxis.Changed += () => SeedsChanged?.Invoke();
        Thumbnails.Changed += () => DisplayChanged?.Invoke();

        // "none" + the 66 unordered coefficient pairs.
        var pairs = new List<CoeffPair> { new() { Label = "none", I = -1, J = -1 } };
        for (int i = 0; i < 12; i++)
            for (int j = i + 1; j < 12; j++)
                pairs.Add(new CoeffPair { Label = $"a{i + 1}-a{j + 1}", I = i, J = j });
        CoeffPairs = pairs;
        // Default to a1-a7 so the coefficients (Mandelbrot) fractal panel is visible on startup.
        _selectedCoeffPair = pairs.First(p => p.I == 0 && p.J == 6);
        UpdateCoeffHighlights();

        ColorMaps = new List<ColorMap>
        {
            new() { Label = "Gradient",   Id = 0 },
            new() { Label = "Monochrome", Id = 1 },
            new() { Label = "Viridis",    Id = 2 },
            new() { Label = "Magma",      Id = 3 },
            new() { Label = "Classic",    Id = 4 },
        };
        _selectedColorMap = ColorMaps[0]; // Gradient (default)

        ToggleDisplayCommand = new RelayCommand(_ =>
        {
            DisplayModeIndex = (DisplayModeIndex + 1) % ActiveModeCount; // 2 or 3 modes
            DisplayChanged?.Invoke();
        });
    }

    /// <summary>Highlight the two coefficients of the selected pair (none -> clears all highlights).</summary>
    private void UpdateCoeffHighlights()
    {
        for (int k = 0; k < Coefficients.Count; k++)
            Coefficients[k].IsHighlighted = (k == CoeffI || k == CoeffJ);
    }

    /// <summary>Pack the 12 slider values (cast to float) into a <see cref="QuadraticMap"/>.</summary>
    public QuadraticMap ToMap()
    {
        var c = Coefficients;
        return new QuadraticMap
        {
            A1 = (float)c[0].Value, A2 = (float)c[1].Value, A3 = (float)c[2].Value,
            A4 = (float)c[3].Value, A5 = (float)c[4].Value, A6 = (float)c[5].Value,
            A7 = (float)c[6].Value, A8 = (float)c[7].Value, A9 = (float)c[8].Value,
            A10 = (float)c[9].Value, A11 = (float)c[10].Value, A12 = (float)c[11].Value,
        };
    }

    /// <summary>Shader splat radius (0 = single pixel), derived from the splat size dropdown.</summary>
    public int SplatRadius => SplatSize.Value - 1;

    /// <summary>Grid lines per axis (drives the seed buffer).</summary>
    public int LinesPerAxis => AxisCount.Value;

    /// <summary>Sample points per grid line (drives the seed buffer).</summary>
    public int SamplesPerLine => PointsPerAxis.Value;

    /// <summary>Iteration count (integer; the slider snaps to whole numbers).</summary>
    public int IterationCount => (int)Math.Round(Iterations.Value);

    /// <summary>Escape-time iteration cap for the fractal panels.</summary>
    public int MaxIterationsValue => MaxIterations.Value;

    /// <summary>Per-visit grey level [0,1] for the accumulating grid plot.</summary>
    public float IntensityValue => (float)Intensity.Value;
}
