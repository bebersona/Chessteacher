using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ChessTeacher.App.Infrastructure;
using ChessTeacher.Core;
using ChessTeacher.Core.Models;
using ChessTeacher.Data;
using ChessTeacher.Engine;
using ChessTeacher.Teaching;

namespace ChessTeacher.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private static readonly IBrush LightSquare = new SolidColorBrush(Color.Parse("#D8E1C4"));
    private static readonly IBrush DarkSquare = new SolidColorBrush(Color.Parse("#67855B"));
    private static readonly IBrush SelectedSquare = new SolidColorBrush(Color.Parse("#E7C95A"));
    private static readonly IBrush LegalSquare = new SolidColorBrush(Color.Parse("#83B979"));
    private static readonly IBrush LastMoveSquare = new SolidColorBrush(Color.Parse("#B4C36A"));
    private static readonly IBrush CheckSquare = new SolidColorBrush(Color.Parse("#D96565"));

    private readonly IStockfishService _engine;
    private readonly IGameRepository _repository;
    private readonly ISettingsService _settingsService;
    private readonly IExplanationService _explanations;
    private readonly PgnService _pgn = new();
    private readonly ChessGame _game = new();
    private CancellationTokenSource? _analysisCancellation;
    private AppSettings _settings = new();
    private Square? _selected;
    private ChessMove? _lastMove;
    private bool _flipped;
    private bool _isBusy;
    private bool _playAgainstComputer = true;
    private string _status = "White to move";
    private string _feedback = "Select a piece, then select a highlighted destination.";
    private string _evaluation = "0.00";
    private string _bestLine = "Engine idle";
    private string _engineStatus = "Stockfish starts only when requested.";
    private string _currentScreen = "Play";

    public MainWindowViewModel(
        IStockfishService engine,
        IGameRepository repository,
        ISettingsService settingsService,
        IExplanationService explanations)
    {
        _engine = engine;
        _repository = repository;
        _settingsService = settingsService;
        _explanations = explanations;

        Squares = new ObservableCollection<BoardSquareViewModel>(
            Enumerable.Range(0, 64).Select(i =>
                new BoardSquareViewModel(Square.FromIndex(i), OnSquareClicked)));

        MoveHistory = new ObservableCollection<string>();
        CandidateLines = new ObservableCollection<string>();
        RecentGames = new ObservableCollection<string>();

        NewGameCommand = new RelayCommand(NewGame);
        UndoCommand = new RelayCommand(Undo);
        RedoCommand = new RelayCommand(Redo);
        FlipCommand = new RelayCommand(() => { _flipped = !_flipped; RefreshBoard(); });
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusy);
        StopAnalysisCommand = new AsyncRelayCommand(StopAnalysisAsync);
        HintCommand = new AsyncRelayCommand(HintAsync, () => !IsBusy);
        ComputerMoveCommand = new AsyncRelayCommand(ComputerMoveAsync, () => !IsBusy && !_game.IsGameOver);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        ToggleComputerCommand = new RelayCommand(() =>
        {
            PlayAgainstComputer = !PlayAgainstComputer;
            Feedback = PlayAgainstComputer
                ? "Computer opponent enabled. Stockfish will play Black."
                : "Free analysis board enabled. You may play both sides.";
        });

        foreach (var name in new[] { "Home", "Play", "Analysis", "Puzzles", "Lessons", "Openings", "Progress", "Settings" })
            Navigation.Add(new NavigationItem(name, new RelayCommand(() => CurrentScreen = name)));

        _engine.AnalysisUpdated += (_, line) =>
            Dispatcher.UIThread.Post(() =>
            {
                if (line.MultiPv == 1)
                {
                    Evaluation = line.Score.ToString();
                    EngineStatus = $"Depth {line.Depth} · {line.Nps:n0} nodes/s";
                }
            });

        _ = InitializeAsync();
        RefreshBoard();
    }

    public ObservableCollection<BoardSquareViewModel> Squares { get; }
    public ObservableCollection<string> MoveHistory { get; }
    public ObservableCollection<string> CandidateLines { get; }
    public ObservableCollection<string> RecentGames { get; }
    public ObservableCollection<NavigationItem> Navigation { get; } = new();

    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string Feedback { get => _feedback; set => SetProperty(ref _feedback, value); }
    public string Evaluation { get => _evaluation; set => SetProperty(ref _evaluation, value); }
    public string BestLine { get => _bestLine; set => SetProperty(ref _bestLine, value); }
    public string EngineStatus { get => _engineStatus; set => SetProperty(ref _engineStatus, value); }
    public string CurrentScreen { get => _currentScreen; set => SetProperty(ref _currentScreen, value); }
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public bool PlayAgainstComputer { get => _playAgainstComputer; set => SetProperty(ref _playAgainstComputer, value); }

    public ICommand NewGameCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand FlipCommand { get; }
    public ICommand AnalyzeCommand { get; }
    public ICommand StopAnalysisCommand { get; }
    public ICommand HintCommand { get; }
    public ICommand ComputerMoveCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleComputerCommand { get; }

    private async Task InitializeAsync()
    {
        _settings = await _settingsService.LoadAsync();
        try
        {
            foreach (var game in await _repository.GetRecentAsync(8))
                RecentGames.Add($"{game.PlayedAt.LocalDateTime:g} · {game.WhitePlayer}–{game.BlackPlayer} · {game.Result}");
        }
        catch (Exception ex)
        {
            Feedback = "Recent games could not be loaded: " + ex.Message;
        }
        RefreshBoard();
    }

    private void OnSquareClicked(BoardSquareViewModel squareViewModel)
    {
        if (IsBusy || _game.IsGameOver) return;
        if (PlayAgainstComputer && _game.Position.SideToMove == PieceColor.Black)
        {
            Feedback = "Stockfish is thinking. Use Stop to cancel.";
            return;
        }

        var square = squareViewModel.Square;
        var piece = _game.Position[square];

        if (_selected is null)
        {
            if (piece.IsEmpty || piece.Color != _game.Position.SideToMove)
            {
                Feedback = "Choose a piece belonging to the side to move.";
                return;
            }
            _selected = square;
            Feedback = $"Selected {square}. Legal destinations are highlighted.";
            RefreshBoard();
            return;
        }

        if (square == _selected)
        {
            _selected = null;
            RefreshBoard();
            return;
        }

        var legal = _game.GenerateLegalMovesFrom(_selected.Value);
        var move = legal.FirstOrDefault(x => x.To == square);
        if (move is null)
        {
            if (!piece.IsEmpty && piece.Color == _game.Position.SideToMove)
            {
                _selected = square;
                Feedback = $"Selected {square}.";
            }
            else
            {
                _selected = null;
                Feedback = "That destination is not legal.";
            }
            RefreshBoard();
            return;
        }

        MakeMove(move, engineBased: false);
        if (PlayAgainstComputer && !_game.IsGameOver && _game.Position.SideToMove == PieceColor.Black)
            _ = ComputerMoveAsync();
    }

    private void MakeMove(ChessMove move, bool engineBased)
    {
        var before = _game.Position.Clone();
        var result = _game.TryMakeMove(move);
        if (!result.Success)
        {
            Feedback = result.Error;
            return;
        }

        _lastMove = move;
        _selected = null;
        RebuildMoveHistory();

        var classification = new ClassifiedMove(
            engineBased ? MoveClassification.Best : MoveClassification.Good,
            0,
            engineBased
                ? "The computer selected a principal engine move."
                : "The move is legal. Start analysis for an engine-based classification.");
        var themes = TacticalThemeDetector.Detect(before, _game.Position, move);
        Feedback = _explanations.Explain(before, move, null, classification, themes);
        UpdateStatus(result);
        RefreshBoard();
    }

    private void NewGame()
    {
        _analysisCancellation?.Cancel();
        _game.Reset();
        MoveHistory.Clear();
        CandidateLines.Clear();
        _selected = null;
        _lastMove = null;
        Evaluation = "0.00";
        BestLine = "Engine idle";
        EngineStatus = "New game ready. Stockfish has not been started.";
        Feedback = PlayAgainstComputer
            ? "New game started. You are White; Stockfish is Black."
            : "New free-analysis board started.";
        Status = "White to move";
        RefreshBoard();
    }

    private void Undo()
    {
        if (!_game.Undo()) { Feedback = "No move is available to undo."; return; }
        if (PlayAgainstComputer && _game.Position.SideToMove == PieceColor.Black)
            _game.Undo();
        _lastMove = _game.Moves.LastOrDefault()?.Move;
        _selected = null;
        RebuildMoveHistory();
        Status = SideText();
        Feedback = "Move undone.";
        RefreshBoard();
    }

    private void Redo()
    {
        if (!_game.Redo()) { Feedback = "No move is available to redo."; return; }
        _lastMove = _game.Moves.LastOrDefault()?.Move;
        RebuildMoveHistory();
        Status = SideText();
        Feedback = "Move restored.";
        RefreshBoard();
    }

    private async Task AnalyzeAsync()
    {
        _analysisCancellation?.Cancel();
        _analysisCancellation = new CancellationTokenSource();
        IsBusy = true;
        CandidateLines.Clear();
        EngineStatus = "Starting Stockfish…";

        try
        {
            var analysis = await _engine.AnalyzeAsync(
                FenSerializer.ToFen(_game.Position),
                null,
                _settings.Engine,
                _analysisCancellation.Token);

            CandidateLines.Clear();
            foreach (var line in analysis.Lines)
                CandidateLines.Add($"{line.MultiPv}. {line.Score} · {string.Join(' ', line.PrincipalVariation.Take(8))}");

            BestLine = analysis.Lines.FirstOrDefault() is { } best
                ? string.Join(' ', best.PrincipalVariation.Take(12))
                : analysis.BestMove;
            EngineStatus = $"Analysis complete at depth {analysis.CompletedDepth}.";
            Feedback = $"Best move: {analysis.BestMove}. Candidate lines are shown below.";
        }
        catch (OperationCanceledException)
        {
            EngineStatus = "Analysis cancelled.";
        }
        catch (Exception ex)
        {
            EngineStatus = "Engine unavailable.";
            Feedback = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task StopAnalysisAsync()
    {
        _analysisCancellation?.Cancel();
        await _engine.StopAnalysisAsync();
        IsBusy = false;
        EngineStatus = "Analysis stopped.";
    }

    private async Task HintAsync()
    {
        IsBusy = true;
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var settings = _settings.Engine with { MultiPv = 1, MoveTimeMs = 700 };
            var analysis = await _engine.AnalyzeAsync(
                FenSerializer.ToFen(_game.Position), null, settings, cancellation.Token);
            Feedback = $"Hint: consider {analysis.BestMove}. Check forcing moves, king safety, development, and loose pieces.";
            EngineStatus = "Hint ready.";
        }
        catch (Exception ex)
        {
            EngineStatus = "Hint unavailable.";
            Feedback = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task ComputerMoveAsync()
    {
        if (_game.IsGameOver) return;
        IsBusy = true;
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var settings = _settings.Engine with { MultiPv = 1 };
            var analysis = await _engine.AnalyzeAsync(
                FenSerializer.ToFen(_game.Position), null, settings, cancellation.Token);
            if (!_game.TryMakeUci(analysis.BestMove, out var result) || result.Move is null)
                throw new InvalidOperationException("Stockfish returned an invalid move.");

            _lastMove = result.Move;
            RebuildMoveHistory();
            Feedback = $"Stockfish played {result.San}.";
            UpdateStatus(result);
            RefreshBoard();
        }
        catch (Exception ex)
        {
            Feedback = ex.Message;
            EngineStatus = "Computer move failed.";
        }
        finally { IsBusy = false; }
    }

    private async Task SaveAsync()
    {
        try
        {
            var result = _game.Termination switch
            {
                GameTermination.Checkmate =>
                    _game.Position.SideToMove == PieceColor.White ? "0-1" : "1-0",
                GameTermination.None => "*",
                _ => "1/2-1/2"
            };
            var pgn = _pgn.Export(_game, "Player", PlayAgainstComputer ? "Stockfish" : "Analysis side", result);
            await _repository.SaveAsync(new SavedGame(
                0, DateTimeOffset.Now, "Player", PlayAgainstComputer ? "Stockfish" : "Analysis side",
                result, "Unlimited", ChessPosition.InitialFen, pgn, "", "",
                null, null, 0, 0, 0, 0, false));
            Feedback = "Game saved under %LocalAppData%\\ChessTeacher.";
        }
        catch (Exception ex)
        {
            Feedback = "Game could not be saved: " + ex.Message;
        }
    }

    private void UpdateStatus(MoveResult result)
    {
        Status = result.Termination switch
        {
            GameTermination.Checkmate =>
                _game.Position.SideToMove == PieceColor.White ? "Black wins by checkmate" : "White wins by checkmate",
            GameTermination.Stalemate => "Draw by stalemate",
            GameTermination.ThreefoldRepetition => "Draw by threefold repetition",
            GameTermination.FiftyMoveRule => "Draw by fifty-move rule",
            GameTermination.InsufficientMaterial => "Draw by insufficient material",
            _ => SideText() + (result.IsCheck ? " — check" : "")
        };
    }

    private string SideText() =>
        _game.Position.SideToMove == PieceColor.White ? "White to move" : "Black to move";

    private void RebuildMoveHistory()
    {
        MoveHistory.Clear();
        for (var i = 0; i < _game.Moves.Count; i += 2)
        {
            var white = _game.Moves[i].San;
            var black = i + 1 < _game.Moves.Count ? _game.Moves[i + 1].San : "";
            MoveHistory.Add($"{i / 2 + 1}. {white}  {black}");
        }
    }

    private void RefreshBoard()
    {
        var targets = _selected is null
            ? new HashSet<Square>()
            : _game.GenerateLegalMovesFrom(_selected.Value).Select(x => x.To).ToHashSet();

        for (var visual = 0; visual < 64; visual++)
        {
            var visualFile = visual % 8;
            var visualRank = visual / 8;
            var file = _flipped ? 7 - visualFile : visualFile;
            var rank = _flipped ? visualRank : 7 - visualRank;
            var square = new Square(file, rank);
            var vm = Squares[visual];
            vm.Square = square;
            vm.Glyph = _game.Position[square].ToUnicode();
            vm.Coordinate = _settings.ShowCoordinates ? square.ToString() : "";
            vm.Foreground = Brushes.Black;

            IBrush brush = (file + rank) % 2 == 0 ? DarkSquare : LightSquare;
            if (_lastMove is not null && (square == _lastMove.From || square == _lastMove.To))
                brush = LastMoveSquare;
            if (targets.Contains(square)) brush = LegalSquare;
            if (_selected == square) brush = SelectedSquare;

            var piece = _game.Position[square];
            if (piece.Type == PieceType.King && _game.IsInCheck(piece.Color))
                brush = CheckSquare;
            vm.Background = brush;
        }
    }
}

public sealed record NavigationItem(string Name, ICommand Command);
