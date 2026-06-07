using OpenTK.Mathematics;

namespace TotalFractal.Model;

/// <summary>
/// Generates seed points for the grid-deformation view: a mesh of horizontal and vertical
/// lines, each densely sampled so that after transformation the points read as continuous
/// curves rather than scattered dots. Total points = linesPerAxis * samplesPerLine * 2.
/// </summary>
public static class GridSeeds
{
    public static Vector2[] Generate(int linesPerAxis, int samplesPerLine, Vector2 min, Vector2 max)
    {
        var points = new Vector2[linesPerAxis * samplesPerLine * 2];
        int k = 0;

        // Horizontal lines: constant y, x sweeps min.X -> max.X.
        for (int j = 0; j < linesPerAxis; j++)
        {
            float y = Lerp(min.Y, max.Y, Frac(j, linesPerAxis));
            for (int s = 0; s < samplesPerLine; s++)
                points[k++] = new Vector2(Lerp(min.X, max.X, Frac(s, samplesPerLine)), y);
        }

        // Vertical lines: constant x, y sweeps min.Y -> max.Y.
        for (int j = 0; j < linesPerAxis; j++)
        {
            float x = Lerp(min.X, max.X, Frac(j, linesPerAxis));
            for (int s = 0; s < samplesPerLine; s++)
                points[k++] = new Vector2(x, Lerp(min.Y, max.Y, Frac(s, samplesPerLine)));
        }

        return points;
    }

    private static float Frac(int i, int count) => count <= 1 ? 0f : (float)i / (count - 1);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
