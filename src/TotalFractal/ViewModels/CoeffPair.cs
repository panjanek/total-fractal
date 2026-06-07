namespace TotalFractal.ViewModels;

/// <summary>
/// A selectable pair of coefficient indexes for the coefficients (parameter-space) fractal.
/// <see cref="I"/> = -1 marks the "none" option (fractal disabled). Indexes are 0-based
/// (0 = a1 .. 11 = a12); <see cref="I"/> &lt; <see cref="J"/>.
/// </summary>
public sealed class CoeffPair
{
    public string Label { get; init; } = "";
    public int I { get; init; } = -1;
    public int J { get; init; } = -1;

    public bool IsNone => I < 0;
}
