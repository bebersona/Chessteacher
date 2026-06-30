namespace ChessTeacher.Core.Models;

public sealed class ChessPosition
{
    public const string InitialFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    private readonly Piece[] _board = new Piece[64];

    public PieceColor SideToMove { get; set; } = PieceColor.White;
    public CastlingRights CastlingRights { get; set; } =
        CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide |
        CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide;
    public Square? EnPassantTarget { get; set; }
    public int HalfmoveClock { get; set; }
    public int FullmoveNumber { get; set; } = 1;

    public Piece this[Square square]
    {
        get => square.IsValid ? _board[square.Index] : Piece.Empty;
        set
        {
            if (!square.IsValid) throw new ArgumentOutOfRangeException(nameof(square));
            _board[square.Index] = value;
        }
    }

    public Piece this[int file, int rank]
    {
        get => this[new Square(file, rank)];
        set => this[new Square(file, rank)] = value;
    }

    public IEnumerable<(Square Square, Piece Piece)> Pieces()
    {
        for (var i = 0; i < 64; i++)
            if (!_board[i].IsEmpty)
                yield return (Square.FromIndex(i), _board[i]);
    }

    public ChessPosition Clone()
    {
        var clone = new ChessPosition
        {
            SideToMove = SideToMove,
            CastlingRights = CastlingRights,
            EnPassantTarget = EnPassantTarget,
            HalfmoveClock = HalfmoveClock,
            FullmoveNumber = FullmoveNumber
        };
        Array.Copy(_board, clone._board, 64);
        return clone;
    }

    public string RepetitionKey()
    {
        var fields = FenSerializer.ToFen(this).Split(' ');
        return string.Join(' ', fields.Take(4));
    }

    public static ChessPosition CreateInitial() => FenSerializer.Parse(InitialFen);
}
