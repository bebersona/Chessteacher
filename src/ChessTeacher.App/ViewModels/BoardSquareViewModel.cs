using System.Windows.Input;
using Avalonia.Media;
using ChessTeacher.App.Infrastructure;
using ChessTeacher.Core.Models;

namespace ChessTeacher.App.ViewModels;

public sealed class BoardSquareViewModel : ObservableObject
{
    private string _glyph = string.Empty;
    private IBrush _background = Brushes.Transparent;
    private IBrush _foreground = Brushes.Black;
    private string _coordinate = string.Empty;

    public BoardSquareViewModel(Square square, Action<BoardSquareViewModel> clicked)
    {
        Square = square;
        ClickCommand = new RelayCommand(() => clicked(this));
    }

    public Square Square { get; set; }
    public string Glyph { get => _glyph; set => SetProperty(ref _glyph, value); }
    public IBrush Background { get => _background; set => SetProperty(ref _background, value); }
    public IBrush Foreground { get => _foreground; set => SetProperty(ref _foreground, value); }
    public string Coordinate { get => _coordinate; set => SetProperty(ref _coordinate, value); }
    public ICommand ClickCommand { get; }
}
