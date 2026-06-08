namespace TotalFractal.ViewModels;

/// <summary>
/// One named entry in the fractal "Color map" dropdown: a display <see cref="Label"/> plus the
/// shader palette <see cref="Id"/> passed through the config UBO. The colour-map analogue of
/// <see cref="CoeffPair"/>.
/// </summary>
public sealed class ColorMap
{
    public string Label { get; init; } = "";
    public int Id { get; init; }
}
