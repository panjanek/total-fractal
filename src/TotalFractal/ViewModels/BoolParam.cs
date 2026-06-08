namespace TotalFractal.ViewModels;

/// <summary>
/// One labeled boolean bound to a CheckBox in the config window: the on/off analogue of
/// <see cref="ChoiceParam"/>. <see cref="Changed"/> fires when the value is toggled.
/// </summary>
public sealed class BoolParam : ObservableObject
{
    public string Label { get; init; } = "";

    private bool _value;
    public bool Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
                Changed?.Invoke();
        }
    }

    /// <summary>Raised whenever <see cref="Value"/> changes (e.g. the user toggles the checkbox).</summary>
    public event Action? Changed;
}
