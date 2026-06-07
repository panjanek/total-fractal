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
    /// <summary>The 12 quadratic-map coefficients a1..a12, each ranged [-2, 2].</summary>
    public ObservableCollection<SliderParam> Coefficients { get; }

    /// <summary>Splat size: 1 = single pixel, 2, 3. Maps to shader radius = size - 1.</summary>
    public ChoiceParam SplatSize { get; } =
        new() { Label = "Splat size", Options = new[] { 1, 2, 3 }, Value = 2 };

    /// <summary>Number of grid lines per axis.</summary>
    public ChoiceParam AxisCount { get; } =
        new() { Label = "Axis count", Options = new[] { 5, 10, 20, 30, 50, 100 }, Value = 20 };

    /// <summary>Sample points along each grid line.</summary>
    public ChoiceParam PointsPerAxis { get; } =
        new() { Label = "Points per axis", Options = new[] { 100, 1000, 10000 }, Value = 1000 };

    /// <summary>How many times the map is applied to each seed before its final point is drawn.</summary>
    public SliderParam Iterations { get; } =
        new() { Label = "Iterations", Min = 1, Max = 100, Value = 1 };

    /// <summary>Which panel is maximized: 0 = grid, 1 = escape fractal. Toggled by the button.</summary>
    public int DisplayModeIndex { get; private set; }

    /// <summary>Toggles <see cref="DisplayModeIndex"/> between the two display modes.</summary>
    public ICommand ToggleDisplayCommand { get; }

    /// <summary>Cheap update: coefficients or splat changed (UBO only, no buffer rebuild).</summary>
    public event Action? MapChanged;

    /// <summary>Display-only update: which panel is maximized changed (no recompute).</summary>
    public event Action? DisplayChanged;

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
            var p = new SliderParam { Label = $"a{i + 1}", Value = init[i] };
            p.Changed += () => MapChanged?.Invoke();
            Coefficients.Add(p);
        }

        SplatSize.Changed += () => MapChanged?.Invoke();
        Iterations.Changed += () => MapChanged?.Invoke();
        AxisCount.Changed += () => SeedsChanged?.Invoke();
        PointsPerAxis.Changed += () => SeedsChanged?.Invoke();

        ToggleDisplayCommand = new RelayCommand(_ =>
        {
            DisplayModeIndex ^= 1; // two modes for now: 0 <-> 1
            DisplayChanged?.Invoke();
        });
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
}
