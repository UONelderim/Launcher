using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NelderimLauncher.Views;

public partial class UpdateDialog : Window
{
    public UpdateDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}