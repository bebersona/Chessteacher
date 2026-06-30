namespace ChessTeacher.Core.Models;

public enum PieceColor { White, Black }
public enum PieceType { None, Pawn, Knight, Bishop, Rook, Queen, King }

[Flags]
public enum CastlingRights
{
    None = 0,
    WhiteKingSide = 1,
    WhiteQueenSide = 2,
    BlackKingSide = 4,
    BlackQueenSide = 8
}

[Flags]
public enum MoveFlags
{
    None = 0,
    Capture = 1,
    EnPassant = 2,
    CastleKingSide = 4,
    CastleQueenSide = 8,
    PawnDouble = 16,
    Promotion = 32
}

public readonly record struct Piece(PieceColor Color, PieceType Type)
{
    public bool IsEmpty => Type == PieceType.None;
    public static Piece Empty => new(PieceColor.White, PieceType.None);

    public char ToFenChar()
    {
        var value = Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => ' '
        };
        return Color == PieceColor.White ? char.ToUpperInvariant(value) : value;
    }

    public string ToUnicode() => (Color, Type) switch
    {
        (PieceColor.White, PieceType.King) => "♔",
        (PieceColor.White, PieceType.Queen) => "♕",
        (PieceColor.White, PieceType.Rook) => "♖",
        (PieceColor.White, PieceType.Bishop) => "♗",
        (PieceColor.White, PieceType.Knight) => "♘",
        (PieceColor.White, PieceType.Pawn) => "♙",
        (PieceColor.Black, PieceType.King) => "♚",
        (PieceColor.Black, PieceType.Queen) => "♛",
        (PieceColor.Black, PieceType.Rook) => "♜",
        (PieceColor.Black, PieceType.Bishop) => "♝",
        (PieceColor.Black, PieceType.Knight) => "♞",
        (PieceColor.Black, PieceType.Pawn) => "♟",
        _ => string.Empty
    };
}

public readonly record struct Square(int File, int Rank)
{
    public bool IsValid => File is >= 0 and < 8 && Rank is >= 0 and < 8;
    public int Index => Rank * 8 + File;
    public override string ToString() => IsValid ? $"{(char)('a' + File)}{Rank + 1}" : "-";

    public static bool TryParse(string? text, out Square square)
    {
        square = default;
        if (string.IsNullOrWhiteSpace(text) || text.Length != 2) return false;
        square = new Square(char.ToLowerInvariant(text[0]) - 'a', text[1] - '1');
        return square.IsValid;
    }

    public static Square FromIndex(int index) => new(index % 8, index / 8);
}

public sealed record ChessMove(
    Square From,
    Square To,
    PieceType Promotion = PieceType.None,
    MoveFlags Flags = MoveFlags.None)
{
    public string ToUci()
    {
        var promotion = Promotion switch
        {
            PieceType.Queen => "q",
            PieceType.Rook => "r",
            PieceType.Bishop => "b",
            PieceType.Knight => "n",
            _ => string.Empty
        };
        return $"{From}{To}{promotion}";
    }
}

public enum GameTermination
{
    None,
    Checkmate,
    Stalemate,
    ThreefoldRepetition,
    FiftyMoveRule,
    InsufficientMaterial,
    Resignation,
    DrawAgreement
}

public sealed record MoveResult(
    bool Success,
    string Error,
    ChessMove? Move,
    string San,
    bool IsCheck,
    bool IsCheckmate,
    GameTermination Termination)
{
    public static MoveResult Failed(string error) =>
        new(false, error, null, string.Empty, false, false, GameTermination.None);
}

public sealed record PlayedMove(ChessMove Move, string San, string FenAfter);

public sealed record SavedGame(
    long Id,
    DateTimeOffset PlayedAt,
    string WhitePlayer,
    string BlackPlayer,
    string Result,
    string TimeControl,
    string StartingFen,
    string Pgn,
    string OpeningName,
    string EcoCode,
    double? WhiteAcpl,
    double? BlackAcpl,
    int BestMoves,
    int Inaccuracies,
    int Mistakes,
    int Blunders,
    bool AnalysisComplete);
