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

    /// <summary>A gentle non-linear test map that visibly bends the grid without blowing up.</summary>
    public static QuadraticMap Example => new()
    {
        A1 = 0.0f, A2 = 0.9f, A3 = -0.4f, A4 = 0.3f, A5 = 0.2f, A6 = -0.3f,
        A7 = 0.0f, A8 = 0.4f, A9 = 0.9f, A10 = -0.2f, A11 = 0.3f, A12 = 0.2f,
    };

    /// <summary>Pack into the three UBO vec4 slots: (a1..a4), (a5..a8), (a9..a12).</summary>
    public readonly (Vector4 cA, Vector4 cB, Vector4 cC) ToVec4()
        => (new Vector4(A1, A2, A3, A4),
            new Vector4(A5, A6, A7, A8),
            new Vector4(A9, A10, A11, A12));
}
