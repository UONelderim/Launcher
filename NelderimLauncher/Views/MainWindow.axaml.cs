using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {
            tb.Text += "\nTest";
            tb.CaretIndex = Int32.MaxValue;
        }

        private void Scroll_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            tb.CaretIndex += (int)e.OffsetDelta.Y;
        }
    }
}