using Avalonia.Controls;
using ChessTeacher.App.ViewModels;

namespace ChessTeacher.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
