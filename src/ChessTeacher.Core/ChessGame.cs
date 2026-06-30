using ChessTeacher.Core.Models;

namespace ChessTeacher.Core;

public sealed class ChessGame
{
    private readonly Stack<State> _undo = new();
    private readonly Stack<State> _redo = new();
    private readonly List<string> _repetitions = new();
    private readonly List<PlayedMove> _moves = new();

    public ChessPosition Position { get; private set; }
    public IReadOnlyList<PlayedMove> Moves => _moves;
    public GameTermination Termination { get; private set; }
    public bool IsGameOver => Termination != GameTermination.None;

    public ChessGame() : this(ChessPosition.CreateInitial()) { }

    public ChessGame(ChessPosition position)
    {
        Position = position.Clone();
        _repetitions.Add(Position.RepetitionKey());
        Termination = DetermineTermination();
    }

    public void Reset(ChessPosition? position = null)
    {
        Position = (position ?? ChessPosition.CreateInitial()).Clone();
        _undo.Clear();
        _redo.Clear();
        _moves.Clear();
        _repetitions.Clear();
        _repetitions.Add(Position.RepetitionKey());
        Termination = DetermineTermination();
    }

    public IReadOnlyList<ChessMove> GenerateLegalMoves() => GenerateLegalMoves(Position.SideToMove);

    public IReadOnlyList<ChessMove> GenerateLegalMoves(PieceColor color)
    {
        var result = new List<ChessMove>(64);
        foreach (var (square, piece) in Position.Pieces())
        {
            if (piece.Color != color) continue;
            foreach (var move in GeneratePseudoMoves(Position, square))
            {
                var clone = Position.Clone();
                ApplyUnchecked(clone, move);
                if (!IsInCheck(clone, color)) result.Add(move);
            }
        }
        return result;
    }

    public IReadOnlyList<ChessMove> GenerateLegalMovesFrom(Square square) =>
        GenerateLegalMoves().Where(x => x.From == square).ToArray();

    public MoveResult TryMakeMove(ChessMove requested)
    {
        if (IsGameOver) return MoveResult.Failed("The game has ended.");
        var legal = GenerateLegalMoves();
        var move = legal.FirstOrDefault(x =>
            x.From == requested.From && x.To == requested.To &&
            (requested.Promotion == PieceType.None || requested.Promotion == x.Promotion));
        if (move is null) return MoveResult.Failed("That move is illegal.");

        if (move.Flags.HasFlag(MoveFlags.Promotion) && requested.Promotion != PieceType.None)
            move = move with { Promotion = requested.Promotion };

        var state = CaptureState();
        var san = BuildSanBase(Position, move, legal);
        ApplyUnchecked(Position, move);
        var check = IsInCheck(Position, Position.SideToMove);
        var mate = check && GenerateLegalMoves().Count == 0;
        san += mate ? "#" : check ? "+" : string.Empty;

        _undo.Push(state);
        _redo.Clear();
        _moves.Add(new PlayedMove(move, san, FenSerializer.ToFen(Position)));
        _repetitions.Add(Position.RepetitionKey());
        Termination = DetermineTermination();

        return new MoveResult(true, string.Empty, move, san, check, mate, Termination);
    }

    public bool TryMakeUci(string uci, out MoveResult result)
    {
        result = MoveResult.Failed("Invalid UCI move.");
        if (uci.Length is < 4 or > 5 ||
            !Square.TryParse(uci[..2], out var from) ||
            !Square.TryParse(uci.Substring(2, 2), out var to))
            return false;

        var promotion = uci.Length == 5 ? char.ToLowerInvariant(uci[4]) switch
        {
            'q' => PieceType.Queen,
            'r' => PieceType.Rook,
            'b' => PieceType.Bishop,
            'n' => PieceType.Knight,
            _ => PieceType.None
        } : PieceType.None;

        result = TryMakeMove(new ChessMove(from, to, promotion));
        return result.Success;
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Push(CaptureState());
        RestoreState(_undo.Pop());
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Push(CaptureState());
        RestoreState(_redo.Pop());
        return true;
    }

    public bool IsInCheck(PieceColor color) => IsInCheck(Position, color);

    public static bool IsInCheck(ChessPosition position, PieceColor color)
    {
        var king = position.Pieces()
            .FirstOrDefault(x => x.Piece.Color == color && x.Piece.Type == PieceType.King).Square;
        return IsSquareAttacked(position, king, Opposite(color));
    }

    public static bool IsSquareAttacked(ChessPosition position, Square target, PieceColor byColor)
    {
        foreach (var (from, piece) in position.Pieces())
        {
            if (piece.Color != byColor) continue;
            var df = target.File - from.File;
            var dr = target.Rank - from.Rank;
            switch (piece.Type)
            {
                case PieceType.Pawn:
                    var direction = byColor == PieceColor.White ? 1 : -1;
                    if (dr == direction && Math.Abs(df) == 1) return true;
                    break;
                case PieceType.Knight:
                    if ((Math.Abs(df) == 1 && Math.Abs(dr) == 2) ||
                        (Math.Abs(df) == 2 && Math.Abs(dr) == 1)) return true;
                    break;
                case PieceType.King:
                    if (Math.Max(Math.Abs(df), Math.Abs(dr)) == 1) return true;
                    break;
                case PieceType.Bishop:
                    if (Math.Abs(df) == Math.Abs(dr) && RayClear(position, from, target)) return true;
                    break;
                case PieceType.Rook:
                    if ((df == 0 || dr == 0) && RayClear(position, from, target)) return true;
                    break;
                case PieceType.Queen:
                    if ((df == 0 || dr == 0 || Math.Abs(df) == Math.Abs(dr)) &&
                        RayClear(position, from, target)) return true;
                    break;
            }
        }
        return false;
    }

    private static bool RayClear(ChessPosition position, Square from, Square to)
    {
        var sf = Math.Sign(to.File - from.File);
        var sr = Math.Sign(to.Rank - from.Rank);
        var current = new Square(from.File + sf, from.Rank + sr);
        while (current != to)
        {
            if (!position[current].IsEmpty) return false;
            current = new Square(current.File + sf, current.Rank + sr);
        }
        return true;
    }

    private IEnumerable<ChessMove> GeneratePseudoMoves(ChessPosition position, Square from)
    {
        var piece = position[from];
        return piece.Type switch
        {
            PieceType.Pawn => PawnMoves(position, from, piece.Color),
            PieceType.Knight => KnightMoves(position, from, piece.Color),
            PieceType.Bishop => SlidingMoves(position, from, piece.Color, Diagonals),
            PieceType.Rook => SlidingMoves(position, from, piece.Color, Straights),
            PieceType.Queen => SlidingMoves(position, from, piece.Color, Queens),
            PieceType.King => KingMoves(position, from, piece.Color),
            _ => Array.Empty<ChessMove>()
        };
    }

    private static readonly (int f, int r)[] Diagonals = [(-1,-1),(-1,1),(1,-1),(1,1)];
    private static readonly (int f, int r)[] Straights = [(-1,0),(1,0),(0,-1),(0,1)];
    private static readonly (int f, int r)[] Queens =
        [(-1,-1),(-1,1),(1,-1),(1,1),(-1,0),(1,0),(0,-1),(0,1)];
    private static readonly (int f, int r)[] Knights =
        [(-2,-1),(-2,1),(-1,-2),(-1,2),(1,-2),(1,2),(2,-1),(2,1)];

    private static IEnumerable<ChessMove> PawnMoves(ChessPosition position, Square from, PieceColor color)
    {
        var list = new List<ChessMove>();
        var direction = color == PieceColor.White ? 1 : -1;
        var startRank = color == PieceColor.White ? 1 : 6;
        var promotionRank = color == PieceColor.White ? 7 : 0;

        var one = new Square(from.File, from.Rank + direction);
        if (one.IsValid && position[one].IsEmpty)
        {
            AddPawnMove(list, from, one, MoveFlags.None, promotionRank);
            var two = new Square(from.File, from.Rank + 2 * direction);
            if (from.Rank == startRank && position[two].IsEmpty)
                list.Add(new ChessMove(from, two, PieceType.None, MoveFlags.PawnDouble));
        }

        foreach (var fileDelta in new[] { -1, 1 })
        {
            var to = new Square(from.File + fileDelta, from.Rank + direction);
            if (!to.IsValid) continue;
            if (!position[to].IsEmpty && position[to].Color != color && position[to].Type != PieceType.King)
                AddPawnMove(list, from, to, MoveFlags.Capture, promotionRank);
            else if (position.EnPassantTarget == to)
                list.Add(new ChessMove(from, to, PieceType.None, MoveFlags.Capture | MoveFlags.EnPassant));
        }
        return list;
    }

    private static void AddPawnMove(List<ChessMove> list, Square from, Square to, MoveFlags flags, int promotionRank)
    {
        if (to.Rank == promotionRank)
        {
            foreach (var type in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
                list.Add(new ChessMove(from, to, type, flags | MoveFlags.Promotion));
        }
        else list.Add(new ChessMove(from, to, PieceType.None, flags));
    }

    private static IEnumerable<ChessMove> KnightMoves(ChessPosition position, Square from, PieceColor color)
    {
        foreach (var (df, dr) in Knights)
        {
            var to = new Square(from.File + df, from.Rank + dr);
            if (!to.IsValid) continue;
            var target = position[to];
            if (target.IsEmpty) yield return new ChessMove(from, to);
            else if (target.Color != color && target.Type != PieceType.King)
                yield return new ChessMove(from, to, PieceType.None, MoveFlags.Capture);
        }
    }

    private static IEnumerable<ChessMove> SlidingMoves(
        ChessPosition position, Square from, PieceColor color, IEnumerable<(int f, int r)> directions)
    {
        foreach (var (df, dr) in directions)
        {
            var to = new Square(from.File + df, from.Rank + dr);
            while (to.IsValid)
            {
                var target = position[to];
                if (target.IsEmpty) yield return new ChessMove(from, to);
                else
                {
                    if (target.Color != color && target.Type != PieceType.King)
                        yield return new ChessMove(from, to, PieceType.None, MoveFlags.Capture);
                    break;
                }
                to = new Square(to.File + df, to.Rank + dr);
            }
        }
    }

    private IEnumerable<ChessMove> KingMoves(ChessPosition position, Square from, PieceColor color)
    {
        for (var df = -1; df <= 1; df++)
        for (var dr = -1; dr <= 1; dr++)
        {
            if (df == 0 && dr == 0) continue;
            var to = new Square(from.File + df, from.Rank + dr);
            if (!to.IsValid) continue;
            var target = position[to];
            if (target.IsEmpty) yield return new ChessMove(from, to);
            else if (target.Color != color && target.Type != PieceType.King)
                yield return new ChessMove(from, to, PieceType.None, MoveFlags.Capture);
        }

        if (IsInCheck(position, color)) yield break;
        var rank = color == PieceColor.White ? 0 : 7;
        var enemy = Opposite(color);
        var kingRight = color == PieceColor.White ? CastlingRights.WhiteKingSide : CastlingRights.BlackKingSide;
        var queenRight = color == PieceColor.White ? CastlingRights.WhiteQueenSide : CastlingRights.BlackQueenSide;

        if (from == new Square(4, rank) && position.CastlingRights.HasFlag(kingRight) &&
            position[5, rank].IsEmpty && position[6, rank].IsEmpty &&
            position[7, rank] == new Piece(color, PieceType.Rook) &&
            !IsSquareAttacked(position, new Square(5, rank), enemy) &&
            !IsSquareAttacked(position, new Square(6, rank), enemy))
            yield return new ChessMove(from, new Square(6, rank), PieceType.None, MoveFlags.CastleKingSide);

        if (from == new Square(4, rank) && position.CastlingRights.HasFlag(queenRight) &&
            position[1, rank].IsEmpty && position[2, rank].IsEmpty && position[3, rank].IsEmpty &&
            position[0, rank] == new Piece(color, PieceType.Rook) &&
            !IsSquareAttacked(position, new Square(3, rank), enemy) &&
            !IsSquareAttacked(position, new Square(2, rank), enemy))
            yield return new ChessMove(from, new Square(2, rank), PieceType.None, MoveFlags.CastleQueenSide);
    }

    private static void ApplyUnchecked(ChessPosition position, ChessMove move)
    {
        var moving = position[move.From];
        var captured = position[move.To];

        if (move.Flags.HasFlag(MoveFlags.EnPassant))
        {
            var rank = move.To.Rank + (moving.Color == PieceColor.White ? -1 : 1);
            captured = position[move.To.File, rank];
            position[move.To.File, rank] = Piece.Empty;
        }

        position[move.From] = Piece.Empty;
        position[move.To] = move.Flags.HasFlag(MoveFlags.Promotion)
            ? new Piece(moving.Color, move.Promotion)
            : moving;

        if (move.Flags.HasFlag(MoveFlags.CastleKingSide))
        {
            var rank = moving.Color == PieceColor.White ? 0 : 7;
            position[5, rank] = position[7, rank];
            position[7, rank] = Piece.Empty;
        }
        if (move.Flags.HasFlag(MoveFlags.CastleQueenSide))
        {
            var rank = moving.Color == PieceColor.White ? 0 : 7;
            position[3, rank] = position[0, rank];
            position[0, rank] = Piece.Empty;
        }

        UpdateCastling(position, moving, move, captured);
        position.EnPassantTarget = move.Flags.HasFlag(MoveFlags.PawnDouble)
            ? new Square(move.From.File, (move.From.Rank + move.To.Rank) / 2)
            : null;
        position.HalfmoveClock =
            moving.Type == PieceType.Pawn || !captured.IsEmpty ? 0 : position.HalfmoveClock + 1;
        if (moving.Color == PieceColor.Black) position.FullmoveNumber++;
        position.SideToMove = Opposite(position.SideToMove);
    }

    private static void UpdateCastling(ChessPosition position, Piece moving, ChessMove move, Piece captured)
    {
        if (moving.Type == PieceType.King)
        {
            if (moving.Color == PieceColor.White)
                position.CastlingRights &= ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
            else
                position.CastlingRights &= ~(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
        }
        if (moving.Type == PieceType.Rook) RemoveRookRight(position, move.From, moving.Color);
        if (captured.Type == PieceType.Rook) RemoveRookRight(position, move.To, captured.Color);
    }

    private static void RemoveRookRight(ChessPosition position, Square square, PieceColor color)
    {
        if (color == PieceColor.White)
        {
            if (square == new Square(0, 0)) position.CastlingRights &= ~CastlingRights.WhiteQueenSide;
            if (square == new Square(7, 0)) position.CastlingRights &= ~CastlingRights.WhiteKingSide;
        }
        else
        {
            if (square == new Square(0, 7)) position.CastlingRights &= ~CastlingRights.BlackQueenSide;
            if (square == new Square(7, 7)) position.CastlingRights &= ~CastlingRights.BlackKingSide;
        }
    }

    private GameTermination DetermineTermination()
    {
        var legal = GenerateLegalMoves();
        if (legal.Count == 0)
            return IsInCheck(Position.SideToMove) ? GameTermination.Checkmate : GameTermination.Stalemate;
        if (Position.HalfmoveClock >= 100) return GameTermination.FiftyMoveRule;
        if (_repetitions.Count(x => x == Position.RepetitionKey()) >= 3)
            return GameTermination.ThreefoldRepetition;
        if (InsufficientMaterial(Position)) return GameTermination.InsufficientMaterial;
        return GameTermination.None;
    }

    private static bool InsufficientMaterial(ChessPosition position)
    {
        var nonKings = position.Pieces().Where(x => x.Piece.Type != PieceType.King).ToArray();
        if (nonKings.Length == 0) return true;
        if (nonKings.Length == 1 && nonKings[0].Piece.Type is PieceType.Bishop or PieceType.Knight) return true;
        if (nonKings.All(x => x.Piece.Type == PieceType.Bishop))
            return nonKings.Select(x => (x.Square.File + x.Square.Rank) % 2).Distinct().Count() == 1;
        return false;
    }

    private static string BuildSanBase(ChessPosition position, ChessMove move, IReadOnlyList<ChessMove> legal)
    {
        if (move.Flags.HasFlag(MoveFlags.CastleKingSide)) return "O-O";
        if (move.Flags.HasFlag(MoveFlags.CastleQueenSide)) return "O-O-O";
        var piece = position[move.From];
        var capture = move.Flags.HasFlag(MoveFlags.Capture);
        var san = string.Empty;

        if (piece.Type == PieceType.Pawn)
        {
            if (capture) san += (char)('a' + move.From.File);
        }
        else
        {
            san += piece.Type switch
            {
                PieceType.Knight => "N",
                PieceType.Bishop => "B",
                PieceType.Rook => "R",
                PieceType.Queen => "Q",
                PieceType.King => "K",
                _ => ""
            };
            var competing = legal.Where(x =>
                x.To == move.To && x.From != move.From && position[x.From].Type == piece.Type).ToArray();
            if (competing.Length > 0)
            {
                var sameFile = competing.Any(x => x.From.File == move.From.File);
                var sameRank = competing.Any(x => x.From.Rank == move.From.Rank);
                if (!sameFile) san += (char)('a' + move.From.File);
                else if (!sameRank) san += move.From.Rank + 1;
                else san += move.From.ToString();
            }
        }

        if (capture) san += "x";
        san += move.To;
        if (move.Flags.HasFlag(MoveFlags.Promotion))
        {
            san += "=" + (move.Promotion switch
            {
                PieceType.Queen => "Q",
                PieceType.Rook => "R",
                PieceType.Bishop => "B",
                PieceType.Knight => "N",
                _ => "Q"
            });
        }
        return san;
    }

    public bool TryParseSan(string san, out ChessMove? move)
    {
        move = null;
        var normalized = NormalizeSan(san);
        var legal = GenerateLegalMoves();
        foreach (var candidate in legal)
        {
            if (NormalizeSan(BuildSanBase(Position, candidate, legal)) == normalized)
            {
                move = candidate;
                return true;
            }
        }
        return false;
    }

    private static string NormalizeSan(string san) =>
        san.Trim().Replace("+", "").Replace("#", "").Replace("!", "").Replace("?", "")
            .Replace("0-0-0", "O-O-O", StringComparison.OrdinalIgnoreCase)
            .Replace("0-0", "O-O", StringComparison.OrdinalIgnoreCase);

    private State CaptureState() =>
        new(Position.Clone(), _moves.ToList(), _repetitions.ToList(), Termination);

    private void RestoreState(State state)
    {
        Position = state.Position.Clone();
        _moves.Clear(); _moves.AddRange(state.Moves);
        _repetitions.Clear(); _repetitions.AddRange(state.Repetitions);
        Termination = state.Termination;
    }

    private static PieceColor Opposite(PieceColor color) =>
        color == PieceColor.White ? PieceColor.Black : PieceColor.White;

    private sealed record State(
        ChessPosition Position,
        List<PlayedMove> Moves,
        List<string> Repetitions,
        GameTermination Termination);
}
