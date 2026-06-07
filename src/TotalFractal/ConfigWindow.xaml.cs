using System.Windows;

namespace TotalFractal;

/// <summary>
/// The config window: 12 coefficient sliders (and future parameter controls). It has no GL
/// knowledge - its DataContext is a <see cref="ViewModels.ParametersViewModel"/> assigned by
/// <c>App</c>, and bindings drive the renderer through that view model.
/// </summary>
public partial class ConfigWindow : Window
{
    public ConfigWindow() => InitializeComponent();
}
