using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NelderimLauncher.Views;

public partial class OptionsDialog : Window
{
    public OptionsDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}