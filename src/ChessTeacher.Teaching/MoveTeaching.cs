using ChessTeacher.Core.Models;

namespace ChessTeacher.Teaching;

public enum MoveClassification
{
    Brilliant,
    Great,
    Best,
    Excellent,
    Good,
    Book,
    Inaccuracy,
    Mistake,
    Blunder,
    MissedWin,
    ForcedMove
}

public sealed record MoveEvaluationContext(
    int? EvaluationBeforeCp,
    int? EvaluationAfterCp,
    int? MateBefore,
    int? MateAfter,
    bool IsForced,
    bool IsBook,
    bool IsSacrifice,
    bool FindsOnlyWinningMove,
    int MaterialChangeCp);

public sealed record ClassifiedMove(
    MoveClassification Classification,
    int CentipawnLoss,
    string Reason);

public sealed class MoveClassifier
{
    public ClassifiedMove Classify(MoveEvaluationContext context)
    {
        if (context.IsBook)
            return new(MoveClassification.Book, 0, "The move follows the local opening book.");
        if (context.IsForced)
            return new(MoveClassification.ForcedMove, 0, "Only one legal move preserved the position.");

        var loss = CalculateCentipawnLoss(context);

        if (context.MateBefore is > 0 && context.MateAfter is null)
            return new(MoveClassification.MissedWin, Math.Max(loss, 300),
                "A forced mate was available before the move but is no longer present.");
        if (context.MateAfter is < 0)
            return new(MoveClassification.Blunder, Math.Max(loss, 300),
                "The move allows a forced mate.");
        if (context.IsSacrifice && context.FindsOnlyWinningMove && loss <= 15)
            return new(MoveClassification.Brilliant, loss,
                "A sound sacrifice found the only clear route to a winning advantage.");
        if (context.FindsOnlyWinningMove && loss <= 35)
            return new(MoveClassification.Great, loss,
                "The move found a difficult winning continuation.");

        var classification = loss switch
        {
            <= 15 => MoveClassification.Best,
            <= 35 => MoveClassification.Excellent,
            <= 75 => MoveClassification.Good,
            <= 150 => MoveClassification.Inaccuracy,
            <= 300 => MoveClassification.Mistake,
            _ => MoveClassification.Blunder
        };

        return new(classification, loss, classification switch
        {
            MoveClassification.Best => $"The move stayed within {loss / 100.0:0.00} pawns of the best line.",
            MoveClassification.Excellent => $"The move kept nearly all of the position's value.",
            MoveClassification.Good => $"The move is playable, though a stronger line saved about {loss / 100.0:0.00} pawns.",
            MoveClassification.Inaccuracy => $"The move gave away roughly {loss / 100.0:0.00} pawns.",
            MoveClassification.Mistake => $"The position worsened by about {loss / 100.0:0.00} pawns.",
            _ => $"The move lost at least {loss / 100.0:0.00} pawns or changed the game decisively."
        });
    }

    public int CalculateCentipawnLoss(MoveEvaluationContext context)
    {
        if (context.EvaluationBeforeCp is not null && context.EvaluationAfterCp is not null)
            return Math.Clamp(context.EvaluationBeforeCp.Value - context.EvaluationAfterCp.Value, 0, 5000);
        if (context.MateBefore is > 0 && context.MateAfter is null) return 1000;
        if (context.MateAfter is < 0) return 1000;
        return 0;
    }
}

public static class AverageCentipawnLoss
{
    public static double Calculate(
        IEnumerable<(int LossCp, bool IsForced)> moves,
        bool ignoreForced = true,
        int capCp = 1000)
    {
        var values = moves.Where(x => !ignoreForced || !x.IsForced)
            .Select(x => Math.Clamp(x.LossCp, 0, capCp))
            .ToArray();
        return values.Length == 0 ? 0 : values.Average();
    }
}

public interface IExplanationService
{
    string Explain(
        ChessPosition before,
        ChessMove played,
        ChessMove? bestMove,
        ClassifiedMove classification,
        IReadOnlyList<string> themes);
}

public sealed class RuleBasedExplanationService : IExplanationService
{
    public string Explain(
        ChessPosition before,
        ChessMove played,
        ChessMove? bestMove,
        ClassifiedMove classification,
        IReadOnlyList<string> themes)
    {
        var movingPiece = before[played.From];
        var parts = new List<string> { classification.Reason };

        if (played.Flags.HasFlag(MoveFlags.CastleKingSide) ||
            played.Flags.HasFlag(MoveFlags.CastleQueenSide))
            parts.Add("Castling improves king safety and connects the rooks.");

        if (movingPiece.Type is PieceType.Knight or PieceType.Bishop &&
            IsHomeSquare(played.From, movingPiece.Color))
            parts.Add("This develops a minor piece and brings another unit into the game.");

        if (movingPiece.Type == PieceType.Pawn &&
            played.To.ToString() is "d4" or "e4" or "d5" or "e5")
            parts.Add("The pawn contests the centre and opens lines for development.");

        if (movingPiece.Type == PieceType.Queen && before.FullmoveNumber <= 5)
            parts.Add("An early queen move can lose time if the opponent develops while attacking it.");

        if (themes.Count > 0)
            parts.Add("Tactical themes detected: " + string.Join(", ", themes) + ".");

        if (bestMove is not null && bestMove != played)
            parts.Add($"A stronger practical idea was {bestMove.ToUci()}, which better preserves activity, safety, or material.");

        return string.Join(' ', parts);
    }

    private static bool IsHomeSquare(Square square, PieceColor color)
    {
        var rank = color == PieceColor.White ? 0 : 7;
        return square.Rank == rank && square.File is 1 or 2 or 5 or 6;
    }
}

public static class TacticalThemeDetector
{
    public static IReadOnlyList<string> Detect(ChessPosition before, ChessPosition after, ChessMove move)
    {
        var themes = new List<string>();
        var piece = after[move.To];

        if (piece.Type == PieceType.Knight)
        {
            var targets = after.Pieces().Count(x =>
                x.Piece.Color != piece.Color &&
                x.Piece.Type is PieceType.Queen or PieceType.Rook or PieceType.King &&
                IsKnightAttack(move.To, x.Square));
            if (targets >= 2) themes.Add("fork");
        }

        if (move.Flags.HasFlag(MoveFlags.Capture)) themes.Add("capture");
        if (piece.Type == PieceType.Pawn && move.To.Rank is 6 or 1)
            themes.Add("advanced passed-pawn candidate");
        return themes;
    }

    private static bool IsKnightAttack(Square from, Square to)
    {
        var df = Math.Abs(from.File - to.File);
        var dr = Math.Abs(from.Rank - to.Rank);
        return (df == 1 && dr == 2) || (df == 2 && dr == 1);
    }
}
