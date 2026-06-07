namespace TotalFractal.ViewModels;

/// <summary>
/// One labeled discrete choice bound to a ComboBox in the config window: a fixed set of
/// integer <see cref="Options"/> and the selected <see cref="Value"/>. The dropdown analogue
/// of <see cref="SliderParam"/>. <see cref="Changed"/> fires when the selection changes.
/// </summary>
public sealed class ChoiceParam : ObservableObject
{
    public string Label { get; init; } = "";
    public IReadOnlyList<int> Options { get; init; } = Array.Empty<int>();

    private int _value;
    public int Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
                Changed?.Invoke();
        }
    }

    /// <summary>Raised whenever <see cref="Value"/> changes (e.g. the user picks an option).</summary>
    public event Action? Changed;
}
