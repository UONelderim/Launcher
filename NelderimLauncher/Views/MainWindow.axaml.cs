using Avalonia.Controls;
using NelderimLauncher.ViewModels;

namespace NelderimLauncher.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }
    }
}