using System.Collections.ObjectModel;
using OpenTK.Mathematics;
using TotalFractal.Model;

namespace TotalFractal.ViewModels;

/// <summary>
/// Single source of truth for the shader parameters exposed in the config window. Knows
/// nothing about OpenGL - the GL-aware <c>MainWindow</c> subscribes to <see cref="Changed"/>
/// and pushes the values into the renderer. More parameter groups (view, splat, iterations)
/// will be added here as their own properties later.
/// </summary>
public sealed class ParametersViewModel
{
    /// <summary>The 12 quadratic-map coefficients a1..a12, each ranged [-2, 2].</summary>
    public ObservableCollection<SliderParam> Coefficients { get; }

    /// <summary>Raised whenever any parameter changes.</summary>
    public event Action? Changed;

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
            p.Changed += () => Changed?.Invoke();
            Coefficients.Add(p);
        }
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

    /// <summary>World-space view rectangle (xmin, ymin, xmax, ymax). Fixed for now.</summary>
    public Vector4 View => new(-2.5f, -2.5f, 2.5f, 2.5f);

    /// <summary>Splat radius in pixels. Fixed for now.</summary>
    public int SplatRadius => 1;
}
