using OpenTK.Mathematics;

namespace TotalFractal.Model;

/// <summary>
/// The 12 coefficients of the general 2D quadratic map (Sprott form):
///   x' = a1 + a2 x + a3 y + a4 x^2 + a5 x y + a6 y^2
///   y' = a7 + a8 x + a9 y + a10 x^2 + a11 x y + a12 y^2
/// </summary>
public struct QuadraticMap
{
    public float A1, A2, A3, A4, A5, A6;
    public float A7, A8, A9, A10, A11, A12;

    /// <summary>x' = x, y' = y. Plumbing sanity check: the grid stays a straight grid.</summary>
    public static QuadraticMap Identity => new() { A2 = 1f, A9 = 1f };

    /// <summary>
    /// The complex squaring map z' = z^2 + c (z = x + iy, c = a1 + i*a7):
    ///   x' = x^2 - y^2 + a1,  y' = 2 x y + a7  -> a4 = 1, a6 = -1, a11 = 2, rest 0.
    /// Default map: the escape panel renders the exact Julia set for c = (a1, a7), and selecting
    /// the a1-a7 coefficient pair renders the exact classic Mandelbrot set (seed z0 = 0).
    /// </summary>
    public static QuadraticMap Example => new()
    {
        A1 = 0f, A2 = 0f, A3 = 0f, A4 = 1f, A5 = 0f, A6 = -1f,
        A7 = 0f, A8 = 0f, A9 = 0f, A10 = 0f, A11 = 2f, A12 = 0f,
    };

    /// <summary>Pack into the three UBO vec4 slots: (a1..a4), (a5..a8), (a9..a12).</summary>
    public readonly (Vector4 cA, Vector4 cB, Vector4 cC) ToVec4()
        => (new Vector4(A1, A2, A3, A4),
            new Vector4(A5, A6, A7, A8),
            new Vector4(A9, A10, A11, A12));

    /// <summary>Read a coefficient a(i+1) by 0-based index (0 = a1 .. 11 = a12).</summary>
    public readonly float this[int i] => i switch
    {
        0 => A1, 1 => A2, 2 => A3, 3 => A4, 4 => A5, 5 => A6,
        6 => A7, 7 => A8, 8 => A9, 9 => A10, 10 => A11, 11 => A12,
        _ => throw new ArgumentOutOfRangeException(nameof(i)),
    };
}
