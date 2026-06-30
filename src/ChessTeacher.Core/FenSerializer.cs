using System.Globalization;
using System.Text;
using ChessTeacher.Core.Models;

namespace ChessTeacher.Core;

public static class FenSerializer
{
    public static ChessPosition Parse(string fen)
    {
        if (!TryParse(fen, out var position, out var error))
            throw new FormatException(error);
        return position!;
    }

    public static bool TryParse(string? fen, out ChessPosition? position, out string error)
    {
        position = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(fen))
        {
            error = "FEN is empty.";
            return false;
        }

        var fields = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 6)
        {
            error = "FEN must contain exactly six fields.";
            return false;
        }

        var ranks = fields[0].Split('/');
        if (ranks.Length != 8)
        {
            error = "FEN board must contain eight ranks.";
            return false;
        }

        var result = new ChessPosition();
        var whiteKings = 0;
        var blackKings = 0;

        for (var fenRank = 0; fenRank < 8; fenRank++)
        {
            var file = 0;
            var rank = 7 - fenRank;
            foreach (var token in ranks[fenRank])
            {
                if (char.IsDigit(token))
                {
                    var empty = token - '0';
                    if (empty is < 1 or > 8) { error = "Invalid empty-square count."; return false; }
                    file += empty;
                    continue;
                }

                var color = char.IsUpper(token) ? PieceColor.White : PieceColor.Black;
                var type = char.ToLowerInvariant(token) switch
                {
                    'p' => PieceType.Pawn,
                    'n' => PieceType.Knight,
                    'b' => PieceType.Bishop,
                    'r' => PieceType.Rook,
                    'q' => PieceType.Queen,
                    'k' => PieceType.King,
                    _ => PieceType.None
                };

                if (type == PieceType.None || file >= 8)
                {
                    error = $"Invalid board token '{token}'.";
                    return false;
                }

                result[file, rank] = new Piece(color, type);
                if (type == PieceType.King)
                {
                    if (color == PieceColor.White) whiteKings++;
                    else blackKings++;
                }
                file++;
            }
            if (file != 8) { error = $"Rank {8 - fenRank} does not contain eight squares."; return false; }
        }

        if (whiteKings != 1 || blackKings != 1)
        {
            error = "A valid position must contain exactly one king of each color.";
            return false;
        }

        if (fields[1] == "w") result.SideToMove = PieceColor.White;
        else if (fields[1] == "b") result.SideToMove = PieceColor.Black;
        else { error = "Side to move must be w or b."; return false; }

        result.CastlingRights = CastlingRights.None;
        if (fields[2] != "-")
        {
            foreach (var c in fields[2])
            {
                result.CastlingRights |= c switch
                {
                    'K' => CastlingRights.WhiteKingSide,
                    'Q' => CastlingRights.WhiteQueenSide,
                    'k' => CastlingRights.BlackKingSide,
                    'q' => CastlingRights.BlackQueenSide,
                    _ => CastlingRights.None
                };
                if (c is not ('K' or 'Q' or 'k' or 'q'))
                {
                    error = "Invalid castling rights.";
                    return false;
                }
            }
        }

        if (fields[3] != "-")
        {
            if (!Square.TryParse(fields[3], out var ep) || ep.Rank is not (2 or 5))
            {
                error = "Invalid en-passant target square.";
                return false;
            }
            result.EnPassantTarget = ep;
        }

        if (!int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out var halfmove) || halfmove < 0)
        {
            error = "Invalid halfmove clock.";
            return false;
        }
        if (!int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out var fullmove) || fullmove < 1)
        {
            error = "Invalid fullmove number.";
            return false;
        }

        result.HalfmoveClock = halfmove;
        result.FullmoveNumber = fullmove;
        position = result;
        return true;
    }

    public static string ToFen(ChessPosition position)
    {
        var board = new StringBuilder();
        for (var rank = 7; rank >= 0; rank--)
        {
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                var piece = position[file, rank];
                if (piece.IsEmpty) { empty++; continue; }
                if (empty > 0) { board.Append(empty); empty = 0; }
                board.Append(piece.ToFenChar());
            }
            if (empty > 0) board.Append(empty);
            if (rank > 0) board.Append('/');
        }

        var castling = new StringBuilder();
        if (position.CastlingRights.HasFlag(CastlingRights.WhiteKingSide)) castling.Append('K');
        if (position.CastlingRights.HasFlag(CastlingRights.WhiteQueenSide)) castling.Append('Q');
        if (position.CastlingRights.HasFlag(CastlingRights.BlackKingSide)) castling.Append('k');
        if (position.CastlingRights.HasFlag(CastlingRights.BlackQueenSide)) castling.Append('q');

        return string.Join(' ',
            board.ToString(),
            position.SideToMove == PieceColor.White ? "w" : "b",
            castling.Length == 0 ? "-" : castling.ToString(),
            position.EnPassantTarget?.ToString() ?? "-",
            position.HalfmoveClock,
            position.FullmoveNumber);
    }
}
