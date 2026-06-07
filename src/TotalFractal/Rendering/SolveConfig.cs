using System.Runtime.InteropServices;
using OpenTK.Mathematics;

namespace TotalFractal.Rendering;

/// <summary>
/// CPU mirror of the config UBO (std140, binding = 1). Field order and sizes MUST match the
/// uniform block in solve.comp exactly: 4x vec4 + 1x ivec4 = 80 bytes. std140 places each
/// vec4/ivec4 on a 16-byte boundary, which Sequential layout reproduces here.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct SolveConfig
{
    public Vector4 CA;    // a1 a2 a3 a4
    public Vector4 CB;    // a5 a6 a7 a8
    public Vector4 CC;    // a9 a10 a11 a12
    public Vector4 View;  // xmin ymin xmax ymax
    public Vector4i Dims; // pointCount, texW, texH, splatRadius
}
