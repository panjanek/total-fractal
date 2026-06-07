using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TotalFractal.ViewModels;

/// <summary>Minimal INotifyPropertyChanged base for view models.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Set <paramref name="field"/> and raise PropertyChanged if it changed. Returns true if changed.</summary>
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
