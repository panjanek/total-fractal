namespace TotalFractal.ViewModels;

/// <summary>
/// One labeled, bounded scalar bound to a slider in the config window. The data template
/// renders <see cref="Label"/> + a slider over [<see cref="Min"/>, <see cref="Max"/>] +
/// the live <see cref="Value"/>. <see cref="Changed"/> fires on every move.
/// </summary>
public sealed class SliderParam : ObservableObject
{
    public string Label { get; init; } = "";
    public double Min { get; init; } = -2.0;
    public double Max { get; init; } = 2.0;

    private double _value;
    public double Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
                Changed?.Invoke();
        }
    }

    /// <summary>Raised whenever <see cref="Value"/> changes (e.g. the user drags the slider).</summary>
    public event Action? Changed;
}
